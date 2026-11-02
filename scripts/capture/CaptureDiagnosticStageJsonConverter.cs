using System.Text.Json;
using System.Text.Json.Serialization;

namespace D3B.Scripts.Capture;

internal sealed class CaptureDiagnosticStageJsonConverter : JsonStringEnumConverter<CaptureDiagnosticStage>
{
    public CaptureDiagnosticStageJsonConverter()
        : base(JsonNamingPolicy.CamelCase)
    {
    }
}
