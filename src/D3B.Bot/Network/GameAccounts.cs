using D3B.Network.Relay;

namespace D3B.Bot.Network;

/// <summary>The accounts whose server selection D3B rewrote, each waiting for its game connection by the ticket it presents.</summary>
/// <remarks>
/// <para>
/// The connection phase names the account in the identification's answer, then gives the client a ticket in the server
/// selection's; the client presents that ticket in the first request of its game connection (D52). So the account of a
/// game connection is the one whose ticket its first request holds: the connection phase remembers it here, and the game
/// session takes it.
/// </para>
/// <para>
/// An account is taken once and forgotten after <see cref="RelaySettings.SelectionLifetime"/>, as the selection it goes
/// with. The ticket lets a client into the game server: it is kept in memory only, never logged.
/// </para>
/// </remarks>
public sealed class GameAccounts
{
    private readonly TimeSpan _lifetime;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (GameAccount Account, DateTimeOffset At)> _waiting = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="GameAccounts"/> class.</summary>
    /// <param name="lifetime">How long an account waits for its game connection.</param>
    /// <param name="time">The clock accounts are timed by.</param>
    public GameAccounts(TimeSpan lifetime, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        _lifetime = lifetime;
        _time = time;
    }

    /// <summary>Remembers the account a server selection's ticket belongs to.</summary>
    /// <param name="ticket">The ticket the selection's answer gives the client.</param>
    /// <param name="account">The account the connection phase identified.</param>
    internal void Remember(string ticket, GameAccount account)
    {
        var now = _time.GetUtcNow();

        lock (_lock)
        {
            Forget(now);
            _waiting[ticket] = (account, now);
        }
    }

    /// <summary>Takes the account whose ticket a game connection presents.</summary>
    /// <param name="ticket">The ticket the game connection's first request holds.</param>
    /// <returns>The account, or <see langword="null"/> when none waits for the ticket.</returns>
    internal GameAccount? Take(string ticket)
    {
        var now = _time.GetUtcNow();

        lock (_lock)
        {
            Forget(now);

            return _waiting.Remove(ticket, out var waiting) ? waiting.Account : null;
        }
    }

    private void Forget(DateTimeOffset now)
    {
        foreach (var ticket in _waiting.Where(pair => now - pair.Value.At > _lifetime).Select(pair => pair.Key).ToList())
            _waiting.Remove(ticket);
    }
}
