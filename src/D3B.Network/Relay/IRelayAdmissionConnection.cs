namespace D3B.Network.Relay;

/// <summary>Sends frames with a session-issued admission capability.</summary>
internal interface IRelayAdmissionConnection
{
    /// <summary>Sends a frame whose admission is bound to its bytes and originating game session version.</summary>
    /// <param name="frame">The frame's bytes, without their length.</param>
    /// <param name="admission">The capability issued for these bytes by the originating game session.</param>
    /// <param name="cancellationToken">Stops waiting; a frame not gone yet then never goes.</param>
    /// <returns>A task that completes once the frame went to the server's side.</returns>
    /// <exception cref="RelayInjectionException">The capability did not match, was no longer admitted, or the connection closed before the frame went.</exception>
    Task SendAdmittedAsync(ReadOnlyMemory<byte> frame, RelaySendAdmission admission, CancellationToken cancellationToken = default);
}
