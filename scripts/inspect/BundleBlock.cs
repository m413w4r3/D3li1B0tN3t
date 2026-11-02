namespace D3B.Scripts.Inspect;

/// <summary>A storage block of a UnityFS bundle: entries are concatenated, then cut into blocks compressed one by one.</summary>
/// <param name="CompressedSize">Its size on disk.</param>
/// <param name="DecompressedSize">Its size once decompressed.</param>
/// <param name="Compression">How it is compressed.</param>
internal readonly record struct BundleBlock(uint CompressedSize, uint DecompressedSize, BundleCompression Compression);
