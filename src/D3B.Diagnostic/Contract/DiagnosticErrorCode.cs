using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Contract;

/// <summary>Stable error codes emitted when a replay cannot proceed.</summary>
public enum DiagnosticErrorCode
{
    /// <summary>The request violates the diagnostic contract.</summary>
    [JsonStringEnumMemberName("CONTRACT_INVALID")] ContractInvalid,

    /// <summary>The protocol version is unsupported.</summary>
    [JsonStringEnumMemberName("VERSION_UNSUPPORTED")] VersionUnsupported,

    /// <summary>The schema fingerprint is invalid.</summary>
    [JsonStringEnumMemberName("SCHEMA_FINGERPRINT_INVALID")] SchemaFingerprintInvalid,

    /// <summary>The schema is invalid.</summary>
    [JsonStringEnumMemberName("SCHEMA_INVALID")] SchemaInvalid,

    /// <summary>The requested command is unknown.</summary>
    [JsonStringEnumMemberName("UNKNOWN_COMMAND")] UnknownCommand,

    /// <summary>An internal engine failure occurred.</summary>
    [JsonStringEnumMemberName("INTERNAL_ERROR")] InternalError,

    /// <summary>An input or output operation failed.</summary>
    [JsonStringEnumMemberName("IO_ERROR")] IoError,

    /// <summary>Creating or using the diagnostic socket failed.</summary>
    [JsonStringEnumMemberName("SOCKET_FAILED")] SocketFailed,

    /// <summary>The current platform is unsupported.</summary>
    [JsonStringEnumMemberName("UNSUPPORTED_PLATFORM")] UnsupportedPlatform
}
