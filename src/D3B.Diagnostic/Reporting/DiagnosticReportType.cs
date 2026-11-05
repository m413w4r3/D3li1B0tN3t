using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Reporting;

/// <summary>Aggregated diagnostics for one schema message token.</summary>
public sealed record DiagnosticReportType
{
/// <summary>Gets or initializes the Token value.</summary>
    public string Token { get; init; } = string.Empty;
/// <summary>Gets or initializes the Message value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
/// <summary>Gets or initializes the Occurrences value.</summary>
    public long Occurrences { get; init; }
/// <summary>Gets or initializes the Directions value.</summary>
    public IReadOnlyList<DiagnosticReportDirectionCount> Directions { get; init; } = Array.Empty<DiagnosticReportDirectionCount>();
/// <summary>Gets or initializes the Statuses value.</summary>
    public IReadOnlyList<DiagnosticReportStatusCount> Statuses { get; init; } = Array.Empty<DiagnosticReportStatusCount>();
/// <summary>Gets or initializes the TypeUrlOkOccurrences value.</summary>
    public long TypeUrlOkOccurrences { get; init; }
/// <summary>Gets or initializes the TypeUrlMismatchOccurrences value.</summary>
    public long TypeUrlMismatchOccurrences { get; init; }
/// <summary>Gets or initializes the Fields value.</summary>
    public IReadOnlyList<DiagnosticReportField> Fields { get; init; } = Array.Empty<DiagnosticReportField>();
/// <summary>Gets or initializes the Problems value.</summary>
    public IReadOnlyList<DiagnosticReportProblemCount> Problems { get; init; } = Array.Empty<DiagnosticReportProblemCount>();
/// <summary>Gets or initializes the Variants value.</summary>
    public IReadOnlyList<DiagnosticReportVariant> Variants { get; init; } = Array.Empty<DiagnosticReportVariant>();
/// <summary>Gets or initializes the Contradictions value.</summary>
    public IReadOnlyList<DiagnosticReportContradiction> Contradictions { get; init; } = Array.Empty<DiagnosticReportContradiction>();
/// <summary>Gets or initializes the DetailTruncated value.</summary>
    public bool DetailTruncated { get; init; }
}
