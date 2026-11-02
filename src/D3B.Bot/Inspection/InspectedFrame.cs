using D3B.Protocol.Translation;

namespace D3B.Bot.Inspection;

/// <summary>A frame of a game connection as the protocol inspector shows it: when, who sent it, and what it carries (D115).</summary>
/// <param name="At">When it went: by the host's clock live, from the capture's start and offset in a replay.</param>
/// <param name="ConnectionId">The game connection's id, as the relay names it.</param>
/// <param name="Source">Who sent it.</param>
/// <param name="Frame">The frame, decoded through the installed build's translation.</param>
public sealed record InspectedFrame(DateTimeOffset At, string ConnectionId, FrameSource Source, DecodedGameFrame Frame);
