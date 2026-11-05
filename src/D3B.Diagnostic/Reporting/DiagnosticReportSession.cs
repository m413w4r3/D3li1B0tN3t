using System.Text.Json.Serialization;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

/// <summary>Bounded session accounting included in a diagnostic report.</summary>
public sealed record DiagnosticReportSession
{
/// <summary>Gets or initializes the State value.</summary>
    public DiagnosticReportSessionState State { get; init; }
/// <summary>Gets or initializes the ErrorCode value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DiagnosticErrorCode? ErrorCode { get; init; }
/// <summary>Gets or initializes the EnqueuedFrames value.</summary>
    public long EnqueuedFrames { get; init; }
/// <summary>Gets or initializes the AnalyzedFrames value.</summary>
    public long AnalyzedFrames { get; init; }
/// <summary>Gets or initializes the DroppedFrames value.</summary>
    public long DroppedFrames { get; init; }
/// <summary>Gets or initializes the UnattributedAnalyses value.</summary>
    public long UnattributedAnalyses { get; init; }
/// <summary>Gets or initializes the IncludeReadable value.</summary>
    public bool IncludeReadable { get; init; }
/// <summary>Gets or initializes the QueueCapacity value.</summary>
    public int QueueCapacity { get; init; }
/// <summary>Gets or initializes the QueueByteCapacity value.</summary>
    public long QueueByteCapacity { get; init; }
}
