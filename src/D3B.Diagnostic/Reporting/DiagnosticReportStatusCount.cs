using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

/// <summary>Count for one diagnostic result status.</summary>
public sealed record DiagnosticReportStatusCount
{
/// <summary>Gets or initializes the Status value.</summary>
    public DiagnosticStatus Status { get; init; }
/// <summary>Gets or initializes the Occurrences value.</summary>
    public long Occurrences { get; init; }
}
