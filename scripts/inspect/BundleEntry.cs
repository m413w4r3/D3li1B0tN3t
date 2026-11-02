namespace D3B.Scripts.Inspect;

/// <summary>A file stored in a UnityFS bundle.</summary>
/// <param name="Name">Its name: <c>CAB-</c> and a hash for a serialized file, with <c>.resS</c> for its streamed data.</param>
/// <param name="Size">Its decompressed size.</param>
/// <param name="IsSerialized">Whether the bundle's directory flags it as a serialized file.</param>
internal sealed record BundleEntry(string Name, long Size, bool IsSerialized);
