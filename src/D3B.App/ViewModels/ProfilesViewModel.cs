using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels.Settings;
using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Bot.Settings;
using D3B.Data.Texts;
using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>The profiles page: the profiles as cards, or the one being edited on the whole page (D99, D101, D159).</summary>
/// <remarks>
/// It reads the profiles and characters from memory each refresh, and makes its items again only once their books
/// changed: a profile being edited keeps its editor while the relay records what it sees, and while another page shows.
/// </remarks>
internal sealed partial class ProfilesViewModel : ObservableObject
{
    private const int SummaryLength = 3;
    private const int CopyAttempts = 50;

    private readonly Dictionary<int, ProfileViewModel> _byId = [];
    private IServiceProvider? _services;
    private ActivityCatalog? _catalog;
    private int _profilesVersion = -1;
    private int _accountsVersion = -1;
    private CultureInfo? _culture;

    /// <summary>Gets the profiles, a new one first, then by name.</summary>
    public ObservableCollection<ProfileViewModel> Items { get; } = [];

    /// <summary>Gets the app's texts.</summary>
    public Localizer Localizer { get; }

    /// <summary>Gets the window's dialogs.</summary>
    public Dialogs Dialogs { get; }

    /// <summary>Gets what the window says in its toasts.</summary>
    public Toasts Toasts { get; }

    /// <summary>Gets the activities a profile may run, named in the app's language.</summary>
    public IReadOnlyList<ActivityChoiceViewModel> Activities { get; private set; } = [];

    /// <summary>Gets the profiles, kept in the database.</summary>
    public ProfileBook? Profiles { get; private set; }

    /// <summary>Gets the game's values the settings name.</summary>
    public GameReferences? Game => _services?.GetService<GameReferences>();

    /// <summary>Gets what names those values.</summary>
    public GameNames Names { get; private set; }

    /// <summary>Gets the host's services, once it runs: an activity reckons what its settings lead to from them (D127).</summary>
    public IServiceProvider? Services => _services;

    /// <summary>Gets the profile whose editor shows on the page, if any.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListing))]
    public partial ProfileViewModel? Editing { get; private set; }

    /// <summary>Gets whether there is no profile.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets whether the host keeps profiles: its database opened.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    /// <summary>Gets whether the page shows the profiles, no editor open.</summary>
    public bool IsListing => Editing is null;

    private AccountBook? Accounts => _services?.GetService<AccountBook>();

    /// <summary>Initializes a new instance of the <see cref="ProfilesViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="dialogs">The window's dialogs.</param>
    /// <param name="toasts">What the window says in its toasts.</param>
    public ProfilesViewModel(Localizer localizer, Dialogs dialogs, Toasts toasts)
    {
        Localizer = localizer;
        Dialogs = dialogs;
        Toasts = toasts;
        Names = new GameNames(null, localizer);
    }

    /// <summary>Reads the profiles again, in the language chosen, once they changed.</summary>
    /// <param name="services">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _services is null)
        {
            _services = services;
            _catalog = services.GetService<ActivityCatalog>();
            Profiles = services.GetService<ProfileBook>();
            Names = new GameNames(services.GetService<GameTexts>(), Localizer);
            IsAvailable = Profiles is not null;
        }

        if (Profiles is null)
            return;

        var relabel = !Localizer.Culture.Equals(_culture);

        if (relabel)
        {
            _culture = Localizer.Culture;
            Activities = [.. (_catalog?.All ?? []).Select(definition => new ActivityChoiceViewModel(definition, Localizer.Culture))];
            OnPropertyChanged(nameof(Activities));
        }

