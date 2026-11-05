using System.Text.Json.Serialization;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

/// <summary>Count for a problem code at a sanitized field path.</summary>
public sealed record DiagnosticReportProblemCount
{
/// <summary>Gets or initializes the Code value.</summary>
    public DiagnosticProblemCode Code { get; init; }
/// <summary>Gets or initializes the FieldPath value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FieldPath { get; init; }
/// <summary>Gets or initializes the Occurrences value.</summary>
    public long Occurrences { get; init; }
}
