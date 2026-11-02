using D3B.Bot.Characters;
using D3B.Network.Relay;
using Google.Protobuf;

namespace D3B.Bot.Runtime;

/// <summary>A message a behavior waits for, such as the server's answer to a request it is about to send.</summary>
/// <remarks>
/// A behavior expects the answer before it sends the request: the bot's loop then sees the expectation before any frame
/// the request may bring, since both go through its inbox in order (D9). The loop tries each message on it once the
/// state took the message in, so the state the match sees already holds it. An expectation ends with its behavior.
/// </remarks>
public sealed class GameExpectation
{
    private readonly Func<RelayDirection, IMessage, CharacterState, bool> _match;
    private readonly TimeProvider _time;
    private readonly TaskCompletionSource<IMessage> _seen = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal RunningBehavior Owner { get; }

    internal bool IsDone => _seen.Task.IsCompleted;

    internal GameExpectation(Func<RelayDirection, IMessage, CharacterState, bool> match, RunningBehavior owner, TimeProvider time)
    {
        _match = match;
        Owner = owner;
        _time = time;
    }

    /// <summary>Waits for the message, at most a delay.</summary>
    /// <param name="timeout">How long to wait, by the bot's clock.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The first message that matched, or <see langword="null"/> when the delay passed first.</returns>
    /// <exception cref="OperationCanceledException">The behavior was interrupted, or the token cancelled.</exception>
    public async Task<IMessage?> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return await _seen.Task.WaitAsync(timeout, _time, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    internal void TryMatch(RelayDirection side, IMessage message, CharacterState state)
    {
        if (_seen.Task.IsCompleted)
            return;

        try
        {
            if (_match(side, message, state))
                _seen.TrySetResult(message);
        }
        catch (Exception exception)
        {
            // A match that throws is the behavior's fault: the behavior sees it, the bot's loop goes on.
            _seen.TrySetException(exception);
        }
    }

    internal void Cancel()
    {
        _seen.TrySetCanceled();
    }
}
