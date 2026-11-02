using System.Collections.Concurrent;
using D3B.Network.Relay;

namespace D3B.Bot.Tests.Network;

/// <summary>A connection as the relay shows it, keeping the frames sent to its server, or failing them once closed.</summary>
internal sealed class FakeRelayConnection : IRelayConnection, IDisposable
{
    private readonly CancellationTokenSource _closed = new();
    private readonly ConcurrentQueue<byte[]> _sent = new();

    public string Id { get; }

    public RelayRoute Route { get; }

    public CancellationToken Closed => _closed.Token;

    public IReadOnlyList<byte[]> Sent => [.. _sent];

    public FakeRelayConnection(string id, RelayRoute route)
    {
        Id = id;
        Route = route;
    }

    public Task SendToServerAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (_closed.IsCancellationRequested)
            return Task.FromException(new RelayInjectionException("The frame did not go: the client's side closed."));

        _sent.Enqueue(frame.ToArray());

        return Task.CompletedTask;
    }

    public void Close()
    {
        _closed.Cancel();
    }

    public void Dispose()
    {
        _closed.Dispose();
    }
}
