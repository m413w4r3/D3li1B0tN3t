using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

/// <summary>Terminal session accounting used when the final report is built.</summary>
public sealed record DiagnosticReportBuildOutcome
{
    /// <summary>Gets or initializes the State value.</summary>
    public DiagnosticReportSessionState State { get; init; }
    /// <summary>Gets or initializes the ErrorCode value.</summary>
    public DiagnosticErrorCode? ErrorCode { get; init; }
    /// <summary>Gets or initializes the Coverage value.</summary>
    public string Coverage { get; init; } = "partial";
    /// <summary>Gets or initializes the EnqueuedFrames value.</summary>
    public long EnqueuedFrames { get; init; }
    /// <summary>Gets or initializes the AnalyzedFrames value.</summary>
    public long AnalyzedFrames { get; init; }
    /// <summary>Gets or initializes the DroppedFrames value.</summary>
    public long DroppedFrames { get; init; }
    /// <summary>Gets or initializes the CompletedAtUtc value.</summary>
    public DateTimeOffset CompletedAtUtc { get; init; }
}
