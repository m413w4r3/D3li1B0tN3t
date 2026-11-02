using D3B.Bot.Characters;
using D3B.Bot.Network;
using Microsoft.EntityFrameworkCore;

namespace D3B.Storage.Accounts;

/// <summary>The accounts and characters D3B saw, and the profile each character plays, kept in the database and in memory (D99).</summary>
/// <remarks>
/// <para>
/// An account is never added by hand: it comes the first time its client lists its characters through D3B, and may be
/// forgotten. The app reads what is in memory, every refresh, without the database; each write goes to the database
/// first, then reads it all again, a few rows, and counts a new version.
/// </para>
/// <para>Writes go one at a time, whether the app's or what the relay saw.</para>
/// </remarks>
public sealed class AccountBook : IDisposable
{
    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly SemaphoreSlim _writing = new(1, 1);
    private volatile Snapshot _snapshot = new([], [], 0);

    /// <summary>Gets the accounts, the one seen last first.</summary>
    public IReadOnlyList<KnownAccount> Accounts => _snapshot.Accounts;

    /// <summary>Gets the characters, by account, the one played last first.</summary>
    public IReadOnlyList<KnownCharacter> Characters => _snapshot.Characters;

    /// <summary>Gets a number that changes each time the accounts or characters change, for a view to tell it must read them again.</summary>
    public int Version => _snapshot.Version;

    /// <summary>Initializes a new instance of the <see cref="AccountBook"/> class.</summary>
    /// <param name="contexts">Makes the database's contexts.</param>
    public AccountBook(IDbContextFactory<StorageContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        _contexts = contexts;
    }

    private static async Task SeeAccountAsync(StorageContext context, GameAccount account, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var known = await context.Accounts.FindAsync([account.Id], cancellationToken).ConfigureAwait(false);

        if (known is null)
        {
            context.Accounts.Add(new KnownAccount
            {
                Id = account.Id,
                Nickname = account.Nickname,
                Tag = account.Tag,
                SubscriptionEnd = account.SubscriptionEnd,
                FirstSeen = now,
                LastSeen = now,
            });
        }
        else
        {
            context.Entry(known).CurrentValues.SetValues(new { account.Nickname, account.Tag, account.SubscriptionEnd, LastSeen = now });
        }
    }

    /// <summary>Finds a character.</summary>
    /// <param name="key">The character.</param>
    /// <returns>The character, or <see langword="null"/> if D3B never saw it.</returns>
    public KnownCharacter? Find(CharacterKey key)
    {
        return Characters.FirstOrDefault(character => character.Key == key);
    }

    /// <summary>Records an account and the characters its client listed.</summary>
    /// <param name="account">The account, as its identification named it.</param>
    /// <param name="characters">The characters the server listed: their id, name and level.</param>
    /// <param name="seen">When the relay saw the list.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once recorded.</returns>
    internal Task SeeAsync(GameAccount account, IReadOnlyList<(long Id, string Name, int? Level)> characters, DateTimeOffset seen, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(characters);

        return WriteAsync(
            async context =>
            {
                await SeeAccountAsync(context, account, seen, cancellationToken).ConfigureAwait(false);

                foreach (var (id, name, level) in characters)
                {
                    var known = await context.Characters.FindAsync([account.Id, id], cancellationToken).ConfigureAwait(false);

                    if (known is null)
                        context.Characters.Add(new KnownCharacter { AccountId = account.Id, Id = id, Name = name, Level = level });
                    else
                        context.Entry(known).CurrentValues.SetValues(new { Name = name, Level = level ?? known.Level });
                }
            },
            cancellationToken);
    }

