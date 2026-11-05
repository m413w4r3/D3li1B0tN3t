namespace D3B.Diagnostic.Reporting;

/// <summary>Bounds for report aggregation, serialization, and loading.</summary>
public sealed record DiagnosticReportOptions
{
    /// <summary>Gets or initializes the Enabled value.</summary>
    public bool Enabled { get; init; }
    /// <summary>Gets or initializes the MaxTypes value.</summary>
    public int MaxTypes { get; init; } = 2_048;
    /// <summary>Gets or initializes the MaxFieldsPerType value.</summary>
    public int MaxFieldsPerType { get; init; } = 256;
    /// <summary>Gets or initializes the MaxFieldNodesPerType value.</summary>
    public int MaxFieldNodesPerType { get; init; } = 4_096;
    /// <summary>Gets or initializes the MaxNestedDepth value.</summary>
    public int MaxNestedDepth { get; init; } = 8;
    /// <summary>Gets or initializes the MaxVariantsPerType value.</summary>
    public int MaxVariantsPerType { get; init; } = 16;
    /// <summary>Gets or initializes the MaxShapeValues value.</summary>
    public int MaxShapeValues { get; init; } = 32;
    /// <summary>Gets or initializes the MaxProblemsPerType value.</summary>
    public int MaxProblemsPerType { get; init; } = 64;
    /// <summary>Gets or initializes the MaxBytes value.</summary>
    public int MaxBytes { get; init; } = 4 * 1024 * 1024;

    /// <summary>Provides the Validate report operation.</summary>
    public void Validate()
    {
        ValidateRange(MaxTypes, 1, 20_000, nameof(MaxTypes));
        ValidateRange(MaxFieldsPerType, 1, 4_096, nameof(MaxFieldsPerType));
        ValidateRange(MaxFieldNodesPerType, 1, 65_536, nameof(MaxFieldNodesPerType));
        ValidateRange(MaxNestedDepth, 0, 32, nameof(MaxNestedDepth));
        ValidateRange(MaxVariantsPerType, 1, 1_024, nameof(MaxVariantsPerType));
        ValidateRange(MaxShapeValues, 1, 4_096, nameof(MaxShapeValues));
        ValidateRange(MaxProblemsPerType, 1, 4_096, nameof(MaxProblemsPerType));
        ValidateRange(MaxBytes, 65_536, 67_108_864, nameof(MaxBytes));
    }

    private static void ValidateRange(int value, int minimum, int maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
