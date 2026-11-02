using System.Collections.Concurrent;
using D3B.Bot.Network;
using D3B.Network.Relay;
using D3B.Protocol.Translation;

namespace D3B.Bot.Tests.Network;

/// <summary>Keeps every game session and frame it is shown, does what a test asks on each frame, or fails on each when told to.</summary>
internal sealed class RecordingObserver : IGameObserver
{
    private readonly ConcurrentQueue<GameSession> _sessions = new();
    private readonly ConcurrentQueue<(RelayDirection Direction, DecodedGameFrame Frame)> _frames = new();

    public bool Fails { get; init; }

    /// <summary>Gets what it does with each frame, once kept, such as sending the server a request.</summary>
    public Action<GameSession, RelayDirection, DecodedGameFrame>? OnEachFrame { get; init; }

    public IReadOnlyList<GameSession> Sessions => [.. _sessions];

    public IReadOnlyList<(RelayDirection Direction, DecodedGameFrame Frame)> Frames => [.. _frames];

    public void OnSessionStarted(GameSession session)
    {
        if (Fails)
            throw new InvalidOperationException("The observer failed.");

        _sessions.Enqueue(session);
    }

    public void OnFrame(GameSession session, RelayDirection direction, DecodedGameFrame frame)
    {
        if (Fails)
            throw new InvalidOperationException("The observer failed.");

        _frames.Enqueue((direction, frame));
        OnEachFrame?.Invoke(session, direction, frame);
    }
}
