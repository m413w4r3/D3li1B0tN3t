namespace D3B.Network.Relay;

/// <summary>A connection the relay took, as its interceptor sees it: which it is, and how D3B sends its server frames of its own (D61).</summary>
public interface IRelayConnection
{
    /// <summary>Gets the connection's id, as the relay's log names it.</summary>
    string Id { get; }

    /// <summary>Gets which of the client's connections it is.</summary>
    RelayRoute Route { get; }

    /// <summary>Gets a token cancelled once the relay is done with the connection, both sides closed.</summary>
    CancellationToken Closed { get; }

    /// <summary>Waits until every relay read admitted before this call has been shown to the connection interceptor.</summary>
    /// <remarks>
    /// The cutoff covers non-empty reads returned by either relay pump before this call's snapshot. A complete frame in
    /// those reads has been shown once the wait completes. Bytes still in a socket, transport, or unread pipe are outside
    /// the cutoff, and a partial frame is not covered until a later read completes it.
    /// </remarks>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes once the admitted reads were observed.</returns>
    /// <exception cref="RelayInjectionException">The connection ended or its stream could not be observed as frames.</exception>
    Task WaitForObservedFramesAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends the server a frame of D3B's own, between two of the client's frames.</summary>
    /// <remarks>
    /// The frame goes at once when what went to the server ends on one of the client's frames, else as soon as the client's
    /// frame in progress has gone whole: the server reads every frame whole, D3B's among the client's. It is recorded in the
    /// connection's capture. Frames sent one after another go in that order.
    /// </remarks>
    /// <param name="frame">The frame's bytes, without their length.</param>
    /// <param name="cancellationToken">Stops waiting; a frame not gone yet then never goes.</param>
    /// <returns>A task that completes once the frame went to the server's side.</returns>
    /// <exception cref="RelayInjectionException">The connection closed before the frame went, or the client's bytes are no frames.</exception>
    Task SendToServerAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default);
}
