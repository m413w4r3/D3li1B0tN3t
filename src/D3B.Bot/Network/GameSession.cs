using System.Buffers;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Network;

/// <summary>One game connection through D3B: its frames read through the build's translation, and D3B's requests sent to its server (D60, D62).</summary>
/// <remarks>
/// <para>
/// Every frame is decoded once (D58) and shown to the <see cref="IGameObserver"/> instances, then logged at the debug
/// level by what it is and its length, never by what it holds. No frame is held or rewritten: the client and the server
/// get every byte the other sent.
/// </para>
/// <para>
/// D3B acts through the client's connection with the requests that start an action, each with the id the client gives
/// them, <see cref="ProtocolTranslation.UncountedRequestId"/>. The server answers the client as it answers the client's
/// own requests, and the client does what follows as it does after its own: it confirms a movement once walked, and asks
/// what is on a map it is sent to. So D3B never sends those, and the client stays the one that shows what happens.
/// </para>
/// </remarks>
public sealed class GameSession : IConnectionInterceptor
{
    private readonly IRelayConnection _connection;
    private readonly ProtocolTranslation _translation;
    private readonly GameAccounts _accounts;
    private readonly IReadOnlyList<IGameObserver> _observers;
    private readonly ILogger _log;

    // Set from the client's read loop on its first request, read from the server's once the character is chosen.
    private volatile GameAccount? _account;

    /// <summary>Gets the game connection's id, as the relay's log names it.</summary>
    public string Id => _connection.Id;

    /// <summary>Gets a token cancelled once the connection is closed, both sides.</summary>
    public CancellationToken Closed => _connection.Closed;

    /// <summary>Gets the account the client logged in with, known once its first request presented the ticket the connection phase gave it.</summary>
    /// <remarks>
    /// <see langword="null"/> before that request, or when D3B did not see the connection phase identify the account
    /// (<see cref="GameAccounts"/>). Observers see that request before the server answers it, so they know the account
    /// before any event of the server.
    /// </remarks>
    public GameAccount? Account => _account;

    private GameSession(IRelayConnection connection, ProtocolTranslation translation, GameAccounts accounts, IReadOnlyList<IGameObserver> observers, ILogger log)
    {
        _connection = connection;
        _translation = translation;
        _accounts = accounts;
        _observers = observers;
        _log = log;
    }

    /// <summary>Starts reading a game connection, and shows it to the observers before any of its frames.</summary>
    internal static GameSession Start(IRelayConnection connection, ProtocolTranslation translation, GameAccounts accounts, IReadOnlyList<IGameObserver> observers, ILogger log)
    {
        var session = new GameSession(connection, translation, accounts, observers, log);

        foreach (var observer in observers)
        {
            try
            {
                observer.OnSessionStarted(session);
            }
            catch (Exception exception)
            {
                // An observer only watches: its failure must not cut the player off.
                GameLog.ObserverFailed(log, connection.Id, observer.GetType(), exception);
            }
        }

        return session;
    }

    /// <summary>Names a decoded frame by the message it carries, without what it holds.</summary>
    internal static string Describe(DecodedGameFrame frame)
    {
        if (frame.ReadableType is { } readable)
            return readable.Name;

        if (frame.CarriedType is { } carried)
            return "." + carried;

        return frame.Envelope is null ? "no game message" : frame.Envelope.ContentCase.ToString();
    }

    /// <summary>Sends the game server a request, between two of the client's frames.</summary>
    /// <param name="request">The readable request, whole: a message the frames carry, which the mapping pairs (D55).</param>
    /// <param name="cancellationToken">Stops waiting; a request not gone yet then never goes.</param>
    /// <returns>A task that completes once the request went to the server's side.</returns>
    /// <exception cref="ArgumentException">The request is no message the game's frames carry, the mapping lacks it, or it is partial.</exception>
    /// <exception cref="RelayInjectionException">The connection closed before the request went.</exception>
    public async Task SendAsync(IMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var frame = _translation.EncodeGameRequest(request);

        await _connection.SendToServerAsync(frame, cancellationToken);
        GameLog.Sent(_log, Id, request.Descriptor.Name, frame.Length);

        var decoded = _translation.DecodeGame(new ReadOnlySequence<byte>(frame));

        foreach (var observer in _observers)
        {
            try
            {
                observer.OnSent(this, decoded);
            }
            catch (Exception exception)
            {
                // An observer only watches: its failure must not fail a request that went.
                GameLog.ObserverFailed(_log, Id, observer.GetType(), exception);
            }
        }
    }

    bool IConnectionInterceptor.Holds(RelayDirection direction)
    {
        return false;
    }

    byte[]? IConnectionInterceptor.OnFrame(RelayDirection direction, ReadOnlySequence<byte> frame, bool held)
    {
        var decoded = _translation.DecodeGame(frame);

        if (_log.IsEnabled(LogLevel.Debug))
        {
            var side = direction is RelayDirection.FromClient ? "client" : "server";
            var state = decoded.IsUnderstood ? "understood" : decoded.IsRead ? "read" : "not read";
            var message = Describe(decoded);

            GameLog.Decoded(_log, Id, side, message, frame.Length, state);
        }

        if (direction is RelayDirection.FromClient && _account is null && decoded.Content is AuthenticationRequest authentication)
            _account = _accounts.Take(authentication.Ticket);

        foreach (var observer in _observers)
        {
            try
            {
                observer.OnFrame(this, direction, decoded);
            }
            catch (Exception exception)
            {
                // An observer only watches: its failure must neither cut the player off nor stop the others seeing frames.
                GameLog.ObserverFailed(_log, Id, observer.GetType(), exception);
            }
        }

        return null;
    }

    void IConnectionInterceptor.OnHoldAbandoned(RelayDirection direction, string reason)
    {
        // Nothing is held on the game connection.
    }
}
