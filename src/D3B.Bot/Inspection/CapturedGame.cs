using D3B.Network.Captures;

namespace D3B.Bot.Inspection;

/// <summary>A capture of a game connection, read again for the protocol inspector (D63, D115).</summary>
/// <param name="Header">What the capture says of its connection.</param>
/// <param name="Frames">Its frames, both sides and D3B's own, in the order they went.</param>
/// <param name="IsTruncated">Whether its last record was cut short, as when the host writing it was killed.</param>
public sealed record CapturedGame(CaptureHeader Header, IReadOnlyList<InspectedFrame> Frames, bool IsTruncated);
