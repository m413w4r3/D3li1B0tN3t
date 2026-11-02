using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Bot.Activities;
using D3B.Bot.Settings;
using D3B.Data;
using D3B.Data.Monsters;
using D3B.Data.Texts;
using D3B.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Settings;

/// <summary>Edits an activity's settings apart: a field per setting, by section, each checked as the user types (D98).</summary>
/// <remarks>
/// The fields write into a copy of the settings, which the profile saves once they hold. Each change checks them all
/// again: what is wrong with each, which show, the rules the activity declared, and what a setting sought within another
/// offers.
/// </remarks>
internal sealed partial class SettingsEditor : ObservableObject
{
    private readonly List<SettingFieldViewModel> _fields = [];
    private SettingsPreview? _shownPreview;
    private CultureInfo? _shownCulture;

    /// <summary>Gets the settings the activity declares.</summary>
    public ActivitySettings Settings { get; }

    /// <summary>Gets the settings being edited, a copy, or <see langword="null"/> for an activity with none.</summary>
    public object? Draft { get; }

    /// <summary>Gets the game's values the settings name, or <see langword="null"/> when the host has none.</summary>
    public GameReferences? Game { get; }

    /// <summary>Gets the app's texts.</summary>
    public Localizer Localizer { get; }

    /// <summary>Gets the settings, by section.</summary>
    public IReadOnlyList<SettingSectionViewModel> Sections { get; }

    /// <summary>Gets whether the activity declares no setting.</summary>
    public bool IsEmpty => Settings.IsEmpty;

    /// <summary>Gets what the activity's rules say, those that do not hold, a line each.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRuleProblems))]
    public partial string Rules { get; private set; } = "";

    /// <summary>Gets whether something is wrong with the settings.</summary>
    [ObservableProperty]
    public partial bool HasProblems { get; private set; }

    /// <summary>Gets whether a rule of the activity's does not hold.</summary>
    public bool HasRuleProblems => Rules.Length > 0;

    /// <summary>Gets what the settings lead to, as the activity reckons it from the pack, if it says (D127).</summary>
    public SettingsPreview? Preview { get; private set; }

    /// <summary>Gets the zone the settings lead to, as its mini-map draws it, beside the settings (D159).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasZone), nameof(HasPreview))]
    public partial ZoneScene? Zone { get; private set; }

    /// <summary>Gets whether the zone holds isolated maps, which its legend then names (D149).</summary>
    [ObservableProperty]
    public partial bool HasIsolated { get; private set; }

    /// <summary>Gets whether the zone tells probable or empty maps apart, as a harvest's does, which its legend then names; a hunt's does not (D245).</summary>
    [ObservableProperty]
    public partial bool HasForecast { get; private set; }

    /// <summary>Gets what the zone's place says while no zone is drawn: the activity's word, or that the settings lack what and where (D246).</summary>
    [ObservableProperty]
    public partial string NoZoneText { get; private set; } = "";

    /// <summary>Gets the figures the activity gives of its settings.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFigures), nameof(HasPreview))]
    public partial IReadOnlyList<FigureViewModel> Figures { get; private set; } = [];

    /// <summary>Gets whether a zone is drawn.</summary>
    public bool HasZone => Zone is not null;

    /// <summary>Gets whether the activity gives figures.</summary>
    public bool HasFigures => Figures.Count > 0;

    /// <summary>Gets whether the activity says what its settings lead to.</summary>
    public bool HasPreview => HasZone || HasFigures;

    /// <summary>Gets whether a character plays the profile.</summary>
    public bool HasPlayers => Players.Count > 0;

    /// <summary>Gets the characters that play the profile, each with what the settings lead to for it, if the activity says (D166).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PlayerNoteViewModel> PlayerNotes { get; private set; } = [];

    /// <summary>Gets the window's dialogs, which a setting may open, such as choosing resources, if the editor shows in a window.</summary>
    public Dialogs? Dialogs { get; }

