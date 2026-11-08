namespace D3B.Network.Relay;

/// <summary>A connection the relay took, as it shows it to its interceptor.</summary>
internal sealed class RelayConnection : IRelayConnection, IRelayAdmissionModeController
{
    private readonly InjectedFrames _injected;
    private readonly RelayObservationTracker _observation;
    private AdmissionSnapshot _admission = new(0, false);

    public string Id { get; }

    public RelayRoute Route { get; }

    public CancellationToken Closed { get; }

    public RelayConnection(string id, RelayRoute route, InjectedFrames injected, RelayObservationTracker observation, CancellationToken closed)
    {
        Id = id;
        Route = route;
        Closed = closed;
        _injected = injected;
        _observation = observation;
    }

    void IRelayAdmissionModeController.SetAdmissionMode(long version, bool requiresTypedAdmission)
    {
        Volatile.Write(ref _admission, new AdmissionSnapshot(version, requiresTypedAdmission));
    }

    public Task SendToServerAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        return SendCoreAsync(frame, stillAdmitted: null, cancellationToken);
    }

    public Task SendToServerAsync(ReadOnlyMemory<byte> frame, Func<bool> stillAdmitted, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stillAdmitted);

        return SendCoreAsync(frame, stillAdmitted, cancellationToken);
    }

    private async Task SendCoreAsync(ReadOnlyMemory<byte> frame, Func<bool>? stillAdmitted, CancellationToken cancellationToken)
    {
        var admission = Volatile.Read(ref _admission);

        if (_observation.IsFailed)
            throw new RelayInjectionException("The frame did not go: the connection's frames are no longer observable, so there are no frames safe to insert between.");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _observation.Failed);
        var admissionCheck = new Func<bool>(() =>
        {
            var current = Volatile.Read(ref _admission);

            if (current.Version != admission.Version)
                return false;

            if (admission.RequiresTypedAdmission && stillAdmitted is null)
                return false;

            return stillAdmitted?.Invoke() ?? true;
        });

        try
        {
            await _injected.SendAsync(frame, linked.Token, () => !_observation.IsFailed, admissionCheck).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_observation.IsFailed && !cancellationToken.IsCancellationRequested)
        {
            throw new RelayInjectionException("The frame did not go: the connection's frames are no longer observable, so there are no frames safe to insert between.");
        }
    }

    public Task WaitForObservedFramesAsync(CancellationToken cancellationToken = default)
    {
        return _observation.WaitAsync(cancellationToken);
    }

    private sealed class AdmissionSnapshot
    {
        public long Version { get; }

        public bool RequiresTypedAdmission { get; }

        public AdmissionSnapshot(long version, bool requiresTypedAdmission)
        {
            Version = version;
            RequiresTypedAdmission = requiresTypedAdmission;
        }
    }
}
