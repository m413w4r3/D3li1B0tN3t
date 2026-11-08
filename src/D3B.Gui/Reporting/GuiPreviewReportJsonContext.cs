using System.Text.Json.Serialization;

namespace D3B.Gui.Reporting;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(GuiPreviewReport))]
internal partial class GuiPreviewReportJsonContext : JsonSerializerContext
{
}
