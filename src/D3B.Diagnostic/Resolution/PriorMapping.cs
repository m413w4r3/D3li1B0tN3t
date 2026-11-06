using D3B.Protocol.Mapping;

namespace D3B.Diagnostic.Resolution;

/// <summary>A build-pinned readable-to-obfuscated pair with optional semantic proof.</summary>
public sealed record PriorMapping
{
    /// <summary>Readable protobuf full name.</summary>
    public string ReadableFullName { get; init; } = string.Empty;

    /// <summary>Obfuscated schema token or full name.</summary>
    public string ObfuscatedToken { get; init; } = string.Empty;

    /// <summary>Build associated with the pin.</summary>
    public string Build { get; init; } = string.Empty;

    /// <summary>Whether the pin names one exact build.</summary>
    public bool BuildExact { get; init; }

    /// <summary>Optional semantic identity proof carried by this pin.</summary>
    public SemanticProof? SemanticProof { get; init; }

    /// <summary>Converts pins to corroborating prior mappings.</summary>
    public static IReadOnlyList<PriorMapping> From(PinnedPairs pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        return pins.Messages.Select(pin => new PriorMapping
        {
            ReadableFullName = pin.Readable,
            ObfuscatedToken = pin.Obfuscated,
            Build = pins.ClientVersion,
            BuildExact = true,
            SemanticProof = HasSemanticProof(pin)
                ? new SemanticProof
                {
                    Token = pin.Obfuscated,
                    Readable = pin.Readable,
                    Build = pins.ClientVersion,
                    SchemaSha256 = pin.SemanticProofSchemaSha256,
                    Kind = SemanticProof.ParseKind(pin.SemanticProofKind),
                    Source = pin.SemanticProofSource,
                    ArtifactSha256 = pin.SemanticProofArtifactSha256
                }
                : null
        }).ToArray();
    }

    private static bool HasSemanticProof(MessagePin pin)
    {
        return pin.HasSemanticProofKind
            || pin.HasSemanticProofSource
            || pin.HasSemanticProofSchemaSha256
            || pin.HasSemanticProofArtifactSha256;
    }
}
