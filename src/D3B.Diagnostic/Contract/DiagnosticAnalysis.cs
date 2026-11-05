using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Contract;

/// <summary>Redacted structural analysis of one frame.</summary>
public sealed record DiagnosticAnalysis
{
    /// <summary>The protocol discriminator.</summary>
    public string Kind { get; init; } = "analysis";
    /// <summary>Input frame index.</summary>
    public long Index { get; init; }
    /// <summary>Direction of the frame.</summary>
    public string Direction { get; init; } = string.Empty;
    /// <summary>Carried type token, if present.</summary>
    public string? CarriedType { get; init; }
    /// <summary>Whether the type URL exactly matched the carried type.</summary>
    public bool TypeUrlOk { get; init; }
    /// <summary>Matched schema message name, if any.</summary>
    public string? Message { get; init; }
    /// <summary>Analysis status.</summary>
    public DiagnosticStatus Status { get; init; }
    /// <summary>Observed top-level fields.</summary>
    public IReadOnlyList<DiagnosticFieldAnalysis> Fields { get; init; } = Array.Empty<DiagnosticFieldAnalysis>();
    /// <summary>Frame-level problem codes.</summary>
    public IReadOnlyList<DiagnosticProblem> Problems { get; init; } = Array.Empty<DiagnosticProblem>();
    /// <summary>Whether nested detail was truncated to fit output limits.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DetailTruncated { get; init; }
}
