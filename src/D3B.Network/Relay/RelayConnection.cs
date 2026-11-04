namespace D3B.Network.Relay;

/// <summary>A connection the relay took, as it shows it to its interceptor.</summary>
internal sealed class RelayConnection : IRelayConnection
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

    public async Task SendToServerAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (_observation.IsFailed)
            throw new RelayInjectionException("The frame did not go: the connection's frames are no longer observable, so there are no frames safe to insert between.");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _observation.Failed);

        try
        {
            await _injected.SendAsync(frame, linked.Token, () => !_observation.IsFailed).ConfigureAwait(false);
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
