using System.Text.Json.Serialization;

namespace D3B.Scripts.Preflight;

internal sealed class PreflightManifest
{
    [JsonPropertyName("schemaVersion")]
    [JsonPropertyOrder(0)]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("entries")]
    [JsonPropertyOrder(1)]
    public List<PreflightManifestEntry> Entries { get; init; } = [];

    public PreflightManifest()
    {
    }

    public PreflightManifest(IEnumerable<PreflightManifestEntry> entries)
    {
        Entries = entries.ToList();
    }
}
