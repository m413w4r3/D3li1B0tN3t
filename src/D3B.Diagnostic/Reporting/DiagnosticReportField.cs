using System.Text.Json.Serialization;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

/// <summary>Aggregated observations for a protobuf field number.</summary>
public sealed record DiagnosticReportField
{
/// <summary>Gets or initializes the Number value.</summary>
    public int Number { get; init; }
/// <summary>Gets or initializes the Name value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }
/// <summary>Gets or initializes the DeclaredKind value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DiagnosticFieldKind? DeclaredKind { get; init; }
/// <summary>Gets or initializes the Repeated value.</summary>
    public bool Repeated { get; init; }
/// <summary>Gets or initializes the Packed value.</summary>
    public bool Packed { get; init; }
/// <summary>Gets or initializes the Occurrences value.</summary>
    public long Occurrences { get; init; }
/// <summary>Gets or initializes the WireTypes value.</summary>
    public IReadOnlyList<DiagnosticWireType> WireTypes { get; init; } = Array.Empty<DiagnosticWireType>();
/// <summary>Gets or initializes the Observed value.</summary>
    public DiagnosticObservedShape Observed { get; init; } = new();
/// <summary>Gets or initializes the Nested value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DiagnosticReportField>? Nested { get; init; }
/// <summary>Gets or initializes the Problems value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DiagnosticReportFieldProblemCount>? Problems { get; init; }
}