    /// <summary>Records that an account's client chose a character.</summary>
    /// <param name="account">The account.</param>
    /// <param name="id">The character's id.</param>
    /// <param name="name">Its name.</param>
    /// <param name="level">Its level, if the server said.</param>
    /// <param name="played">When the relay saw the server confirm the choice.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once recorded.</returns>
    internal Task PlayAsync(GameAccount account, long id, string name, int? level, DateTimeOffset played, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(name);

        return WriteAsync(
            async context =>
            {
                await SeeAccountAsync(context, account, played, cancellationToken).ConfigureAwait(false);

                var known = await context.Characters.FindAsync([account.Id, id], cancellationToken).ConfigureAwait(false);

                if (known is null)
                    context.Characters.Add(new KnownCharacter { AccountId = account.Id, Id = id, Name = name, Level = level, LastPlayed = played });
                else
                    context.Entry(known).CurrentValues.SetValues(new { Name = name, Level = level ?? known.Level, LastPlayed = (DateTimeOffset?)played });
            },
            cancellationToken);
    }

    /// <summary>Gives a character a profile, or none.</summary>
    /// <param name="key">The character.</param>
    /// <param name="profileId">The profile's id, or <see langword="null"/> for none, which no longer starts on connection.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once saved.</returns>
    public Task AssignAsync(CharacterKey key, int? profileId, CancellationToken cancellationToken)
    {
        return WriteAsync(
            context => context.Characters
                .Where(character => character.AccountId == key.AccountId && character.Id == key.CharacterId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(character => character.ProfileId, profileId)
                        .SetProperty(character => character.StartsOnConnection, character => profileId != null && character.StartsOnConnection),
                    cancellationToken),
            cancellationToken);
    }

    /// <summary>Says whether a character's profile starts once its client first connects while the host runs.</summary>
    /// <param name="key">The character.</param>
    /// <param name="starts">Whether it starts; a character with no profile never does.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once saved.</returns>
    public Task SetStartsOnConnectionAsync(CharacterKey key, bool starts, CancellationToken cancellationToken)
    {
        return WriteAsync(
            context => context.Characters
                .Where(character => character.AccountId == key.AccountId && character.Id == key.CharacterId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(character => character.StartsOnConnection, character => starts && character.ProfileId != null), cancellationToken),
            cancellationToken);
    }

    /// <summary>Forgets an account and its characters, until its client lists them again.</summary>
    /// <param name="accountId">The account's id.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once forgotten.</returns>
    public Task ForgetAsync(long accountId, CancellationToken cancellationToken)
    {
        return WriteAsync(context => context.Accounts.Where(account => account.Id == accountId).ExecuteDeleteAsync(cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _writing.Dispose();
    }

    /// <summary>Reads the accounts and characters from the database again.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>A task that completes once read.</returns>
    internal Task LoadAsync(CancellationToken cancellationToken)
    {
        return WriteAsync(_ => Task.CompletedTask, cancellationToken);
    }

    private async Task WriteAsync(Func<StorageContext, Task> write, CancellationToken cancellationToken)
    {
        await _writing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            await using (context.ConfigureAwait(false))
            {
                await write(context).ConfigureAwait(false);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                var accounts = await context.Accounts.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
                var characters = await context.Characters.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
                var seen = accounts.ToDictionary(account => account.Id, account => account.LastSeen);

                // SQLite cannot order by a date with an offset: the few rows are ordered here.
                _snapshot = new Snapshot(
                    [.. accounts.OrderByDescending(account => account.LastSeen).ThenBy(account => account.Nickname, StringComparer.CurrentCultureIgnoreCase)],
                    [.. characters
                        .OrderByDescending(character => seen.GetValueOrDefault(character.AccountId))
                        .ThenBy(character => character.AccountId)
                        .ThenByDescending(character => character.LastPlayed)
                        .ThenBy(character => character.Name, StringComparer.CurrentCultureIgnoreCase)],
                    _snapshot.Version + 1);
            }
        }
        finally
        {
            _writing.Release();
        }
    }

    private sealed record Snapshot(IReadOnlyList<KnownAccount> Accounts, IReadOnlyList<KnownCharacter> Characters, int Version);
}
