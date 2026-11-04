using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using D3B.Bot.Runtime;
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
[SuppressMessage("Design", "CA1001", Justification = "The signal lives as long as the game session and holds no timer or handle; the session is collected with its connection.")]
public sealed class GameSession : IConnectionInterceptor
{
    private readonly IRelayConnection _connection;
    private readonly ProtocolTranslation _translation;
    private readonly GameAccounts _accounts;
    private readonly IReadOnlyList<IGameObserver> _observers;
    private readonly ILogger _log;
    private readonly ConcurrentDictionary<TranslationProblemKey, int> _translationProblems = new();
    // Movement stays blocked for this session because unread wire content makes its position unsafe to trust.
    private RelayInjectionException? _translationFailure;
    private readonly CancellationTokenSource _translationFailureSignal = new();
    private Func<IMessage, bool>? _sendGate;
    private Func<Task>? _sendBarrier;

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

    /// <summary>Gets whether a frame this session could not read latched movement closed until the session ends.</summary>
    internal bool IsMovementTranslationBlocked => Volatile.Read(ref _translationFailure) is not null;

    internal Func<IMessage, bool>? SendGate
    {
        get
        {
            return Volatile.Read(ref _sendGate);
        }
        set
        {
            Volatile.Write(ref _sendGate, value);
        }
    }

    internal Func<Task>? SendBarrier
    {
        get
        {
            return Volatile.Read(ref _sendBarrier);
        }
        set
        {
            Volatile.Write(ref _sendBarrier, value);
        }
    }

    internal async Task WaitForObservedFramesAsync(CancellationToken cancellationToken)
    {
        ThrowIfTranslationFailed();
        await _connection.WaitForObservedFramesAsync(cancellationToken).ConfigureAwait(false);
        ThrowIfTranslationFailed();
    }

    private GameSession(IRelayConnection connection, ProtocolTranslation translation, GameAccounts accounts, IReadOnlyList<IGameObserver> observers, ILogger log)
    {
        _connection = connection;
        _translation = translation;
        _accounts = accounts;
        _observers = observers;
        _log = log;
        SendGate = request => !MovementRequests.IsMovementCapable(request);
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
    /// <exception cref="InvalidOperationException">The request is not allowed from the current bot state.</exception>
    /// <exception cref="RelayInjectionException">The connection closed before the request went.</exception>
    public async Task SendAsync(IMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (MovementRequests.IsMovementCapable(request))
        {
            await WaitForObservedFramesAsync(cancellationToken).ConfigureAwait(false);

            if (SendBarrier is { } barrier)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await barrier().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (SendGate is { } gate && !gate(request))
            throw new InvalidOperationException("The request is not allowed from the current bot state.");

        await SendUnguardedAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal async Task SendUnguardedAsync(IMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var movement = MovementRequests.IsMovementCapable(request);

        // Guarded sends bypass SendAsync, so recheck after their inbox barrier and guard.
        if (movement)
            ThrowIfTranslationFailed();

        var frame = _translation.EncodeGameRequest(request);

        if (!movement)
        {
            await _connection.SendToServerAsync(frame, cancellationToken);
        }
        else
        {
            // The relay can queue the frame behind the client's injection gate after the check above. Link its token to
            // the latch so a frame still queued when an unread frame arrives is withdrawn instead of written.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _translationFailureSignal.Token);

            try
            {
                await _connection.SendToServerAsync(frame, linked.Token);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && IsMovementTranslationBlocked)
            {
                throw new RelayInjectionException("The frame did not go: the session's movement translation latch closed.", exception);
            }
        }

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
        if (!decoded.IsRead && !decoded.IsUnderstood)
        {
            var failure = new RelayInjectionException("The frame could not be read as a game message; movement is not authorized for this session.");

            if (Interlocked.CompareExchange(ref _translationFailure, failure, null) is null)
                _translationFailureSignal.Cancel();
        }

        var side = direction is RelayDirection.FromClient ? "client" : "server";
        var message = Describe(decoded);

        if (_log.IsEnabled(LogLevel.Debug))
        {
            var state = decoded.IsUnderstood ? "understood" : decoded.IsRead ? "read" : "not read";

            GameLog.Decoded(_log, Id, side, message, frame.Length, state);
        }

        if (decoded.Problem is not null)
            LogTranslationProblem(side, message, decoded);

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

    private void LogTranslationProblem(string side, string message, DecodedGameFrame decoded)
    {
        var category = GameLog.ProblemCategory(decoded);
        var key = new TranslationProblemKey(side, message, category);
        var count = _translationProblems.AddOrUpdate(key, 1, (_, previous) => previous + 1);

        if (count is 1)
        {
            if (decoded.IsRead)
                GameLog.Partial(_log, Id, side, message, category);
            else
                GameLog.NotUnderstood(_log, Id, side, message, category);

            return;
        }

        if (decoded.IsRead)
            GameLog.RepeatedPartial(_log, Id, side, message, count, category);
        else
            GameLog.RepeatedUnread(_log, Id, side, message, count, category);
    }

    private void ThrowIfTranslationFailed()
    {
        if (Volatile.Read(ref _translationFailure) is { } failure)
            throw new RelayInjectionException(failure.Message, failure);
    }

    void IConnectionInterceptor.OnHoldAbandoned(RelayDirection direction, string reason)
    {
        // Nothing is held on the game connection.
    }

    private readonly record struct TranslationProblemKey(string Side, string Message, string Category);
}
