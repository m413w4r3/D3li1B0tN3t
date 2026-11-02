using System.Text.Json.Serialization;

namespace D3B.Scripts.Capture;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(CaptureDiagnosticReport))]
internal sealed partial class CaptureDiagnosticJsonContext : JsonSerializerContext
{
}
