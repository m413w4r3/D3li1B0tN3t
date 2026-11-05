namespace D3B.Diagnostic.Reporting;

/// <summary>A generated, redacted contradiction summary.</summary>
public sealed record DiagnosticReportContradiction
{
/// <summary>Gets or initializes the Code value.</summary>
    public DiagnosticReportContradictionCode Code { get; init; }
/// <summary>Gets or initializes the Detail value.</summary>
    public string Detail { get; init; } = string.Empty;
/// <summary>Gets or initializes the Occurrences value.</summary>
    public long Occurrences { get; init; }
}
