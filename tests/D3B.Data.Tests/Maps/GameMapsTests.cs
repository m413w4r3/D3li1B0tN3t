using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Tests.Packs;

namespace D3B.Data.Tests.Maps;

public sealed class GameMapsTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly GamePack _pack;

    public GameMapsTests()
    {
        _pack = TestPacks.Open(_directory, TestPacks.Write(writer =>
        {
            writer.Add(MapData.EntryName(10), TestMaps.Map(10));
            writer.Add(MapData.EntryName(20), TestMaps.Map(20));
        }));
    }

    public void Dispose()
    {
        _pack.Dispose();
        _directory.Dispose();
    }

    [Fact]
    public void AMapIsReadOnceWhileItStaysInTheCache()
    {
        var maps = new GameMaps(_pack);

        Assert.Same(maps.Get(10), maps.Get(10));
        Assert.Equal(20, maps.Get(20).Id);
    }

    [Fact]
    public void AMissingMapIsReported()
    {
        var maps = new GameMaps(_pack);

        Assert.True(maps.Contains(10));
        Assert.False(maps.Contains(30));
        Assert.False(maps.TryGet(30, out _));
        Assert.Throws<KeyNotFoundException>(() => maps.Get(30));
    }
}
