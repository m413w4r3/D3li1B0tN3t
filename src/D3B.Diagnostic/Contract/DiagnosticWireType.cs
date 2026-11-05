using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Contract;

/// <summary>Observed protobuf wire types.</summary>
public enum DiagnosticWireType
{
    /// <summary>Varint encoding.</summary>
    [JsonStringEnumMemberName("VARINT")]
    Varint,

    /// <summary>Eight-byte fixed encoding.</summary>
    [JsonStringEnumMemberName("I64")]
    I64,

    /// <summary>Length-delimited encoding.</summary>
    [JsonStringEnumMemberName("LEN")]
    Len,

    /// <summary>Four-byte fixed encoding.</summary>
    [JsonStringEnumMemberName("I32")]
    I32,

    /// <summary>Unsupported protobuf group marker.</summary>
    [JsonStringEnumMemberName("GROUP")]
    Group,

    /// <summary>Other wire type value not recognized by the protocol.</summary>
    [JsonStringEnumMemberName("UNKNOWN")]
    Unknown
}
