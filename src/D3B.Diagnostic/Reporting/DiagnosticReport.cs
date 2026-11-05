namespace D3B.Diagnostic.Reporting;

/// <summary>A bounded, redacted aggregate of diagnostic observations.</summary>
public sealed record DiagnosticReport
{
/// <summary>Gets or initializes the Kind value.</summary>
    public string Kind { get; init; } = "diagnostic-report";
/// <summary>Gets or initializes the ReportVersion value.</summary>
    public int ReportVersion { get; init; } = 1;
/// <summary>Gets or initializes the ProtocolVersion value.</summary>
    public int ProtocolVersion { get; init; }
/// <summary>Gets or initializes the ClientVersion value.</summary>
    public string ClientVersion { get; init; } = string.Empty;
/// <summary>Gets or initializes the SchemaSha256 value.</summary>
    public string SchemaSha256 { get; init; } = string.Empty;
/// <summary>Gets or initializes the Engine value.</summary>
    public DiagnosticReportEngine Engine { get; init; } = new();
/// <summary>Gets or initializes the Provenance value.</summary>
    public DiagnosticReportProvenance Provenance { get; init; } = new();
/// <summary>Gets or initializes the Session value.</summary>
    public DiagnosticReportSession Session { get; init; } = new();
/// <summary>Gets or initializes the Coverage value.</summary>
    public string Coverage { get; init; } = "partial";
/// <summary>Gets or initializes the Types value.</summary>
    public IReadOnlyList<DiagnosticReportType> Types { get; init; } = Array.Empty<DiagnosticReportType>();
/// <summary>Gets or initializes the Truncation value.</summary>
    public DiagnosticReportTruncation Truncation { get; init; } = new();
}
