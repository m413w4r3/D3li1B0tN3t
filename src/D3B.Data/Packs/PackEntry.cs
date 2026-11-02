using MemoryPack;

namespace D3B.Data.Packs;

/// <summary>Where an entry of a pack lies, and what it holds.</summary>
[MemoryPackable]
internal sealed partial class PackEntry
{
    public required string Name { get; init; }

    /// <summary>The full name of the type the entry was written as, which a read must ask for.</summary>
    public required string Type { get; init; }

    /// <summary>Where the entry's compressed bytes start, from the start of the pack.</summary>
    public required long Offset { get; init; }

    public required int CompressedLength { get; init; }

    /// <summary>The length of the serialized entry once decompressed.</summary>
    public required int Length { get; init; }
}
