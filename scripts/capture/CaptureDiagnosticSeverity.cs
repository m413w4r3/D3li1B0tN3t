using System.Text.Json.Serialization;

namespace D3B.Scripts.Capture;

/// <summary>The severity of a capture diagnostic.</summary>
[JsonConverter(typeof(CaptureDiagnosticSeverityJsonConverter))]
public enum CaptureDiagnosticSeverity
{
    /// <summary>An observation that does not affect status.</summary>
    Info,

    /// <summary>A recoverable or incomplete capture condition.</summary>
    Warning,

    /// <summary>A capture or frame could not be interpreted.</summary>
    Error,
}
