namespace D3B.Scripts.Capture;

/// <summary>A capture's stable file identity and connection header.</summary>
public sealed class CaptureDiagnosticFile
{
    /// <summary>Gets the basename, without its containing directory.</summary>
    public required string Basename { get; init; }

    /// <summary>Gets the lowercase SHA-256 of the file.</summary>
    public string? Sha256 { get; init; }

    /// <summary>Gets the file size in bytes.</summary>
    public long? Size { get; init; }

    /// <summary>Gets the capture format version when the header contains one.</summary>
    public int? FormatVersion { get; init; }

    /// <summary>Gets when the captured connection started, when the header is valid.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Gets the listener address and port, when the header is valid.</summary>
    public string? Listener { get; init; }

    /// <summary>Gets the server host requested by the client, when the header is valid.</summary>
    public string? ServerHost { get; init; }

    /// <summary>Gets the server address and port, when the header is valid.</summary>
    public string? Server { get; init; }
}
