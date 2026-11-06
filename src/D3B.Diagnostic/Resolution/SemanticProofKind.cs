using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Resolution;

/// <summary>Stable semantic proof mechanisms accepted by the resolver.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SemanticProofKind>))]
public enum SemanticProofKind
{
    /// <summary>An absent, empty, or unrecognized proof kind.</summary>
    [JsonStringEnumMemberName("UNKNOWN")]
    Unknown,

    /// <summary>Identity established from IL2CPP metadata.</summary>
    [JsonStringEnumMemberName("IL2CPP")]
    Il2Cpp,

    /// <summary>Identity established by observed behavior.</summary>
    [JsonStringEnumMemberName("BEHAVIORAL")]
    Behavioral,

    /// <summary>Identity recorded in an audited build pin.</summary>
    [JsonStringEnumMemberName("AUDITED_PIN")]
    AuditedPin,

    /// <summary>Identity recorded in an audited normalized reference.</summary>
    [JsonStringEnumMemberName("AUDITED_REFERENCE")]
    AuditedReference
}
