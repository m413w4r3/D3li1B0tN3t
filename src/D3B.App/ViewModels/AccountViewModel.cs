using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.Storage.Accounts;

namespace D3B.App.ViewModels;

/// <summary>An account's card: who, its subscription, when D3B last saw it, and its characters (D99, D101).</summary>
internal sealed partial class AccountViewModel : ObservableObject
{
    private readonly AccountsViewModel _page;

    /// <summary>Gets the account, as recorded.</summary>
    public KnownAccount Account { get; }

    /// <summary>Gets the account's nickname.</summary>
    public string Nickname { get; }

    /// <summary>Gets its tag, after a hash.</summary>
    public string Tag { get; }

    /// <summary>Gets its characters, the one played last first.</summary>
    public IReadOnlyList<CharacterRowViewModel> Characters { get; }

    /// <summary>Gets until when it is subscribed, or that it is not.</summary>
    [ObservableProperty]
    public partial string Subscription { get; private set; } = "";

    /// <summary>Gets the tone of its subscription.</summary>
    [ObservableProperty]
    public partial Tone SubscriptionTone { get; private set; }

    /// <summary>Gets whether a bot of its plays now, or when D3B last saw it.</summary>
    [ObservableProperty]
    public partial string Seen { get; private set; } = "";

    /// <summary>Initializes a new instance of the <see cref="AccountViewModel"/> class.</summary>
    /// <param name="page">The accounts page.</param>
    /// <param name="account">The account, as recorded.</param>
    /// <param name="characters">Its characters.</param>
    public AccountViewModel(AccountsViewModel page, KnownAccount account, IReadOnlyList<CharacterRowViewModel> characters)
    {
        _page = page;
        Account = account;
        Nickname = account.Nickname.Length > 0 ? account.Nickname : page.Localizer.Format("AccountNamed", account.Id);
        Tag = account.Tag.Length > 0 ? "#" + account.Tag : "";
        Characters = characters;
    }

    /// <summary>Says again what changes with time: its subscription, when it was seen, its characters' bots.</summary>
    /// <param name="isConnected">Tells whether a character's bot has a game connection now.</param>
    /// <param name="now">The time now.</param>
    public void Refresh(Func<KnownCharacter, bool> isConnected, DateTimeOffset now)
    {
        var localizer = _page.Localizer;
        var connected = false;

        foreach (var character in Characters)
        {
            var playing = isConnected(character.Character);

            character.Refresh(playing, now);
            connected |= playing;
        }

        (Subscription, SubscriptionTone) = Account.SubscriptionEnd is { } end && end > now
            ? (localizer.Format("SubscribedUntil", end.ToLocalTime().DateTime), Tone.Success)
            : (localizer["NotSubscribed"], Tone.Neutral);
        Seen = connected ? localizer["Connected"] : Ago.Of(localizer, "Seen", Account.LastSeen, now);
    }

    [RelayCommand]
    private void AskForget()
    {
        var localizer = _page.Localizer;

        _page.Dialogs.Ask(new QuestionViewModel(_page.Dialogs, localizer.Format("ForgetTitle", Nickname), localizer["ForgetDetail"], localizer["ForgetConfirm"], localizer["Cancel"], () => ForgetCommand.Execute(null), destructive: true));
    }

    [RelayCommand]
    private void Forget()
    {
        _ = _page.WriteAsync(books => books.ForgetAsync(Account.Id, CancellationToken.None));
    }
}
