using System.Text.Json.Serialization;

namespace D3B.Scripts.Capture;

/// <summary>The stage that produced a diagnostic.</summary>
[JsonConverter(typeof(CaptureDiagnosticStageJsonConverter))]
public enum CaptureDiagnosticStage
{
    /// <summary>Capture header and record reading.</summary>
    Capture,

    /// <summary>Frame boundary parsing.</summary>
    Framing,

    /// <summary>Message decoding.</summary>
    Message,

    /// <summary>Schema analysis.</summary>
    Schema,

    /// <summary>Mapping analysis.</summary>
    Mapping,
}
