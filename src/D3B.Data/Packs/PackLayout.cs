namespace D3B.Data.Packs;

/// <summary>How a pack is laid out on disk, little-endian throughout.</summary>
/// <remarks>
/// <para>
/// A header: the magic, then the format version as an <c>int32</c>. Then the entries, each a MemoryPack payload
/// compressed as one zstd frame with its checksum. Then the index, compressed the same way. Then a trailer: the index's
/// offset as an <c>int64</c>, its compressed and decompressed lengths as <c>int32</c>, and the magic again.
/// </para>
/// <para>
/// The format version comes first, so that a pack of another format is reported as such rather than as damaged. The
/// index comes last, so that the writer streams entries without knowing them in advance; a pack whose writing was cut
/// short has no trailer and is rejected.
/// </para>
/// </remarks>
internal static class PackLayout
{
    public const int HeaderLength = 12;

    public const int TrailerLength = 24;

    public static ReadOnlySpan<byte> Magic => "D3B-PACK"u8;
}
