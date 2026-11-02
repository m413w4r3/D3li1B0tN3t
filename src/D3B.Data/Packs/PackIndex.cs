using MemoryPack;

namespace D3B.Data.Packs;

/// <summary>The index written at the end of a pack: what it was made from, and its entries in the order written.</summary>
[MemoryPackable]
internal sealed partial class PackIndex
{
    public required PackInfo Info { get; init; }

    public required PackEntry[] Entries { get; init; }
}
