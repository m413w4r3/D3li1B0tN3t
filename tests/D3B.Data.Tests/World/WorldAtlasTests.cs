using D3B.Data.Tests.Packs;
using D3B.Data.World;

namespace D3B.Data.Tests.World;

public sealed class WorldAtlasTests : IDisposable
{
    private static readonly int[] _harvestables = [289, 303, 400];
    private static readonly int[] _monsters = [31, 34];

    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void ARecordIsFoundByItsIdentifier()
    {
        var atlas = TestWorld.Atlas();

        Assert.True(atlas.TryGetPosition(200, out var position));
        Assert.Equal((5, -18, 10), (position.X, position.Y, position.SubAreaId));
        Assert.False(atlas.TryGetPosition(250, out _));
        Assert.True(atlas.TryGetSubArea(11, out var subArea));
        Assert.True(subArea.BasicAccountAllowed);
        Assert.Equal(SubArea.NoZaap, subArea.AssociatedZaapMapId);
        Assert.True(atlas.TryGetArea(1, out var area));
        Assert.Equal(9001, area.NameId);
        Assert.True(atlas.TryGetWaypoint(5, out var waypoint));
        Assert.Equal(100, waypoint.MapId);
        Assert.False(atlas.TryGetWaypoint(6, out _));
    }

    [Fact]
    public void ASubAreaListsItsResources()
    {
        var atlas = TestWorld.Atlas();

        Assert.True(atlas.TryGetSubArea(10, out var farmed));
        Assert.True(atlas.TryGetSubArea(11, out var bare));
        Assert.Equal([289, 303], atlas.HarvestablesOf(farmed).ToArray());
        Assert.True(atlas.HarvestablesOf(bare).IsEmpty);
    }

    [Fact]
    public void ASubAreaListsItsMonsters()
    {
        var atlas = TestWorld.Atlas();

        Assert.True(atlas.TryGetSubArea(10, out var forest));
        Assert.True(atlas.TryGetSubArea(11, out var field));
        Assert.Equal([47], atlas.MonstersOf(forest).ToArray());
        Assert.Equal([31, 34], atlas.MonstersOf(field).ToArray());
    }

    [Fact]
    public void ATableOutOfOrderIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new WorldAtlas(
            new MapPosition[] { new(200, 0, 0, 1, 1, 1, 0), new(100, 0, 0, 1, 1, 1, 0) },
            ReadOnlyMemory<SubArea>.Empty,
            ReadOnlyMemory<Area>.Empty,
            ReadOnlyMemory<Waypoint>.Empty,
            ReadOnlyMemory<int>.Empty,
            ReadOnlyMemory<int>.Empty));
        Assert.Throws<ArgumentException>(() => new WorldAtlas(
            ReadOnlyMemory<MapPosition>.Empty,
            ReadOnlyMemory<SubArea>.Empty,
            new Area[] { new(1, 1, 0), new(1, 2, 0) },
            ReadOnlyMemory<Waypoint>.Empty,
            ReadOnlyMemory<int>.Empty,
            ReadOnlyMemory<int>.Empty));
    }

    [Fact]
    public void ResourcesThatDoNotFollowOneAnotherAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Atlas(new(10, 1, 1, 1, SubArea.NoZaap, false, 0, 2, 0, 1), new(11, 1, 1, 1, SubArea.NoZaap, false, 1, 1, 1, 1)));
        Assert.Throws<ArgumentException>(() => Atlas(new(10, 1, 1, 1, SubArea.NoZaap, false, 0, 1, 0, 1), new(11, 1, 1, 1, SubArea.NoZaap, false, 1, 1, 1, 1)));
    }

    [Fact]
    public void MonstersThatDoNotFollowOneAnotherAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Atlas(new(10, 1, 1, 1, SubArea.NoZaap, false, 0, 1, 0, 2), new(11, 1, 1, 1, SubArea.NoZaap, false, 1, 2, 1, 1)));
        Assert.Throws<ArgumentException>(() => Atlas(new(10, 1, 1, 1, SubArea.NoZaap, false, 0, 1, 0, 1), new(11, 1, 1, 1, SubArea.NoZaap, false, 1, 2, 1, 0)));
    }

    [Fact]
    public void AnAtlasReadsBackFromAPack()
    {
        var atlas = TestWorld.Atlas();

        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add(WorldAtlas.EntryName, atlas)));

        var read = pack.Read<WorldAtlas>(WorldAtlas.EntryName);

        Assert.Equal(atlas.Positions.ToArray(), read.Positions.ToArray());
        Assert.Equal(atlas.SubAreas.ToArray(), read.SubAreas.ToArray());
        Assert.Equal(atlas.Areas.ToArray(), read.Areas.ToArray());
        Assert.Equal(atlas.Waypoints.ToArray(), read.Waypoints.ToArray());
        Assert.Equal(atlas.Harvestables.ToArray(), read.Harvestables.ToArray());
        Assert.Equal(atlas.Monsters.ToArray(), read.Monsters.ToArray());
    }

    /// <summary>An atlas of two sub-areas, three resources and two monsters.</summary>
    private static WorldAtlas Atlas(SubArea first, SubArea second)
    {
        return new WorldAtlas(ReadOnlyMemory<MapPosition>.Empty, new[] { first, second }, ReadOnlyMemory<Area>.Empty, ReadOnlyMemory<Waypoint>.Empty, _harvestables, _monsters);
    }
}
