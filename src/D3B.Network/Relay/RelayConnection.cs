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

    public Task SendToServerAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        return _injected.SendAsync(frame, cancellationToken);
    }

    public Task WaitForObservedFramesAsync(CancellationToken cancellationToken = default)
    {
        return _observation.WaitAsync(cancellationToken);
    }
}
