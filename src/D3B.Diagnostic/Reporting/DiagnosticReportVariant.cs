namespace D3B.Diagnostic.Reporting;

/// <summary>Count for one safe top-level wire structure.</summary>
public sealed record DiagnosticReportVariant
{
    /// <summary>Gets or initializes the Signature value.</summary>
    public string Signature { get; init; } = string.Empty;
    /// <summary>Gets or initializes the Occurrences value.</summary>
    public long Occurrences { get; init; }
    /// <summary>Gets or initializes the Statuses value.</summary>
    public IReadOnlyList<DiagnosticReportStatusCount> Statuses { get; init; } = Array.Empty<DiagnosticReportStatusCount>();
}
