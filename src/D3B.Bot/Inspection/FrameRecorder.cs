using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using D3B.Bot.Characters;
using D3B.Bot.Journal;
using D3B.Bot.Network;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;

namespace D3B.Bot.Inspection;

/// <summary>Every bot's last game frames, both sides and D3B's own, kept for the protocol inspector (D113, D115).</summary>
/// <remarks>
/// <para>
/// An observer of the game (D62), recording always, so that the inspector shows what came before it was opened. A game
/// connection's frames are its character's once the server confirms the character the client chose, as the bots are tied
/// to theirs (D65); those that came before, the ticket's request and the character list among them, join them then. A
/// connection whose account D3B does not know is never a bot's, and its frames go with it.
/// </para>
/// <para>
/// Frames are decoded once, by the game session (D58): recording one keeps a reference, nothing more. The relay's read
/// loops call it, both sides of a connection at once: each connection's recording takes a lock of its own.
/// </para>
/// </remarks>
public sealed class FrameRecorder : IGameObserver
{
    /// <summary>The number of frames kept for each bot, across its connections: older ones are dropped (D113).</summary>
    public const int Capacity = 3_000;

    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<CharacterKey, RecentItems<InspectedFrame>> _bots = new();
    private readonly ConcurrentDictionary<GameSession, Recording> _sessions = new();

    /// <summary>Initializes a new instance of the <see cref="FrameRecorder"/> class.</summary>
    /// <param name="time">The clock frames are dated by.</param>
    public FrameRecorder(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        _time = time;
    }

    /// <summary>Finds a bot's frames.</summary>
    /// <param name="key">The bot's character.</param>
    /// <param name="frames">Its frames, oldest first, once a game connection chose it.</param>
    /// <returns>Whether a game connection chose the character.</returns>
    public bool TryGet(CharacterKey key, [NotNullWhen(true)] out RecentItems<InspectedFrame>? frames)
    {
        return _bots.TryGetValue(key, out frames);
    }

    void IGameObserver.OnSessionStarted(GameSession session)
    {
        _sessions[session] = new Recording();

        // Runs at once when the connection already closed.
        session.Closed.Register(() => _sessions.TryRemove(session, out _));
    }

    void IGameObserver.OnFrame(GameSession session, RelayDirection direction, DecodedGameFrame frame)
    {
        if (!_sessions.TryGetValue(session, out var recording))
            return;

        var source = direction is RelayDirection.FromClient ? FrameSource.Client : FrameSource.Server;
        var chosen = direction is RelayDirection.FromServer && frame.Content is CharacterSelectionEvent { Success.Character: { } character } && session.Account is { } account
            ? new CharacterKey(account.Id, character.Id)
            : (CharacterKey?)null;

        lock (recording.Gate)
        {
            recording.Frames.Add(new InspectedFrame(_time.GetUtcNow(), session.Id, source, frame));

            if (chosen is { } key && !recording.IsBound)
                Bind(recording, key);
        }
    }

    void IGameObserver.OnSent(GameSession session, DecodedGameFrame frame)
    {
        if (!_sessions.TryGetValue(session, out var recording))
            return;

        lock (recording.Gate)
            recording.Frames.Add(new InspectedFrame(_time.GetUtcNow(), session.Id, FrameSource.D3B, frame));
    }

    private void Bind(Recording recording, CharacterKey key)
    {
        var frames = _bots.GetOrAdd(key, _ => new RecentItems<InspectedFrame>(Capacity));

        foreach (var frame in recording.Frames.All())
            frames.Add(frame);

        recording.Frames = frames;
        recording.IsBound = true;
    }

    private sealed class Recording
    {
        public Lock Gate { get; } = new();

        // Its connection's frames until they are a bot's, then the bot's own.
        public RecentItems<InspectedFrame> Frames { get; set; } = new(Capacity);

        public bool IsBound { get; set; }
    }
}
