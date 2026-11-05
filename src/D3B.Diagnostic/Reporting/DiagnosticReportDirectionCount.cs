namespace D3B.Diagnostic.Reporting;

/// <summary>Count for one supported frame direction.</summary>
public sealed record DiagnosticReportDirectionCount
{
    /// <summary>Gets or initializes the Direction value.</summary>
    public string Direction { get; init; } = "unknown";
    /// <summary>Gets or initializes the Occurrences value.</summary>
    public long Occurrences { get; init; }
}
