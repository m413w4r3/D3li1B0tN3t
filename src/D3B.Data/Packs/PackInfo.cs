using MemoryPack;

namespace D3B.Data.Packs;

/// <summary>What a pack was made from: the client build, the exact files read, and the extractor.</summary>
[MemoryPackable]
public sealed partial class PackInfo
{
    /// <summary>Gets the version of the client build the pack was extracted from, such as <c>3.7.12.12</c>.</summary>
    public required string ClientVersion { get; init; }

    /// <summary>Gets when that client was built, when its version file says so.</summary>
    public DateTimeOffset? ClientBuildDate { get; init; }

    /// <summary>Gets the SHA-256, in hexadecimal, of the client files the extractor read and of their hashes.</summary>
    /// <remarks>Two extractions of one version with different fingerprints read different content (D24).</remarks>
    public required string Fingerprint { get; init; }

    /// <summary>Gets the version of <c>D3B.Data</c> the extractor was built with.</summary>
    public required string ExtractorVersion { get; init; }
}
