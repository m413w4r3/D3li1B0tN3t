using System.Text.Json;
using System.Text.Json.Serialization;

namespace D3B.Scripts.Capture;

internal sealed class CaptureDiagnosticSeverityJsonConverter : JsonStringEnumConverter<CaptureDiagnosticSeverity>
{
    public CaptureDiagnosticSeverityJsonConverter()
        : base(JsonNamingPolicy.CamelCase)
    {
    }
}
