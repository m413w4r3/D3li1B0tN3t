using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Contract;

/// <summary>Redacted structural properties observed in a field.</summary>
public sealed record DiagnosticObservedShape
{
    /// <summary>Distinct encoded varint widths.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<int>? VarintBytes { get; init; }
    /// <summary>Distinct fixed-width lengths.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<int>? FixedLength { get; init; }
    /// <summary>Distinct payload lengths.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<int>? Length { get; init; }
    /// <summary>Whether the length list was capped.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LengthCapped { get; init; }
    /// <summary>Whether all string candidates were valid UTF-8.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Utf8 { get; init; }
    /// <summary>Distinct nested field counts.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<int>? FieldCount { get; init; }
    /// <summary>Whether any occurrence had zero length.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ZeroLength { get; init; }
    /// <summary>Whether an occurrence was truncated.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Truncated { get; init; }
}
