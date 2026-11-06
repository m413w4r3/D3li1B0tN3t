namespace D3B.Diagnostic.Resolution;

/// <summary>A semantic identity assertion bound to one exact build schema pairing.</summary>
public sealed record SemanticProof
{
    /// <summary>Obfuscated token identified by the proof.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>Readable protobuf full name identified by the proof.</summary>
    public string Readable { get; init; } = string.Empty;

    /// <summary>Client build identified by the proof.</summary>
    public string Build { get; init; } = string.Empty;

    /// <summary>Lowercase SHA-256 of the exact build schema.</summary>
    public string SchemaSha256 { get; init; } = string.Empty;

    /// <summary>Audited method used to establish identity.</summary>
    public SemanticProofKind Kind { get; init; }

    /// <summary>Named source of the proof.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Lowercase SHA-256 of the audited artifact supporting the proof.</summary>
    public string ArtifactSha256 { get; init; } = string.Empty;

    /// <summary>Whether every proof binding matches the supplied candidate and both digests are valid.</summary>
    public bool IsValidFor(string token, string readable, string build, string schemaSha256)
    {
        return Enum.IsDefined(Kind)
            && Kind != SemanticProofKind.Unknown
            && !string.IsNullOrWhiteSpace(Token)
            && !string.IsNullOrWhiteSpace(Readable)
            && !string.IsNullOrWhiteSpace(Build)
            && !string.IsNullOrWhiteSpace(Source)
            && string.Equals(Token, token, StringComparison.Ordinal)
            && string.Equals(Readable, readable, StringComparison.Ordinal)
            && string.Equals(Build, build, StringComparison.Ordinal)
            && string.Equals(SchemaSha256, schemaSha256, StringComparison.Ordinal)
            && IsSha256(SchemaSha256)
            && IsSha256(ArtifactSha256);
    }

    internal static SemanticProofKind ParseKind(string? kind)
    {
        return kind switch
        {
            "IL2CPP" => SemanticProofKind.Il2Cpp,
            "BEHAVIORAL" => SemanticProofKind.Behavioral,
            "AUDITED_PIN" => SemanticProofKind.AuditedPin,
            "AUDITED_REFERENCE" => SemanticProofKind.AuditedReference,
            _ => SemanticProofKind.Unknown
        };
    }

    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 }
            && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}
