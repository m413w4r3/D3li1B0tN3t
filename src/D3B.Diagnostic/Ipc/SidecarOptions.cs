namespace D3B.Diagnostic.Ipc;

/// <summary>Configuration for one diagnostic sidecar session.</summary>
public sealed record SidecarOptions
{
    /// <summary>Path to the diagnostic engine executable.</summary>
    public string EnginePath { get; init; } = string.Empty;

    /// <summary>Private directory in which the Unix socket is created.</summary>
    public string SocketDirectory { get; init; } = string.Empty;

    /// <summary>Maximum number of observations buffered by the caller.</summary>
    public int QueueCapacity { get; init; } = 256;

    /// <summary>Maximum time to establish the socket connection.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum time for the protocol handshake.</summary>
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum time for each frame exchange and shutdown response.</summary>
    public TimeSpan ResponseTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
