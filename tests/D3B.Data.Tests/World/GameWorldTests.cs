using D3B.Data.Tests.Packs;
using D3B.Data.World;

namespace D3B.Data.Tests.World;

public sealed class GameWorldTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void TheGraphAndTheAtlasAreReadOnce()
    {
        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer =>
        {
            writer.Add(WorldAtlas.EntryName, TestWorld.Atlas());
            writer.Add(WorldGraph.EntryName, TestWorld.Graph());
        }));

        var world = new GameWorld(pack);

        Assert.Same(world.Graph, world.Graph);
        Assert.Same(world.Atlas, world.Atlas);
        Assert.Equal(4, world.Graph.Vertices.Length);
        Assert.Single(world.Atlas.Waypoints.ToArray());
    }

    [Fact]
    public void AMissingWorldIsReportedEachTimeItIsAskedFor()
    {
        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add("records/1", TestPacks.Record(1))));

        var world = new GameWorld(pack);

        Assert.Throws<KeyNotFoundException>(() => world.Graph);
        Assert.Throws<KeyNotFoundException>(() => world.Graph);
        Assert.Throws<KeyNotFoundException>(() => world.Atlas);
    }
}
