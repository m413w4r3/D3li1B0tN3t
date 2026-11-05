namespace D3B.Diagnostic.Resolution;

/// <summary>A provenance-bearing, redacted evidence item.</summary>
public sealed record CandidateEvidence
{
    /// <summary>Evidence source: schema, readable, reference, observation or prior_mapping.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Build associated with this evidence.</summary>
    public string Build { get; init; } = string.Empty;

    /// <summary>Whether the evidence is exact and verified for its named build.</summary>
    public bool Verified { get; init; }

    /// <summary>Source-level provenance label.</summary>
    public string Provenance { get; init; } = string.Empty;

    /// <summary>Redacted structural detail; never contains payload values.</summary>
    public string Detail { get; init; } = string.Empty;
}
