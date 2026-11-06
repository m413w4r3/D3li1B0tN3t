using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Resolution;

/// <summary>A proposed pairing between one build token and a readable message.</summary>
public sealed record MappingCandidate
{
    /// <summary>Build schema message token.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>Readable protobuf full name, or null when no candidate can be named.</summary>
    public string? Readable { get; init; }

    /// <summary>Observed or requested direction, when known.</summary>
    public string? Direction { get; init; }

    /// <summary>Classification determined by structural, observation, and semantic proof gates.</summary>
    public CandidateClassification Classification { get; init; }

    /// <summary>Ranking hint only; never classification proof.</summary>
    public HeuristicScore Score { get; init; } = new();

    /// <summary>Build identity from the schema.</summary>
    [JsonIgnore]
    public string ClientVersion { get; init; } = string.Empty;

    /// <summary>Schema fingerprint.</summary>
    [JsonIgnore]
    public string SchemaSha256 { get; init; } = string.Empty;

    /// <summary>Whether schema provenance is exact to the named build.</summary>
    [JsonIgnore]
    public bool BuildExact { get; init; }

    /// <summary>Whether the complete declared graph matched exactly.</summary>
    public bool ExactShape { get; init; }

    /// <summary>Whether the structural match is unambiguous.</summary>
    public bool UniqueShape { get; init; }

    /// <summary>Whether a valid observation supports this token.</summary>
    public bool HasValidObservation { get; init; }

    /// <summary>Readable semantic identity established by a matching proof, or null when unproven.</summary>
    public string? SemanticIdentity { get; init; }

    /// <summary>Proof supplied for this candidate, whether valid or invalid.</summary>
    public SemanticProof? Proof { get; init; }

    /// <summary>Ordered provenance evidence.</summary>
    public IReadOnlyList<CandidateEvidence> Evidence { get; init; } = Array.Empty<CandidateEvidence>();

    /// <summary>Hard contradictions.</summary>
    public IReadOnlyList<ResolverContradiction> Contradictions { get; init; } = Array.Empty<ResolverContradiction>();

    /// <summary>Remaining uncertainties.</summary>
    [JsonPropertyName("uncertainty")]
    public IReadOnlyList<ResolverUncertainty> Uncertainties { get; init; } = Array.Empty<ResolverUncertainty>();

    /// <summary>Fixture proposal available only for verified candidates.</summary>
    [JsonPropertyName("fixture")]
    public SyntheticFixtureProposal? FixtureProposal { get; init; }
}
