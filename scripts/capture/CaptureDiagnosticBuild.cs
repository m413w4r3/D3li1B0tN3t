namespace D3B.Scripts.Capture;

/// <summary>Build identifiers associated with a capture.</summary>
public sealed class CaptureDiagnosticBuild
{
    /// <summary>Gets the captured client's version, or null when the header is unavailable.</summary>
    public string? ClientVersion { get; init; }

    /// <summary>Gets the format version of the supplied schema, or null when no schema was supplied or it was invalid.</summary>
    public string? SchemaVersion { get; init; }

    /// <summary>Gets the SHA-256 of the supplied schema file, or null when no schema was supplied or it was invalid.</summary>
    public string? SchemaSha256 { get; init; }

    /// <summary>Gets the format version of the supplied mapping, or null when no mapping was supplied or it was invalid.</summary>
    public string? MappingVersion { get; init; }

    /// <summary>Gets the SHA-256 of the supplied mapping file, or null when no mapping was supplied or it was invalid.</summary>
    public string? MappingSha256 { get; init; }

    /// <summary>Gets whether the mapping names the captured client version, or null when it cannot be compared.</summary>
    public bool? MappingClientVersionCompatible { get; init; }
}
