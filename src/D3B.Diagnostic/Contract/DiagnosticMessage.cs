namespace D3B.Diagnostic.Contract;

/// <summary>A protobuf message descriptor in the exported schema.</summary>
public sealed record DiagnosticMessage
{
    /// <summary>The full protobuf message name.</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>Whether this is a synthetic map-entry message.</summary>
    public bool MapEntry { get; init; }
    /// <summary>Fields sorted by number.</summary>
    public IReadOnlyList<DiagnosticField> Fields { get; init; } = Array.Empty<DiagnosticField>();
}
