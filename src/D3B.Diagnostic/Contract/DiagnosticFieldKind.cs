using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Contract;

/// <summary>Protobuf field kinds used in the exported schema.</summary>
public enum DiagnosticFieldKind
{
    /// <summary>Double precision floating point.</summary>
    [JsonStringEnumMemberName("DOUBLE")]
    DoublePrecision,

    /// <summary>Single precision floating point.</summary>
    [JsonStringEnumMemberName("FLOAT")]
    SinglePrecision,

    /// <summary>Signed 64-bit integer.</summary>
    [JsonStringEnumMemberName("INT64")]
    SignedInteger64,

    /// <summary>Unsigned 64-bit integer.</summary>
    [JsonStringEnumMemberName("UINT64")]
    UnsignedInteger64,

    /// <summary>Signed 32-bit integer.</summary>
    [JsonStringEnumMemberName("INT32")]
    SignedInteger32,

    /// <summary>Fixed-width unsigned 64-bit integer.</summary>
    [JsonStringEnumMemberName("FIXED64")]
    Fixed64,

    /// <summary>Fixed-width unsigned 32-bit integer.</summary>
    [JsonStringEnumMemberName("FIXED32")]
    Fixed32,

    /// <summary>Boolean.</summary>
    [JsonStringEnumMemberName("BOOL")]
    Boolean,

    /// <summary>UTF-8 string.</summary>
    [JsonStringEnumMemberName("STRING")]
    Utf8String,

    /// <summary>Unsupported protobuf group.</summary>
    [JsonStringEnumMemberName("GROUP")]
    Group,

    /// <summary>Nested message.</summary>
    [JsonStringEnumMemberName("MESSAGE")]
    Message,

    /// <summary>Opaque bytes.</summary>
    [JsonStringEnumMemberName("BYTES")]
    Bytes,

    /// <summary>Unsigned 32-bit integer.</summary>
    [JsonStringEnumMemberName("UINT32")]
    UnsignedInteger32,

    /// <summary>Enum.</summary>
    [JsonStringEnumMemberName("ENUM")]
    Enumeration,

    /// <summary>Signed fixed-width 32-bit integer.</summary>
    [JsonStringEnumMemberName("SFIXED32")]
    SignedFixed32,

    /// <summary>Signed fixed-width 64-bit integer.</summary>
    [JsonStringEnumMemberName("SFIXED64")]
    SignedFixed64,

    /// <summary>Zig-zag signed 32-bit integer.</summary>
    [JsonStringEnumMemberName("SINT32")]
    SignedZigZag32,

    /// <summary>Zig-zag signed 64-bit integer.</summary>
    [JsonStringEnumMemberName("SINT64")]
    SignedZigZag64
}
