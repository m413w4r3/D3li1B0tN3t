using MemoryPack;

namespace D3B.Data.Tests.Packs;

[MemoryPackable]
internal sealed partial class SampleRecord
{
    public required int Id { get; init; }

    public required string Name { get; init; }
}
