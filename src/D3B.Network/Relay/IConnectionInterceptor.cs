using System.Buffers;

namespace D3B.Network.Relay;

/// <summary>Watches the frames of one relayed connection, both sides, and rewrites those it has the relay hold (D49).</summary>
/// <remarks>
/// <para>
/// Each side calls it from its own read loop, at once with the other: what one side's frames tell another side's must be
/// safe across threads. A frame a side sends is shown before its bytes are flushed to the other side, so that what it asks
/// of the other side's next frame holds before that side can answer it.
/// </para>
/// <para>
/// It must be quick and must not keep a frame, whose bytes are reused once the call returns.
/// </para>
/// </remarks>
public interface IConnectionInterceptor
{
    /// <summary>Tells whether the relay should hold the next frame a side sends until it is whole, so that it can be rewritten.</summary>
    /// <param name="direction">The side.</param>
    /// <returns><see langword="true"/> to hold it.</returns>
    bool Holds(RelayDirection direction);

    /// <summary>Sees a whole frame.</summary>
    /// <param name="direction">Which side sent it.</param>
    /// <param name="frame">The frame's bytes, its varint length already read.</param>
    /// <param name="held">
    /// Whether the relay held it, nothing of it forwarded yet: the frame may then be rewritten. Otherwise it is already on
    /// its way, as it came.
    /// </param>
    /// <returns>For a frame held, the bytes to forward in its place, without their length, or null to forward it as it came.</returns>
    byte[]? OnFrame(RelayDirection direction, ReadOnlySequence<byte> frame, bool held);

    /// <summary>Learns that the relay forwarded a frame it held before it was whole, and holds nothing more on that side.</summary>
    /// <param name="direction">The side.</param>
    /// <param name="reason">Why, such as the frame not coming whole in time.</param>
    void OnHoldAbandoned(RelayDirection direction, string reason);
}
