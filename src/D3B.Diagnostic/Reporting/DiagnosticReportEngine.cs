namespace D3B.Diagnostic.Reporting;

/// <summary>Whitelist-safe diagnostic engine identity.</summary>
public sealed record DiagnosticReportEngine
{
    /// <summary>Gets or initializes the ProtocolVersion value.</summary>
    public int ProtocolVersion { get; init; }
    /// <summary>Gets or initializes the Engine value.</summary>
    public string Engine { get; init; } = string.Empty;
    /// <summary>Gets or initializes the EngineVersion value.</summary>
    public string EngineVersion { get; init; } = string.Empty;
    /// <summary>Gets or initializes the SnsCommit value.</summary>
    public string SnsCommit { get; init; } = string.Empty;
}
