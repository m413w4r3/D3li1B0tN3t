using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Contract;

/// <summary>Result status for a diagnostic frame analysis.</summary>
public enum DiagnosticStatus
{
    /// <summary>Every byte and field matched the schema.</summary>
    [JsonStringEnumMemberName("VALID")] Valid,
    /// <summary>The payload was readable but disagreed with the schema.</summary>
    [JsonStringEnumMemberName("SCHEMA_MISMATCH")] SchemaMismatch,
    /// <summary>The payload was malformed.</summary>
    [JsonStringEnumMemberName("MALFORMED")] Malformed,
    /// <summary>The payload ended inside a field.</summary>
    [JsonStringEnumMemberName("TRUNCATED")] Truncated,
    /// <summary>Bytes remained after a field boundary.</summary>
    [JsonStringEnumMemberName("TRAILING_BYTES")] TrailingBytes,
    /// <summary>No schema message matched the carried type.</summary>
    [JsonStringEnumMemberName("UNKNOWN_TYPE")] UnknownType,
    /// <summary>A protocol limit was exceeded.</summary>
    [JsonStringEnumMemberName("LIMIT_EXCEEDED")] LimitExceeded
}
