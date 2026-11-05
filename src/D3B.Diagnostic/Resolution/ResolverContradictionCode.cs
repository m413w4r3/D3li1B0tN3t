using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Resolution;

/// <summary>Stable hard contradiction codes emitted by the resolver.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ResolverContradictionCode>))]
public enum ResolverContradictionCode
{
    /// <summary>Field kinds differ.</summary>
    [JsonStringEnumMemberName("FIELD_TYPE_MISMATCH")]
    FieldTypeMismatch,
    /// <summary>Field numbers differ.</summary>
    [JsonStringEnumMemberName("FIELD_NUMBER_MISMATCH")]
    FieldNumberMismatch,
    /// <summary>Field repetition differs.</summary>
    [JsonStringEnumMemberName("FIELD_REPETITION_MISMATCH")]
    FieldRepetitionMismatch,
    /// <summary>Nested message structures differ.</summary>
    [JsonStringEnumMemberName("NESTED_SHAPE_MISMATCH")]
    NestedShapeMismatch,
    /// <summary>Observed direction contradicts a request, event or response name.</summary>
    [JsonStringEnumMemberName("DIRECTION_CONTRADICTION")]
    DirectionContradiction,
    /// <summary>Independent reference corpora name different readable messages for one token.</summary>
    [JsonStringEnumMemberName("CONFLICTING_REFERENCES")]
    ConflictingReferences
}
