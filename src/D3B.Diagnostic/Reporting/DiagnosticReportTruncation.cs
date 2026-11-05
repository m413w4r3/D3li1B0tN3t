namespace D3B.Diagnostic.Reporting;

/// <summary>Counts structural data omitted to respect report limits.</summary>
public sealed record DiagnosticReportTruncation
{
    /// <summary>Gets or initializes the OutputTruncated value.</summary>
    public bool OutputTruncated { get; init; }
    /// <summary>Gets or initializes the TypesOmitted value.</summary>
    public long TypesOmitted { get; init; }
    /// <summary>Gets or initializes the FieldsOmitted value.</summary>
    public long FieldsOmitted { get; init; }
    /// <summary>Gets or initializes the NestedFieldsOmitted value.</summary>
    public long NestedFieldsOmitted { get; init; }
    /// <summary>Gets or initializes the VariantsOmitted value.</summary>
    public long VariantsOmitted { get; init; }
    /// <summary>Gets or initializes the ProblemsOmitted value.</summary>
    public long ProblemsOmitted { get; init; }
    /// <summary>Gets or initializes the ShapeValuesOmitted value.</summary>
    public long ShapeValuesOmitted { get; init; }
    /// <summary>Gets or initializes the Reasons value.</summary>
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}
