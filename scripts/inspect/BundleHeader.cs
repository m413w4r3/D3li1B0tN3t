using AssetsTools.NET;

namespace D3B.Scripts.Inspect;

/// <summary>What the header of a UnityFS bundle says, read without decompressing its data.</summary>
/// <param name="Bundle">The bundle.</param>
/// <param name="DiskSize">Its size on disk.</param>
/// <param name="Version">The UnityFS format version.</param>
/// <param name="EngineVersion">The Unity version written by the build, <c>0.0.0</c> when the build strips it.</param>
/// <param name="Blocks">Its storage blocks.</param>
/// <param name="Entries">The files it stores.</param>
internal sealed record BundleHeader(
    BundlePath Bundle,
    long DiskSize,
    uint Version,
    string EngineVersion,
    IReadOnlyList<BundleBlock> Blocks,
    IReadOnlyList<BundleEntry> Entries)
{
    public long DataSize => Blocks.Sum(block => (long)block.DecompressedSize);

    /// <summary>The compressions its blocks use, such as <c>Lz4HC</c> or <c>Lz4HC+None</c>.</summary>
    public string Compression => string.Join('+', Blocks.Select(block => block.Compression).Distinct());

    /// <summary>The decompressed size of its largest block: the block size the build chose.</summary>
    public uint BlockSize => Blocks.Count is 0 ? 0 : Blocks.Max(block => block.DecompressedSize);

    public static BundleHeader Read(BundlePath bundle)
    {
        using var stream = File.OpenRead(bundle.FullPath);
        using var reader = new AssetsFileReader(stream);
        var file = new AssetBundleFile();

        file.Read(reader);

        var blocks = file.BlockAndDirInfo.BlockInfos
            .Select(block => new BundleBlock(block.CompressedSize, block.DecompressedSize, (BundleCompression)(block.Flags & 0x3F)))
            .ToList();

        var entries = file.BlockAndDirInfo.DirectoryInfos
            .Select(entry => new BundleEntry(entry.Name, entry.DecompressedSize, entry.IsSerialized))
            .ToList();

        return new BundleHeader(bundle, stream.Length, file.Header.Version, file.Header.EngineVersion, blocks, entries);
    }
}
