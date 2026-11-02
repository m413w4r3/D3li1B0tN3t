namespace D3B.Network.Relay;

/// <summary>A connection the relay took, as it shows it to its interceptor.</summary>
internal sealed class RelayConnection : IRelayConnection
{
    private readonly InjectedFrames _injected;

    public string Id { get; }

    public RelayRoute Route { get; }

    public CancellationToken Closed { get; }

    public RelayConnection(string id, RelayRoute route, InjectedFrames injected, CancellationToken closed)
    {
        Id = id;
        Route = route;
        Closed = closed;
        _injected = injected;
    }

    public Task SendToServerAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        return _injected.SendAsync(frame, cancellationToken);
    }
}
