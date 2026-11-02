using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Controls;
using D3B.Storage.Accounts;

namespace D3B.App.ViewModels;

/// <summary>A character on its account's card: who, when it last played, the profile it plays and whether it starts on connection (D99).</summary>
internal sealed partial class CharacterRowViewModel : ObservableObject
{
    private readonly AccountsViewModel _page;
    private bool _reading;

    /// <summary>Gets the character, as recorded.</summary>
    public KnownCharacter Character { get; }

    /// <summary>Gets the character's name.</summary>
    public string Name { get; }

    /// <summary>Gets the initials of its name, for its avatar.</summary>
    public string Initials { get; }

    /// <summary>Gets the profiles it may play, none first.</summary>
    public IReadOnlyList<ProfileOptionViewModel> Profiles { get; }

    /// <summary>Gets its level, and whether its bot plays it now, or when it last played.</summary>
    [ObservableProperty]
    public partial string Detail { get; private set; } = "";

    /// <summary>Gets the tone of its avatar's dot: whether its bot plays it now.</summary>
    [ObservableProperty]
    public partial Tone Tone { get; private set; }

    /// <summary>Gets or sets the profile it plays.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfile))]
    public partial ProfileOptionViewModel? SelectedProfile { get; set; }

    /// <summary>Gets or sets whether its profile starts once its client first connects while D3B runs.</summary>
    [ObservableProperty]
    public partial bool StartsOnConnection { get; set; }

    /// <summary>Gets whether it plays a profile, which may start on connection.</summary>
    public bool HasProfile => SelectedProfile?.Id is not null;

    /// <summary>Initializes a new instance of the <see cref="CharacterRowViewModel"/> class.</summary>
    /// <param name="page">The accounts page.</param>
    /// <param name="character">The character, as recorded.</param>
    /// <param name="profiles">The profiles it may play, none first.</param>
    public CharacterRowViewModel(AccountsViewModel page, KnownCharacter character, IReadOnlyList<ProfileOptionViewModel> profiles)
    {
        _page = page;
        Character = character;
        Name = character.Name.Length > 0 ? character.Name : page.Localizer.Format("CharacterNamed", character.Id);
        Initials = AvatarInitials.Of(Name);
        Profiles = profiles;
        _reading = true;
        SelectedProfile = profiles.FirstOrDefault(profile => profile.Id == character.ProfileId) ?? profiles[0];
        StartsOnConnection = character.StartsOnConnection;
        _reading = false;
    }

    /// <summary>Says again whether its bot plays it, and when it last played.</summary>
    /// <param name="isConnected">Whether its bot has a game connection now.</param>
    /// <param name="now">The time now.</param>
    public void Refresh(bool isConnected, DateTimeOffset now)
    {
        var localizer = _page.Localizer;
        var played = isConnected ? localizer["ConnectedNow"]
            : Character.LastPlayed is { } last ? Ago.Of(localizer, "Played", last, now)
            : localizer["PlayedNever"];

        Detail = Character.Level is { } level ? localizer.Format("CharacterDetail", localizer.Format("Level", level), played) : played;
        Tone = isConnected ? Tone.Success : Tone.Neutral;
    }

    partial void OnSelectedProfileChanged(ProfileOptionViewModel? value)
    {
        if (!_reading && value is not null)
            _ = _page.WriteAsync(books => books.AssignAsync(Character.Key, value.Id, CancellationToken.None));
    }

    partial void OnStartsOnConnectionChanged(bool value)
    {
        if (!_reading)
            _ = _page.WriteAsync(books => books.SetStartsOnConnectionAsync(Character.Key, value, CancellationToken.None));
    }
}
