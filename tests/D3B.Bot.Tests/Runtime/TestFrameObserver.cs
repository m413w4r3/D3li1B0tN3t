using System.Collections.Concurrent;
using D3B.Bot.Network;
using D3B.Network.Relay;
using D3B.Protocol.Translation;

namespace D3B.Bot.Tests.Runtime;

/// <summary>Signals after earlier observers have seen a sent frame.</summary>
internal sealed class TestFrameObserver : IGameObserver
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _sent = new(StringComparer.Ordinal);

    public Task SentAsync(string name)
    {
        return _sent.GetOrAdd(name, static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
    }

    public void OnSessionStarted(GameSession session)
    {
    }

    public void OnFrame(GameSession session, RelayDirection direction, DecodedGameFrame frame)
    {
    }

    public void OnSent(GameSession session, DecodedGameFrame frame)
    {
        if (frame.ReadableType is { } type)
            _sent.GetOrAdd(type.Name, static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    }
}