    /// <summary>Gets the characters that play the profile, with their professions' levels, as their bots last saw them.</summary>
    public IReadOnlyList<PreviewPlayer> Players { get; }

    /// <summary>Gets what says the activity's texts in the app's language, if the host runs.</summary>
    public GameTextFormatter? Says { get; }

    /// <summary>Gets the pack's atlas, which places the zone's maps, if the host runs.</summary>
    public WorldAtlas? Atlas { get; }

    private GameNames Names { get; }

    private GameIcons? Icons { get; }

    private IServiceProvider? Services { get; }

    /// <summary>Occurs once a setting changed and the settings were checked again.</summary>
    public event EventHandler? Edited;

    /// <summary>Initializes a new instance of the <see cref="SettingsEditor"/> class.</summary>
    /// <param name="settings">The settings the activity declares.</param>
    /// <param name="draft">The settings to edit, which the editor writes into.</param>
    /// <param name="game">The game's values the settings name, or <see langword="null"/>.</param>
    /// <param name="names">Names those values.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="services">The host's services, which the activity reckons what its settings lead to from, if it runs.</param>
    /// <param name="players">The characters that play the profile, with their professions' levels.</param>
    /// <param name="dialogs">The window's dialogs, or <see langword="null"/>.</param>
    public SettingsEditor(ActivitySettings settings, object? draft, GameReferences? game, GameNames names, Localizer localizer, IServiceProvider? services = null, IReadOnlyList<PreviewPlayer>? players = null, Dialogs? dialogs = null)
    {
        Dialogs = dialogs;
        Settings = settings;
        Draft = draft;
        Game = game;
        Names = names;
        Localizer = localizer;
        Services = services;
        Players = players ?? [];
        Atlas = services?.GetService<GameWorld>()?.Atlas;
        Icons = GameIcons.Of(services?.GetService<GameTables>());
        Says = services is null ? null : new GameTextFormatter(localizer, services.GetService<GameWorld>(), services.GetService<GameTexts>(), services.GetService<GameTables>());

        var sections = new List<SettingSectionViewModel>();
        var fields = new List<SettingFieldViewModel>();
        var title = settings.All.Count > 0 ? settings.All[0].Section : null;

        foreach (var setting in settings.All)
        {
            if (setting.Section != title && fields.Count > 0)
            {
                sections.Add(new SettingSectionViewModel(title, fields, this));
                fields = [];
            }

            title = setting.Section;

            var field = Create(setting);

            _fields.Add(field);
            fields.Add(field);
        }

        if (fields.Count > 0)
            sections.Add(new SettingSectionViewModel(title, fields, this));

        Sections = sections;
        Changed();
    }

