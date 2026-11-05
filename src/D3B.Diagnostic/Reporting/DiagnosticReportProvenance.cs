namespace D3B.Diagnostic.Reporting;

/// <summary>Identifies the producer and time span for one report.</summary>
public sealed record DiagnosticReportProvenance
{
    /// <summary>Gets or initializes the Source value.</summary>
    public string Source { get; init; } = "live";
    /// <summary>Gets or initializes the Producer value.</summary>
    public string Producer { get; init; } = "D3B.Diagnostic";
    /// <summary>Gets or initializes the ProducerVersion value.</summary>
    public string ProducerVersion { get; init; } = string.Empty;
    /// <summary>Gets or initializes the StartedAtUtc value.</summary>
    public DateTimeOffset StartedAtUtc { get; init; }
    /// <summary>Gets or initializes the CompletedAtUtc value.</summary>
    public DateTimeOffset CompletedAtUtc { get; init; }
}
