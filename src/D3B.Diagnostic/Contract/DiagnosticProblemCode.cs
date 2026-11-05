using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Contract;

/// <summary>Stable problem codes emitted by the diagnostic engine.</summary>
public enum DiagnosticProblemCode
{
    /// <summary>No schema message matched the carried type.</summary>
    [JsonStringEnumMemberName("UNKNOWN_TYPE")] UnknownType,
    /// <summary>The field number is absent from the descriptor.</summary>
    [JsonStringEnumMemberName("UNKNOWN_FIELD_NUMBER")] UnknownFieldNumber,
    /// <summary>The observed wire type cannot encode the declared kind.</summary>
    [JsonStringEnumMemberName("WIRE_TYPE_MISMATCH")] WireTypeMismatch,
    /// <summary>A packed value arrived for an unpacked field.</summary>
    [JsonStringEnumMemberName("PACKED_NOT_ALLOWED")] PackedNotAllowed,
    /// <summary>A string payload is not valid UTF-8.</summary>
    [JsonStringEnumMemberName("INVALID_UTF8")] InvalidUtf8,
    /// <summary>A protobuf group was observed.</summary>
    [JsonStringEnumMemberName("GROUP_UNSUPPORTED")] GroupUnsupported,
    /// <summary>Message nesting exceeded the limit.</summary>
    [JsonStringEnumMemberName("DEPTH_EXCEEDED")] DepthExceeded,
    /// <summary>A length-delimited value exceeded its container.</summary>
    [JsonStringEnumMemberName("LENGTH_OVERFLOW")] LengthOverflow,
    /// <summary>A varint exceeded the supported width.</summary>
    [JsonStringEnumMemberName("VARINT_OVERFLOW")] VarintOverflow,
    /// <summary>A payload or nested value ended early.</summary>
    [JsonStringEnumMemberName("TRUNCATED")] Truncated,
    /// <summary>A tag or wire type could not be decoded.</summary>
    [JsonStringEnumMemberName("MALFORMED")] Malformed,
    /// <summary>A protocol limit was exceeded.</summary>
    [JsonStringEnumMemberName("LIMIT_EXCEEDED")] LimitExceeded,
    /// <summary>A message field references a missing schema message.</summary>
    [JsonStringEnumMemberName("SCHEMA_MESSAGE_MISSING")] SchemaMessageMissing,
    /// <summary>The schema was internally invalid.</summary>
    [JsonStringEnumMemberName("SCHEMA_INVALID")] SchemaInvalid
}
