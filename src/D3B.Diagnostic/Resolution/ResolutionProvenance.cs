namespace D3B.Diagnostic.Resolution;

/// <summary>Identifies the report source and build observation behind a resolution.</summary>
public sealed record ResolutionProvenance
{
    /// <summary>Source label for the resolution input.</summary>
    public string Source { get; init; } = string.Empty;
    /// <summary>SHA-256 of the exact report file bytes.</summary>
    public string ReportSha256 { get; init; } = string.Empty;
    /// <summary>Report format version.</summary>
    public int ReportVersion { get; init; }
    /// <summary>Report protocol version.</summary>
    public int ReportProtocolVersion { get; init; }
    /// <summary>Diagnostic engine name.</summary>
    public string Engine { get; init; } = string.Empty;
    /// <summary>Diagnostic engine version.</summary>
    public string EngineVersion { get; init; } = string.Empty;
    /// <summary>SNS source commit recorded by the report.</summary>
    public string SnsCommit { get; init; } = string.Empty;
    /// <summary>Observation source label.</summary>
    public string ObservationSource { get; init; } = string.Empty;
    /// <summary>Effective coverage used by the resolver.</summary>
    public string ObservationCoverage { get; init; } = string.Empty;
    /// <summary>Number of frames dropped before analysis.</summary>
    public long ObservationDroppedFrames { get; init; }
    /// <summary>Whether report or type detail was truncated.</summary>
    public bool ReportTruncated { get; init; }
}
