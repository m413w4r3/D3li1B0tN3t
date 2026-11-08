namespace D3B.Network.Relay;

/// <summary>A connection the relay took, as it shows it to its interceptor.</summary>
internal sealed class RelayConnection : IRelayConnection, IRelayAdmissionConnection, IRelayAdmissionModeController
{
    private readonly InjectedFrames _injected;
    private readonly RelayObservationTracker _observation;
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
        _injected.SetAdmissionMode(version, requiresTypedAdmission);
    }

    public Task SendToServerAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        return SendCoreAsync(frame, admission: null, cancellationToken);
    }

    Task IRelayAdmissionConnection.SendAdmittedAsync(ReadOnlyMemory<byte> frame, RelaySendAdmission admission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admission);
        var snapshot = frame.ToArray();

        if (!admission.IsBoundTo(snapshot))
            throw new RelayInjectionException("The frame did not go: its admission capability belongs to another frame.");

        return SendCoreAsync(snapshot, admission, cancellationToken);
    }

    internal Action? BeforeFinalAdmissionCheck
    {
        get => _injected.BeforeFinalAdmissionCheck;
        set => _injected.BeforeFinalAdmissionCheck = value;
    }

    internal Action? AfterFinalAdmissionCheck
    {
        get => _injected.AfterFinalAdmissionCheck;
        set => _injected.AfterFinalAdmissionCheck = value;
    }

    private async Task SendCoreAsync(ReadOnlyMemory<byte> frame, RelaySendAdmission? admission, CancellationToken cancellationToken)
    {

        if (_observation.IsFailed)
            throw new RelayInjectionException("The frame did not go: the connection's frames are no longer observable, so there are no frames safe to insert between.");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _observation.Failed);
        try
        {
            await _injected.SendAsync(frame, linked.Token, () => !_observation.IsFailed, admission).ConfigureAwait(false);
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

}