    /// <summary>Checks the settings again, once a field changed.</summary>
    public void Changed()
    {
        if (Draft is null)
            return;

        var problems = Settings.Check(Draft, Game);
        var culture = Localizer.Culture;

        // Before the fields, which show what it says.
        Preview = Services is null ? null : Settings.Preview(Draft, Services, Players);

        foreach (var field in _fields)
            field.Update(problems.FirstOrDefault(problem => problem.Setting == field.Definition));

        foreach (var section in Sections)
            section.Update(this);

        ShowPreview();
        Rules = string.Join(Environment.NewLine, problems.Where(problem => problem.Setting is null).Select(problem => problem.Message?.In(culture)));
        HasProblems = problems.Count > 0 || _fields.Any(field => field.IsVisible && field.HasInputProblem);
        Edited?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Names everything again, in the app's language.</summary>
    public void Relabel()
    {
        foreach (var field in _fields)
            field.Relabel();

        Changed();
    }

    /// <summary>Writes the settings, to store them.</summary>
    /// <returns>The settings, as JSON.</returns>
    public string Write()
    {
        return Settings.Write(Draft);
    }

    /// <summary>Says what is wrong with a setting, in the app's language.</summary>
    /// <param name="problem">What is wrong.</param>
    /// <returns>The text.</returns>
    public string ProblemText(SettingProblem problem)
    {
        var culture = Localizer.Culture;
        var setting = problem.Setting;

        return problem.Kind switch
        {
            SettingProblemKind.Required => Localizer["SettingRequired"],
            SettingProblemKind.OutOfRange => Localizer.Format("SettingOutOfRange", setting?.Minimum, setting?.Maximum),
            SettingProblemKind.TooLong => Localizer.Format("SettingTooLong", setting?.MaximumLength),
            SettingProblemKind.Unknown => Localizer["SettingUnknown"],
            SettingProblemKind.Repeated => Localizer["SettingRepeated"],
            SettingProblemKind.NotWithin => Localizer.Format("SettingNotWithin", setting?.Within?.Name.In(culture)),
            _ => problem.Message?.In(culture) ?? "",
        };
    }

    /// <summary>Gets the value of the setting another is sought within.</summary>
    /// <param name="setting">The setting sought within another.</param>
    /// <returns>The other's value, or <see langword="null"/> when it has none, or the setting is sought within nothing.</returns>
    public int? WithinOf(SettingDefinition setting)
    {
        return Draft is not null && setting.Within?.GetValue(Draft) is int within ? within : null;
    }

    /// <summary>Gets the value of a setting another's suggestions follow, such as the resources whose sub-areas a sub-area setting offers first.</summary>
    /// <param name="setting">The setting suggested by another.</param>
    /// <returns>The other's values, those it shows; every resource the game has when it is hidden, as when the activity harvests everything; none when the setting is suggested by nothing.</returns>
    public IReadOnlyList<int> SuggestionsOf(SettingDefinition setting)
    {
        return Draft is null || Game is null ? [] : setting.Suggestions(Draft, Game);
    }

    /// <summary>Says who gathers a resource, beside its name: its profession and level, or for one of <c>Base</c>'s where the learned graphics place it (D150).</summary>
    /// <param name="resource">The resource's item.</param>
    /// <returns>Its profession and level, or where it was seen; <see langword="null"/> when the game names no such resource.</returns>
    public string? SourceOf(int resource)
    {
        if (Game is null)
            return null;

        if (Game.TryGetSource(resource, out var source))
            return Localizer.Format("ResourceSource", Names.Name(SettingKind.Job, JobOf(source.JobId)), source.Level);

        if (!Game.IsBase(resource))
            return null;

        return Game.PlacesOf(resource) is { Count: > 0 } places ? Localizer.Format("ResourceBasePlaced", places.Count) : Localizer["ResourceBaseUnseen"];
    }

    /// <summary>Says which character playing the profile lacks a profession's level for a resource, if one does.</summary>
    /// <param name="resource">The resource's item.</param>
    /// <returns>The first such character, the level asked and its own; <see langword="null"/> when every one has it, or none was seen.</returns>
    public string? MissingLevelOf(int resource)
    {
        if (Game is null || !Game.TryGetSource(resource, out var source))
            return null;

        foreach (var player in Players)
        {
            // A profession a bot has not seen counts as the first level, as the activity counts it.
            var level = player.JobLevels.TryGetValue(source.JobId, out var known) && known > 0 ? known : 1;

            if (player.JobLevels.Count > 0 && level < source.Level)
                return Localizer.Format("ResourceMissingLevel", Names.Name(SettingKind.Job, JobOf(source.JobId)), source.Level, player.Name, level);
        }

        return null;
    }

    /// <summary>Names a value of the game, in the app's language.</summary>
    /// <param name="kind">Its kind.</param>
    /// <param name="reference">The value.</param>
    /// <returns>Its name.</returns>
    public string NameOf(SettingKind kind, GameReference reference)
    {
        return Names.Name(kind, reference);
    }

    /// <summary>Gets the values of the game a setting offers, named in the app's language.</summary>
    /// <param name="setting">A setting of the game's values.</param>
    /// <param name="within">The value of the setting it is sought within, if any.</param>
    /// <returns>A profession's resources by the level it gathers them from; every other list by name.</returns>
    public IReadOnlyList<OptionViewModel> OptionsOf(SettingDefinition setting, int? within)
    {
        if (Game is null)
            return [];

        // An item setting may offer some types alone, such as food's (D148).
        var references = setting.Kind is SettingKind.Item && setting.ItemTypes is { } types ? Game.ItemsOf(types) : Game.Options(setting.Kind, within);
        var options = references.Select(reference => Option(setting.Kind, reference));

        if (setting.Kind is SettingKind.Resource && within is not null)
            return [.. options];

        var compare = StringComparer.Create(Localizer.Culture, CompareOptions.IgnoreCase);

        return [.. options.OrderBy(option => option.Name, compare)];
    }

    /// <summary>Names a value of the game a setting holds, whether the game offers it or not.</summary>
    /// <param name="setting">The setting.</param>
    /// <param name="id">The value's id.</param>
    /// <returns>It, named.</returns>
    public OptionViewModel OptionOf(SettingDefinition setting, int id)
    {
        var reference = Game?.Options(setting.Kind).FirstOrDefault(option => option.Id == id) ?? new GameReference(id, 0, null);

        return Option(setting.Kind, reference);
    }

    private OptionViewModel Option(SettingKind kind, GameReference reference)
    {
        // A resource shows its icon, and its profession's beside the detail naming it; a profession its own; a monster its
        // picture, larger. Each is drawn once first shown, on the UI's thread as the icons ask.
        var id = reference.Id;
        var icons = Icons;
        Func<IImage?>? icon = kind switch
        {
            SettingKind.Resource or SettingKind.Item => () => icons?.Item(id),
            SettingKind.Job => () => icons?.Job(id),
            SettingKind.Monster => () => icons?.Monster(id),
            _ => null,
        };
        var detailIcon = kind is SettingKind.Resource && Game is not null && Game.TryGetSource(id, out var source) ? icons?.Job(source.JobId) : null;

        return new OptionViewModel(id, Names.Name(kind, reference), DetailOf(kind, reference), icon, detailIcon)
        {
            IconSize = kind is SettingKind.Monster ? OptionViewModel.PortraitSize : OptionViewModel.ItemIconSize,
        };
    }

    /// <summary>Names a profession, with its icon, in the app's language.</summary>
    /// <param name="jobId">The profession.</param>
    /// <returns>It, named.</returns>
    public OptionViewModel JobOption(int jobId)
    {
        return Option(SettingKind.Job, JobOf(jobId));
    }

    /// <summary>Gets the group a value of a list is filtered by, and its level: a resource's profession or a monster's super race.</summary>
    /// <param name="kind">The list's kind.</param>
    /// <param name="id">The value.</param>
    /// <returns>Its group, or <see langword="null"/> for a resource of <c>Base</c>'s or a value of none, and its level, if it has one.</returns>
    public (int? Group, int? Level) GroupOf(SettingKind kind, int id)
    {
        if (Game is null)
            return (null, null);

        return kind switch
        {
            SettingKind.Resource when Game.TryGetSource(id, out var source) => (source.JobId, source.Level),
            SettingKind.Monster when Game.TryGetSuperRace(id, out var superRace) => (superRace is 0 ? null : superRace, ReferenceOf(kind, id).Level),
            _ => (null, null),
        };
    }

    /// <summary>Names a group a list is filtered by, with its icon: a profession or a super race.</summary>
    /// <param name="kind">The list's kind.</param>
    /// <param name="groupId">The group.</param>
    /// <returns>It, named.</returns>
    public OptionViewModel GroupOption(SettingKind kind, int groupId)
    {
        var groups = kind switch
        {
            SettingKind.Monster => Game?.SuperRaces,
            _ => null,
        };

        if (groups is null)
            return JobOption(groupId);

        var group = groups.FirstOrDefault(reference => reference.Id == groupId);

        return new OptionViewModel(groupId, Names.Name(kind, group.NameId == 0 ? new GameReference(groupId, 0, null) : group), null, null, null);
    }

    private void ShowPreview()
    {
        // Reckoned on every check of the settings: drawn again only when the activity reckoned something new.
        if (ReferenceEquals(Preview, _shownPreview) && Localizer.Culture.Equals(_shownCulture))
            return;

        _shownPreview = Preview;
        _shownCulture = Localizer.Culture;
        Zone = Preview is { Zone.Length: > 0 } preview && Atlas is { } atlas
            ? ZoneScenes.Of(preview.Zone, atlas, tour: false, preview.LandmarkMapId, Players.FirstOrDefault(player => player.MapId is not null)?.MapId)
            : null;
        HasIsolated = Preview is not null && Preview.Zone.Any(map => map.State is ZoneMapState.Isolated);
        HasForecast = Preview is not null && Preview.Zone.Any(map => map.State is ZoneMapState.Probable or ZoneMapState.Empty);
        Figures = Preview is null || Says is not { } says ? [] : [.. Preview.Figures.Select(figure => new FigureViewModel(says.Say(figure.Value), says.Say(figure.Label)))];
        NoZoneText = Preview?.Message is { } message && Says is { } saying ? saying.Say(message) : Localizer["ZoneNothingYet"];
        PlayerNotes = [.. Players.Select(player => new PlayerNoteViewModel(player.Name, NoteOf(player)))];
    }

    private string? NoteOf(PreviewPlayer player)
    {
        return Preview?.PlayerNotes.FirstOrDefault(note => note.Name == player.Name) is { } note && Says is { } says ? says.Say(note.Note) : null;
    }

    private GameReference ReferenceOf(SettingKind kind, int id)
    {
        return Game?.Options(kind).FirstOrDefault(option => option.Id == id) ?? new GameReference(id, 0, null);
    }

    private GameReference JobOf(int jobId)
    {
        return Game?.Options(SettingKind.Job).FirstOrDefault(job => job.Id == jobId) ?? new GameReference(jobId, 0, null);
    }

    private string? DetailOf(SettingKind kind, GameReference reference)
    {
        // A resource of any profession says whose it is, its level beside; a family of monsters where they come from.
        return kind switch
        {
            SettingKind.Resource => SourceOf(reference.Id) ?? Names.Detail(reference),
            SettingKind.MonsterFamily when Game is not null && Game.TryGetSuperRaceOfFamily(reference.Id, out var superRace) && superRace is not MonsterRace.NoSuperRace => GroupOption(SettingKind.Monster, superRace).Name,
            _ => Names.Detail(reference),
        };
    }

    private SettingFieldViewModel Create(SettingDefinition setting)
    {
        return (setting.Kind, setting.IsList) switch
        {
            (SettingKind.Flag, _) => new FlagFieldViewModel(setting, this),
            (SettingKind.Number, _) => new NumberFieldViewModel(setting, this),
            (SettingKind.Text, _) => new TextFieldViewModel(setting, this),
            (SettingKind.Choice, false) => new ChoiceFieldViewModel(setting, this),
            (SettingKind.Choice, true) => new ChoicesFieldViewModel(setting, this),
            (SettingKind.Resource or SettingKind.Monster, true) => new PickedGamesFieldViewModel(setting, this),
            (SettingKind.Job, true) when setting.IsGathering => new GatheringJobsFieldViewModel(setting, this),
            (SettingKind.SubArea, true) when setting.IsSuggested => new SubAreasFieldViewModel(setting, this),
            (_, false) => new GameFieldViewModel(setting, this),
            _ => new GamesFieldViewModel(setting, this),
        };
    }
}
