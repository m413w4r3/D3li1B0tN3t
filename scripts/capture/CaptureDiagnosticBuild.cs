namespace D3B.Scripts.Capture;

/// <summary>Build identifiers associated with a capture.</summary>
public sealed class CaptureDiagnosticBuild
{
    /// <summary>Gets the captured client's version, or null when the header is unavailable.</summary>
    public string? ClientVersion { get; init; }

    /// <summary>Gets the schema version associated with the capture, or null until schema analysis is added.</summary>
    public string? SchemaVersion { get; init; }

    /// <summary>Gets the mapping version associated with the capture, or null until mapping analysis is added.</summary>
    public string? MappingVersion { get; init; }
}
