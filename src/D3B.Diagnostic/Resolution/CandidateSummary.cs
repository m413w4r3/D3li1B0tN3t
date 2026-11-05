namespace D3B.Diagnostic.Resolution;

/// <summary>Counts of each resolver classification.</summary>
public sealed record CandidateSummary
{
    /// <summary>Number of verified candidates.</summary>
    public int Verified { get; init; }

    /// <summary>Number of unproven candidates.</summary>
    public int Candidate { get; init; }

    /// <summary>Number of contradictory candidates.</summary>
    public int Conflict { get; init; }

    /// <summary>Number of unknown candidates.</summary>
    public int Unknown { get; init; }
}
