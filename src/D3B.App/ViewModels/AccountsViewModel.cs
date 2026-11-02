using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Localization;
using D3B.Bot.Runtime;
using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>The accounts page: a card per account D3B saw, its characters in rows, each with its profile (D99, D101).</summary>
/// <remarks>
/// It reads the accounts from memory each refresh, and makes its cards again only once the accounts or the profiles
/// changed; what changes with time, the bots' connections and how long ago, is said again each refresh.
/// </remarks>
internal sealed partial class AccountsViewModel : ObservableObject
{
    private IServiceProvider? _services;
    private AccountBook? _accounts;
    private ProfileBook? _profiles;
    private Bots? _bots;
    private TimeProvider _time = TimeProvider.System;
    private int _accountsVersion = -1;
    private int _profilesVersion = -1;
    private CultureInfo? _culture;

    /// <summary>Gets the accounts, the one seen last first.</summary>
    public ObservableCollection<AccountViewModel> Items { get; } = [];

    /// <summary>Gets the app's texts.</summary>
    public Localizer Localizer { get; }

    /// <summary>Gets the window's dialogs.</summary>
    public Dialogs Dialogs { get; }

    /// <summary>Gets whether D3B saw no account yet.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets whether the host keeps accounts: its database opened.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    /// <summary>Gets why a change was not saved, if it was not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Gets whether a change was not saved.</summary>
    public bool HasError => Error is not null;

    /// <summary>Initializes a new instance of the <see cref="AccountsViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="dialogs">The window's dialogs, which ask before an account is forgotten.</param>
    public AccountsViewModel(Localizer localizer, Dialogs dialogs)
    {
        Localizer = localizer;
        Dialogs = dialogs;
    }

    /// <summary>Reads the accounts again, in the language chosen.</summary>
    /// <param name="services">The host's services, once it runs.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _services is null)
        {
            _services = services;
            _accounts = services.GetService<AccountBook>();
            _profiles = services.GetService<ProfileBook>();
            _bots = services.GetService<Bots>();
            _time = services.GetService<TimeProvider>() ?? TimeProvider.System;
            IsAvailable = _accounts is not null;
        }

        if (_accounts is null)
            return;

        if (!Localizer.Culture.Equals(_culture) || _accounts.Version != _accountsVersion || (_profiles?.Version ?? 0) != _profilesVersion)
            Rebuild();

        var now = _time.GetUtcNow();

        foreach (var item in Items)
            item.Refresh(IsConnected, now);
    }

    /// <summary>Saves a change, off the UI's thread's way; the page shows it once the book read it back.</summary>
    /// <param name="write">The change.</param>
    /// <returns>A task that completes once saved, or once the failure is shown.</returns>
    public async Task WriteAsync(Func<AccountBook, Task> write)
    {
        if (_accounts is null)
            return;

        try
        {
            await write(_accounts).ConfigureAwait(true);
            Error = null;
        }
        catch (Exception exception) when (exception is DbUpdateException or SqliteException or InvalidOperationException)
        {
            // The app holds every client's connection (D91): a write that fails says so, and the bots go on.
            Error = Localizer.Format("SaveFailed", exception.GetBaseException().Message);
        }
    }

    private bool IsConnected(KnownCharacter character)
    {
        return _bots is not null && _bots.TryGet(character.Key, out var bot) && bot.IsConnected;
    }

    private void Rebuild()
    {
        var accounts = _accounts!;

        _culture = Localizer.Culture;
        _accountsVersion = accounts.Version;
        _profilesVersion = _profiles?.Version ?? 0;

        List<ProfileOptionViewModel> profiles = [new(null, Localizer["NoProfile"]), .. (_profiles?.Profiles ?? []).Select(profile => new ProfileOptionViewModel(profile.Id, profile.Name))];

        Items.Clear();

        foreach (var account in accounts.Accounts)
        {
            var characters = accounts.Characters
                .Where(character => character.AccountId == account.Id)
                .Select(character => new CharacterRowViewModel(this, character, profiles))
                .ToList();

            Items.Add(new AccountViewModel(this, account, characters));
        }

        IsEmpty = Items.Count == 0;
    }
}
