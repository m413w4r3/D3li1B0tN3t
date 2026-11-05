using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Contract;

/// <summary>Redacted analysis for one observed field.</summary>
public sealed record DiagnosticFieldAnalysis
{
    /// <summary>Field number.</summary>
    public int Number { get; init; }
    /// <summary>Schema field name, if known.</summary>
    public string? Name { get; init; }
    /// <summary>Schema field kind, if known.</summary>
    public DiagnosticFieldKind? DeclaredKind { get; init; }
    /// <summary>Whether the schema field is repeated.</summary>
    public bool Repeated { get; init; }
    /// <summary>Whether the schema field is packed.</summary>
    public bool Packed { get; init; }
    /// <summary>Number of observed occurrences.</summary>
    public int Occurrences { get; init; }
    /// <summary>Distinct wire types in first-seen order.</summary>
    public IReadOnlyList<DiagnosticWireType> WireTypes { get; init; } = Array.Empty<DiagnosticWireType>();
    /// <summary>Redacted shape information.</summary>
    public DiagnosticObservedShape Observed { get; init; } = new();
    /// <summary>Nested field analyses.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DiagnosticFieldAnalysis>? Nested { get; init; }
    /// <summary>Field-level problem codes.</summary>
    public IReadOnlyList<DiagnosticProblemCode> Problems { get; init; } = Array.Empty<DiagnosticProblemCode>();
}
