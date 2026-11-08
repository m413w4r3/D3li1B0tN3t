using System.Text.Json.Serialization;

namespace D3B.Scripts.Preflight;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PreflightManifest))]
internal sealed partial class PreflightManifestJsonContext : JsonSerializerContext
{
}
