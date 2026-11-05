using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Observation;

/// <summary>Redacted counters and results for one diagnostic observation session.</summary>
public sealed record DiagnosticSessionSnapshot
{
    /// <summary>Gets the number of frames accepted by the queue.</summary>
    public long EnqueuedFrames { get; init; }

    /// <summary>Gets the number of frames analyzed by the sidecar.</summary>
    public long AnalyzedFrames { get; init; }

    /// <summary>Gets the number of frames dropped or left unanalyzed.</summary>
    public long DroppedFrames { get; init; }

    /// <summary>Gets the stable sidecar failure code, if a failure occurred.</summary>
    public DiagnosticErrorCode? ErrorCode { get; init; }

    /// <summary>Gets whether observation completed or is partial.</summary>
    public string Coverage { get; init; } = "partial";

    /// <summary>Gets the last redacted analysis, if one completed.</summary>
    public DiagnosticAnalysis? LastAnalysis { get; init; }

    /// <summary>Gets the redacted summary, if one was received.</summary>
    public DiagnosticSummary? Summary { get; init; }
}
