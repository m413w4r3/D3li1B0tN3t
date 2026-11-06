using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Resolution;

/// <summary>One deterministic resolver report for a build and observation run.</summary>
public sealed record CandidateReport
{
    /// <summary>Resolver output protocol version.</summary>
    public int ProtocolVersion { get; init; } = 2;

    /// <summary>Client build under analysis.</summary>
    public string ClientVersion { get; init; } = string.Empty;

    /// <summary>Schema fingerprint under analysis.</summary>
    public string SchemaSha256 { get; init; } = string.Empty;

    /// <summary>Observation counts and coverage.</summary>
    public CandidateObservationSummary Observations { get; init; } = new();

    /// <summary>Convenience access to dropped observation count.</summary>
    [JsonIgnore]
    public long Dropped => Observations.Dropped;

    /// <summary>Convenience access to coverage.</summary>
    [JsonIgnore]
    public string Coverage => Observations.Coverage;

    /// <summary>Deterministically ordered candidate list.</summary>
    public IReadOnlyList<MappingCandidate> Candidates { get; init; } = Array.Empty<MappingCandidate>();

    /// <summary>Classification counts.</summary>
    public CandidateSummary Summary { get; init; } = new();

    /// <summary>Provenance for a report-backed resolution.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResolutionProvenance? Provenance { get; init; }
}
