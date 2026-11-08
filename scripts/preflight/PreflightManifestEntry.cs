using System.Text.Json.Serialization;

namespace D3B.Scripts.Preflight;

internal sealed class PreflightManifestEntry
{
    [JsonPropertyName("pathBase64")]
    [JsonPropertyOrder(0)]
    public string? PathBase64 { get; init; }

    [JsonPropertyName("kind")]
    [JsonPropertyOrder(1)]
    public string? Kind { get; init; }

    [JsonPropertyName("size")]
    [JsonPropertyOrder(2)]
    public long Size { get; init; }

    [JsonPropertyName("mode")]
    [JsonPropertyOrder(3)]
    public int Mode { get; init; }

    [JsonPropertyName("sha256")]
    [JsonPropertyOrder(4)]
    public string? Sha256 { get; init; }

    [JsonPropertyName("linkTargetBase64")]
    [JsonPropertyOrder(5)]
    public string? LinkTargetBase64 { get; init; }

    public PreflightManifestEntry()
    {
    }

    public PreflightManifestEntry(string pathBase64, string kind, long size, int mode, string? sha256, string? linkTargetBase64)
    {
        PathBase64 = pathBase64;
        Kind = kind;
        Size = size;
        Mode = mode;
        Sha256 = sha256;
        LinkTargetBase64 = linkTargetBase64;
    }
}
