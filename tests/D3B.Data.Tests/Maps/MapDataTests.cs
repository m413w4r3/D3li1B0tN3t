using System.Runtime.InteropServices;
using D3B.Data.Maps;
using D3B.Data.Tests.Packs;
using MemoryPack;

namespace D3B.Data.Tests.Maps;

public sealed class MapDataTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void AMapNeedsEveryCell()
    {
        Assert.Throws<ArgumentException>(() => new MapData(1, 0, 0, 0, 0, TestMaps.Cells().AsMemory(1), ReadOnlyMemory<MapInteractive>.Empty));
    }

    [Fact]
    public void ElementsMustBeInStrictlyAscendingOrder()
    {
        Assert.Throws<ArgumentException>(() => new MapData(1, 0, 0, 0, 0, TestMaps.Cells(), new MapInteractive[] { new(7, 10, 0), new(7, 11, 0) }));
        Assert.Throws<ArgumentException>(() => new MapData(1, 0, 0, 0, 0, TestMaps.Cells(), new MapInteractive[] { new(8, 10, 0), new(7, 11, 0) }));
    }

    [Fact]
    public void AnElementOffTheMapIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapInteractive(1, MapData.CellCount, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapInteractive(-1, 10, 0));
    }

    [Fact]
    public void AnElementIsFoundByItsIdentifier()
    {
        var map = TestMaps.Map(191104002);

        Assert.True(map.TryGetInteractive(470_007, out var element));
        Assert.Equal((254, 3212), (element.CellId, element.GfxId));
        Assert.True(map.TryGetInteractive(470_012, out var unplaced));
        Assert.Equal(MapInteractive.NoCell, unplaced.CellId);
        Assert.False(map.TryGetInteractive(470_002, out _));
    }

    [Fact]
    public void ACellIsFoundByItsIdentifier()
    {
        var map = TestMaps.Map(191104002);

        Assert.Equal(TestMaps.Cells()[42], map.Cell(42));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Cell(MapData.CellCount));
    }

    [Fact]
    public void AMapReadsBackFromAPack()
    {
        var map = TestMaps.Map(191104002);

        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add(MapData.EntryName(map.Id), map)));

        var read = pack.Read<MapData>("maps/191104002");

        Assert.Equal(map.Id, read.Id);
        Assert.Equal([map.TopNeighbourId, map.BottomNeighbourId, map.LeftNeighbourId, map.RightNeighbourId], [read.TopNeighbourId, read.BottomNeighbourId, read.LeftNeighbourId, read.RightNeighbourId]);
        Assert.Equal(map.Cells.ToArray(), read.Cells.ToArray());
        Assert.Equal(map.Interactives.ToArray(), read.Interactives.ToArray());
    }

    [Fact]
    public void CellsAreSerializedAsTheirRawBytes()
    {
        // MemoryPack copies arrays of unmanaged structs as they lie in memory: a map then costs its cells' bytes and
        // little more, and the struct's layout is the format's.
        var map = new MapData(1, 0, 0, 0, 0, TestMaps.Cells(), ReadOnlyMemory<MapInteractive>.Empty);
        var cellBytes = MapData.CellCount * Marshal.SizeOf<MapCell>();

        Assert.Equal(9, Marshal.SizeOf<MapCell>());
        Assert.InRange(MemoryPackSerializer.Serialize(map).Length, cellBytes, cellBytes + 64);
    }
}