        if (relabel || Profiles.Version != _profilesVersion || (Accounts?.Version ?? 0) != _accountsVersion)
            Sync(relabel);
    }

    /// <summary>Finds an activity the modules registered.</summary>
    /// <param name="id">Its id.</param>
    /// <returns>The activity, or <see langword="null"/> when none has the id.</returns>
    public ActivityDefinition? ActivityOf(string id)
    {
        return _catalog is not null && _catalog.TryGet(id, out var definition) ? definition : null;
    }

    /// <summary>Gets the characters that play a profile.</summary>
    /// <param name="id">The profile's id.</param>
    /// <returns>The characters.</returns>
    public IReadOnlyList<KnownCharacter> PlayersOf(int id)
    {
        return [.. (Accounts?.Characters ?? []).Where(character => character.ProfileId == id)];
    }

    /// <summary>Gets the characters that play a profile, with their levels and their map, as their bots last saw them.</summary>
    /// <param name="id">The profile's id.</param>
    /// <returns>The characters; those no bot plays yet with no level known.</returns>
    public IReadOnlyList<PreviewPlayer> LevelsOf(int id)
    {
        var bots = _services?.GetService<Bots>();

        return
        [
            .. PlayersOf(id).Select(character => bots is not null && bots.TryGet(character.Key, out var bot)
                ? new PreviewPlayer(character.Name, bot.State.Level ?? 0, bot.State.Jobs.Values.ToDictionary(job => job.JobId, job => job.Level), bot.State.MapId)
                : new PreviewPlayer(character.Name, 0, new Dictionary<int, int>(), null)),
        ];
    }

    /// <summary>Says a few of a profile's settings, those that show, at a glance.</summary>
    /// <param name="declared">The settings its activity declares.</param>
    /// <param name="settings">The profile's settings.</param>
    /// <returns>Three of them at most, joined.</returns>
    public string Summarize(ActivitySettings declared, object? settings)
    {
        if (settings is null)
            return "";

        var culture = Localizer.Culture;
        var parts = new List<string>();

        foreach (var setting in declared.All.Where(setting => declared.IsVisible(setting, settings)))
        {
            // The game's values say what they are, a resource or a sub-area; a flag says its name, a number with it.
            if (Describe(setting, settings) is { Length: > 0 } value)
                parts.Add(setting.Kind is SettingKind.Number ? Localizer.Format("SummaryItem", setting.Name.In(culture), value) : value);

            if (parts.Count == SummaryLength)
                break;
        }

        return string.Join(" · ", parts);
    }

    /// <summary>Shows a profile's editor on the page, the only one open.</summary>
    /// <param name="item">The profile being edited.</param>
    public void BeginEditing(ProfileViewModel item)
    {
        if (Editing is { } other && other != item)
            other.Cancel();

        Editing = item;
    }

    /// <summary>Shows the profiles again, once a profile's editor closed.</summary>
    /// <param name="item">The profile that was edited.</param>
    public void EndEditing(ProfileViewModel item)
    {
        if (Editing == item)
            Editing = null;
    }

    /// <summary>Opens the editor of a new profile, its name and activity chosen, its settings another's or the activity's defaults.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="activity">Its activity.</param>
    /// <param name="settings">The settings it starts from, a profile's, or <see langword="null"/> for the activity's defaults.</param>
    public void Create(string name, ActivityChoiceViewModel activity, string? settings)
    {
        new ProfileViewModel(this, null).EditNew(name, activity, settings);
    }

    /// <summary>Asks the name of a copy of a profile, which then opens in the editor.</summary>
    /// <param name="item">The profile copied.</param>
    public void Duplicate(ProfileViewModel item)
    {
        Dialogs.Show(new NewProfileViewModel(this, item));
    }

    /// <summary>Gives a name no profile has, from a profile's own.</summary>
    /// <param name="name">The profile's name.</param>
    /// <returns>Its copy's name.</returns>
    public string CopyName(string name)
    {
        for (var attempt = 1; attempt <= CopyAttempts; attempt++)
        {
            var copy = attempt == 1 ? Localizer.Format("CopyOf", name) : Localizer.Format("CopyOfNumbered", name, attempt);

            if (Profiles?.IsTaken(copy, null) != true)
                return copy;
        }

        return name;
    }

    /// <summary>Keeps a profile saved, by its id.</summary>
    /// <param name="item">The profile.</param>
    public void Saved(ProfileViewModel item)
    {
        _byId[item.Id] = item;
        Sync(false);
        Toasts.Show(new Toast(Localizer.Format("ProfileSaved", item.Name), Tone.Success));
    }

    /// <summary>Drops a profile deleted.</summary>
    /// <param name="item">The profile.</param>
    public void Deleted(ProfileViewModel item)
    {
        _byId.Remove(item.Id);
        Items.Remove(item);
        EndEditing(item);
        Sync(false);
        Toasts.Show(new Toast(Localizer.Format("ProfileDeleted", item.Name)));
    }

    private string Describe(SettingDefinition setting, object settings)
    {
        var culture = Localizer.Culture;
        var value = setting.GetValue(settings);

        switch (setting.Kind)
        {
            case SettingKind.Flag:
                return value is true ? setting.Name.In(culture) : "";

            case SettingKind.Number:
                return setting.IsPercent ? Localizer.Format("PercentValue", (int)value!) : ((int)value!).ToString(culture);

            case SettingKind.Text:
                return (string)value!;

            case SettingKind.Choice:
                {
                    var names = setting.GetValues(settings).Select(choice => setting.Options.FirstOrDefault(option => option.Value.Equals(choice))?.Text.In(culture) ?? choice.ToString()!);

                    return Joined(setting.IsList ? [.. names] : [setting.Options.FirstOrDefault(option => option.Value.Equals(value))?.Text.In(culture) ?? ""]);
                }

            default:
                {
                    IReadOnlyList<object> ids = setting.IsList ? setting.GetValues(settings) : value is int id ? [id] : [];

                    return Joined([.. ids.Select(item => Names.Name(setting.Kind, Game?.Options(setting.Kind).FirstOrDefault(option => option.Id == (int)item) ?? new GameReference((int)item, 0, null)))]);
                }
        }
    }

    private string Joined(IReadOnlyList<string> names)
    {
        var shown = names.Where(name => name.Length > 0).Take(2).ToList();

        if (shown.Count == 0)
            return "";

        var rest = names.Count - shown.Count;

        return rest > 0 ? Localizer.Format("AndMore", string.Join(", ", shown), rest) : string.Join(", ", shown);
    }

    private void Sync(bool relabel)
    {
        var profiles = Profiles!.Profiles;
        var ids = profiles.Select(profile => profile.Id).ToHashSet();

        _profilesVersion = Profiles.Version;
        _accountsVersion = Accounts?.Version ?? 0;

        foreach (var gone in _byId.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            Items.Remove(_byId[gone]);
            _byId.Remove(gone);
        }

        foreach (var profile in profiles)
        {
            if (_byId.TryGetValue(profile.Id, out var item))
            {
                item.Update(profile);

                if (relabel)
                    item.Relabel();
            }
            else
            {
                _byId[profile.Id] = new ProfileViewModel(this, profile);
            }
        }

        if (relabel && Editing is { IsNew: true } editing)
            editing.Relabel();

        // A new profile shows in its editor alone until it is saved; the others follow the book's order, by name.
        List<ProfileViewModel> order = [.. profiles.Select(profile => _byId[profile.Id])];

        for (var index = 0; index < order.Count; index++)
        {
            var at = Items.IndexOf(order[index]);

            if (at < 0)
                Items.Insert(index, order[index]);
            else if (at != index)
                Items.Move(at, index);
        }

        IsEmpty = Items.Count == 0;
    }

    [RelayCommand]
    private void New()
    {
        Dialogs.Show(new NewProfileViewModel(this, null));
    }
}
