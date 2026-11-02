namespace D3B.Scripts.Inspect;

/// <summary>How a block of a UnityFS bundle is compressed: the low six bits of its flags.</summary>
internal enum BundleCompression
{
    None = 0,
    Lzma = 1,
    Lz4 = 2,
    Lz4HC = 3,
    Lzham = 4,
}
