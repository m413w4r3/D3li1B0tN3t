namespace D3B.Diagnostic.Resolution;

/// <summary>Redacted observation coverage for one resolver run.</summary>
public sealed record CandidateObservationSummary
{
    /// <summary>Number of analysed frames.</summary>
    public long Frames { get; init; }

    /// <summary>Observation coverage label.</summary>
    public string Coverage { get; init; } = "complete";

    /// <summary>Number of frames dropped before analysis.</summary>
    public long Dropped { get; init; }
}
