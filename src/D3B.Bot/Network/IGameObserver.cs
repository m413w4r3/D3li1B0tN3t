using D3B.Network.Relay;
using D3B.Protocol.Translation;

namespace D3B.Bot.Network;

/// <summary>Sees every game connection D3B takes and each of its frames, decoded (D62).</summary>
/// <remarks>
/// A module registers one in the services; the network module shows it every game session. It is called from the relay's
/// read loops, both sides of a connection at once and many connections at once: it must be quick and safe across threads,
/// such as by posting what it sees to a bot's inbox (D9). A request it sends from there goes once the read loop is done
/// with the frame, so it never waits there for it. What it throws is logged and goes no further.
/// </remarks>
public interface IGameObserver
{
    /// <summary>Sees a game connection D3B took, before any of its frames; <see cref="GameSession.Closed"/> says when it ends.</summary>
    /// <param name="session">The session, through which D3B sends the server its requests.</param>
    void OnSessionStarted(GameSession session);

    /// <summary>Sees a frame of a game connection, decoded through the build's translation (D58).</summary>
    /// <param name="session">The session.</param>
    /// <param name="direction">Which side sent it: the client's requests, the server's events and responses.</param>
    /// <param name="frame">The frame, decoded: its message, when the readable protocol reads it.</param>
    void OnFrame(GameSession session, RelayDirection direction, DecodedGameFrame frame);

    /// <summary>Sees a request D3B sent the server itself, once it went, decoded as the server reads it (D115).</summary>
    /// <remarks>
    /// Apart from <see cref="OnFrame"/>, which never shows D3B's own requests as the client's (D63): the bots learn what
    /// they did from the server's answers. By default, an observer does nothing with them; the protocol inspector shows
    /// them. Called from the sender's thread.
    /// </remarks>
    /// <param name="session">The session.</param>
    /// <param name="frame">The request's frame, decoded.</param>
    void OnSent(GameSession session, DecodedGameFrame frame)
    {
    }
}
