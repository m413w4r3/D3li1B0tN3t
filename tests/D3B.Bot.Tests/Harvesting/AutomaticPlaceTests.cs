using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Tests;
using D3B.Data.World;

namespace D3B.Bot.Tests.Harvesting;

public sealed class AutomaticPlaceTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private IReadOnlyList<PlaceCandidate> Rank(HarvestSettings settings, int level, int lumberjack, GraphicSkills? graphics = null)
    {
        return AutomaticPlace.Rank(
            HarvestZone.TargetsOf(settings, _play.Tables.Jobs).Values,
            level,
            new Dictionary<int, int> { [TestPlay.Lumberjack] = lumberjack },
            settings.SafeLevel,
            settings.IsolatedMaps,
            _play.World.Atlas,
            _play.World.Graph,
            _play.Places,
            graphics ?? new GraphicSkills());
    }

    private static HarvestSettings Lumberjack(bool safeLevel = true)
    {
        return new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], Place = HarvestPlace.Automatic, SafeLevel = safeLevel };
    }

    [Fact]
    public void WhileNoGraphicIsLearnedASubAreaCountsTheWorthOfTheResourcesItLists()
    {
        var first = Assert.Single(Rank(Lumberjack(), 50, 1));
        var twenty = Assert.Single(Rank(Lumberjack(), 50, 20));

        // At its profession's first level, the ash alone, worth one; at the 20th, the chestnut too, the ash 19 levels below.
        Assert.Equal(TestPlay.SubAreaId, first.SubAreaId);
        Assert.Equal(1, first.Score, 3);
        Assert.Equal(1 + Math.Pow(2, -19.0 / AutomaticPlace.HalvingLevels), twenty.Score, 3);
        Assert.Equal(_play.World.Atlas.Positions.ToArray().Count(position => position.SubAreaId == TestPlay.SubAreaId && _play.World.Graph.Contains(position.MapId)), first.Maps);
    }

    [Fact]
    public void EachMapTheLearnedGraphicsPlaceAResourceOnCountsItsWorth()
    {
        var graphics = new GraphicSkills();

        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.TreeSkillId]);

        var placed = _play.Places.MapsOffering(new HashSet<int> { TestPlay.TreeSkillId }, graphics).Count;

        Assert.True(placed > 0);
        Assert.Equal(placed, Assert.Single(Rank(Lumberjack(), 50, 1, graphics)).Score, 3);
    }

    [Fact]
    public void ASubAreaAboveTheCharactersLevelIsLeftOutWhenTheSettingsSaySo()
    {
        Assert.Empty(Rank(Lumberjack(), 5, 1));
        Assert.Single(Rank(Lumberjack(safeLevel: false), 5, 1));
        Assert.Single(Rank(Lumberjack(), 0, 1));
    }

    [Fact]
    public void AnIsolatedMapCountsForNothingUnlessTheTourTakesIt()
    {
        // The forest's maps 1 to 3 hold together, map 9 lies five changes away through the grove's 4, 6, 7 and 8: the tree is
        // drawn on map 9 alone of the forest, and on the grove's map 4.
        const int forest = 10;
        const int grove = 20;
        long[] ids = [1, 2, 3, 4, 6, 7, 8, 9];
        int[] subAreaOf = [forest, forest, forest, grove, grove, grove, grove, forest];
        (int From, int To)[] links = [(0, 1), (1, 0), (1, 2), (2, 1), (2, 3), (3, 4), (4, 5), (5, 6), (6, 7)];
        var graph = new WorldGraph(
            ids.Select(id => new WorldVertex(id, 1)).ToArray(),
            links.Select((link, index) => new WorldEdge(link.From, link.To, index, 1)).ToArray(),
            links.Select(link => new WorldTransition(WorldTransitionType.Scroll, 0, 0, WorldTransition.None, ids[link.To], WorldTransition.None, WorldTransition.None)).ToArray(),
            Array.Empty<string>());
        var atlas = new WorldAtlas(
            ids.Select((id, index) => new MapPosition(id, (short)index, 0, subAreaOf[index], 1, 0, 0)).ToArray(),
            new[] { new SubArea(forest, 0, 1, 1, SubArea.NoZaap, true, 0, 0, 0, 0), new SubArea(grove, 0, 1, 1, SubArea.NoZaap, true, 0, 0, 0, 0) },
            new[] { new Area(1, 0, 0) },
            Array.Empty<Waypoint>(),
            Array.Empty<int>(),
            Array.Empty<int>());
        var info = new PackInfo { ClientVersion = TestInstall.Version, Fingerprint = "0123456789abcdef", ExtractorVersion = "0.0.0-test" };
        var cells = new MapCell[MapData.CellCount];

        using var directory = new TemporaryDirectory();
        var path = directory.Combine("data-3.pack");

        using (var stream = File.Create(path))
        using (var writer = new PackWriter(stream, info, 3))
        {
            writer.Add(WorldGraph.EntryName, graph);
            writer.Add(WorldAtlas.EntryName, atlas);

            foreach (var id in ids)
            {
                MapInteractive[] interactives = id is 4 or 9 ? [new MapInteractive(1, 300, TestPlay.TreeGfxId)] : [];

                writer.Add(MapData.EntryName(id), new MapData(id, 0, 0, 0, 0, cells, interactives));
            }

            writer.Complete();
        }

        using var pack = GamePack.Open(path);
        var world = new GameWorld(pack);
        var places = new GraphicPlaces(pack, world);
        var graphics = new GraphicSkills();
        var settings = Lumberjack(safeLevel: false);

        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.TreeSkillId]);

        IReadOnlyList<PlaceCandidate> Ranked(bool isolatedMaps)
        {
            return AutomaticPlace.Rank(HarvestZone.TargetsOf(settings, _play.Tables.Jobs).Values, 50, new Dictionary<int, int> { [TestPlay.Lumberjack] = 1 }, false, isolatedMaps, atlas, graph, places, graphics);
        }

        Assert.Equal([grove], Ranked(isolatedMaps: false).Select(candidate => candidate.SubAreaId));
        Assert.Equal([forest, grove], Ranked(isolatedMaps: true).Select(candidate => candidate.SubAreaId));
    }

    [Fact]
    public void NothingTheCharacterCanHarvestAtItsLevelsLeavesNoPlace()
    {
        Assert.Empty(Rank(new HarvestSettings { Resources = [TestPlay.Chestnut], Place = HarvestPlace.Automatic }, 50, 1));
    }
}
