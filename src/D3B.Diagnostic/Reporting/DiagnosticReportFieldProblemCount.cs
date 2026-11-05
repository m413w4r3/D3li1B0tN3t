using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

/// <summary>Count for one field-level diagnostic problem.</summary>
public sealed record DiagnosticReportFieldProblemCount
{
/// <summary>Gets or initializes the Code value.</summary>
    public DiagnosticProblemCode Code { get; init; }
/// <summary>Gets or initializes the Occurrences value.</summary>
    public long Occurrences { get; init; }
}
