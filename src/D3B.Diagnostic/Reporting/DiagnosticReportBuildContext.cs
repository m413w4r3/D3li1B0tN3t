using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

/// <summary>Immutable identity and session settings captured when aggregation starts.</summary>
public sealed record DiagnosticReportBuildContext
{
/// <summary>Gets or initializes the ClientVersion value.</summary>
    public string ClientVersion { get; init; } = string.Empty;
/// <summary>Gets or initializes the SchemaSha256 value.</summary>
    public string SchemaSha256 { get; init; } = string.Empty;
/// <summary>Gets or initializes the Engine value.</summary>
    public DiagnosticEngineHello Engine { get; init; } = new();
/// <summary>Gets or initializes the Producer value.</summary>
    public string Producer { get; init; } = "D3B.Diagnostic";
/// <summary>Gets or initializes the ProducerVersion value.</summary>
    public string ProducerVersion { get; init; } = string.Empty;
/// <summary>Gets or initializes the StartedAtUtc value.</summary>
    public DateTimeOffset StartedAtUtc { get; init; }
/// <summary>Gets or initializes the IncludeReadable value.</summary>
    public bool IncludeReadable { get; init; }
/// <summary>Gets or initializes the QueueCapacity value.</summary>
    public int QueueCapacity { get; init; }
/// <summary>Gets or initializes the QueueByteCapacity value.</summary>
    public long QueueByteCapacity { get; init; }
}
