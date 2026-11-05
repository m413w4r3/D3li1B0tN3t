namespace D3B.Diagnostic.Reporting;

/// <summary>A loaded report and the hash of its exact source bytes.</summary>
public sealed record LoadedDiagnosticReport
{
    /// <summary>Gets or initializes the Report value.</summary>
    public DiagnosticReport Report { get; init; } = new();
    /// <summary>Gets or initializes the Sha256 value.</summary>
    public string Sha256 { get; init; } = string.Empty;
}
