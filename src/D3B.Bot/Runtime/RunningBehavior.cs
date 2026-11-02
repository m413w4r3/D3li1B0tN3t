using D3B.Bot.Network;

namespace D3B.Bot.Runtime;

/// <summary>The behavior a bot runs: the game connection it acts through, and how it is cancelled.</summary>
internal sealed class RunningBehavior
{
    private const int NotInterrupted = -1;

    private readonly CancellationTokenSource _cancellation;
    private Task? _cancelling;
    private int _reason = NotInterrupted;

    public GameSession Session { get; }

    public CancellationToken Token { get; }

    /// <summary>Gets why the behavior was interrupted, or <see langword="null"/> while it was not.</summary>
    public Interruption? Reason => Volatile.Read(ref _reason) is var reason and not NotInterrupted ? (Interruption)reason : null;

    public RunningBehavior(GameSession session, CancellationToken cancellationToken)
    {
        Session = session;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Token = _cancellation.Token;
    }

    /// <summary>Interrupts the behavior, from the bot's loop: the first reason stays.</summary>
    public void Interrupt(Interruption reason)
    {
        if (Reason is not null)
            return;

        // Written before the cancellation, which the behavior observes before it reads the reason.
        Volatile.Write(ref _reason, (int)reason);

        // The behavior's code runs where its token's callbacks run: never on the bot's loop.
        _cancelling = _cancellation.CancelAsync();
    }

    /// <summary>Lets the cancellation go, from the bot's loop, once the behavior is over.</summary>
    public void Release()
    {
        if (_cancelling is null or { IsCompleted: true })
            _cancellation.Dispose();
        else
            _cancelling.ContinueWith(_ => _cancellation.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
