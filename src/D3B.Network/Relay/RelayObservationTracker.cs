using System.Diagnostics.CodeAnalysis;

namespace D3B.Network.Relay;

/// <summary>Tracks which admitted reads from either side have reached the connection interceptor.</summary>
[SuppressMessage("Design", "CA1001", Justification = "Late senders may still read this connection's cancelled token after its relay handler completes.")]
internal sealed class RelayObservationTracker
{
    private readonly object _gate = new();
    private readonly SortedSet<long> _observedOutOfOrder = [];
    private readonly CancellationTokenSource _failed = new();
    private TaskCompletionSource _changed = NewSignal();
    private long _admitted;
    private long _observed;
    private string? _failure;
    private Exception? _failureCause;
    private int _isFailed;

    /// <summary>Gets whether this connection can no longer guarantee observation.</summary>
    public bool IsFailed => Volatile.Read(ref _isFailed) is not 0;

    /// <summary>Gets a token cancelled when observation fails.</summary>
    public CancellationToken Failed => _failed.Token;

    /// <summary>Assigns one ticket to a non-empty pipe read.</summary>
    /// <returns>The ticket that must be observed or fail the tracker.</returns>
    public long Admit()
    {
        lock (_gate)
            return ++_admitted;
    }

    /// <summary>Marks a read observed, or closes the fence when its stream was no longer watched.</summary>
    /// <param name="ticket">The ticket returned by <see cref="Admit"/>.</param>
    /// <param name="watched">Whether every complete frame in the read was shown.</param>
    /// <param name="failureCause">The error that stopped observation, if any.</param>
    public void Observe(long ticket, bool watched, Exception? failureCause = null)
    {
        var cancelFailed = false;

        lock (_gate)
        {
            if (!watched)
            {
                cancelFailed = FailLocked("A relay stream stopped being observable as frames.", failureCause);
            }
            else if (_failure is not null)
                return;
            else if (ticket == _observed + 1)
            {
                _observed++;

                while (_observedOutOfOrder.Remove(_observed + 1))
                    _observed++;
            }
            else if (ticket > _observed + 1)
            {
                _observedOutOfOrder.Add(ticket);
            }

            if (watched)
                SignalLocked();
        }

        if (cancelFailed)
            CancelFailed();
    }

    /// <summary>Fails every current and future observation wait for this connection.</summary>
    /// <param name="reason">Why observation can no longer be guaranteed.</param>
    /// <param name="cause">The error that ended observation, if any.</param>
    public void Fail(string reason, Exception? cause = null)
    {
        bool cancelFailed;

        lock (_gate)
            cancelFailed = FailLocked(reason, cause);

        if (cancelFailed)
            CancelFailed();
    }

    /// <summary>Waits for the shared admission watermark captured before this call yields.</summary>
    /// <param name="cancellationToken">Stops this caller's wait without changing the connection fence.</param>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        long watermark;
        Task changed;

        lock (_gate)
        {
            watermark = _admitted;

            ThrowIfFailedLocked();

            if (_observed >= watermark)
                return;

            changed = _changed.Task;
        }

        while (true)
        {
            await changed.WaitAsync(cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                ThrowIfFailedLocked();

                if (_observed >= watermark)
                    return;

                changed = _changed.Task;
            }
        }
    }

    private static TaskCompletionSource NewSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private bool FailLocked(string reason, Exception? cause)
    {
        if (_failure is not null)
            return false;

        _failure = reason;
        _failureCause = cause;
        Volatile.Write(ref _isFailed, 1);
        SignalLocked();

        return true;
    }

    private void CancelFailed()
    {
        try
        {
            _failed.Cancel();
        }
        catch (AggregateException)
        {
            // Cancellation callbacks belong to waiters and cannot undo the sticky connection failure.
        }
    }

    private void SignalLocked()
    {
        _changed.TrySetResult();
        _changed = NewSignal();
    }

    private void ThrowIfFailedLocked()
    {
        if (_failure is null)
            return;

        if (_failureCause is null)
            throw new RelayInjectionException($"The observation wait failed: {_failure}");

        throw new RelayInjectionException($"The observation wait failed: {_failure}", _failureCause);
    }
}
