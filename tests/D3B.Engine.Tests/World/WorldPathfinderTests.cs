using System.Collections.Frozen;
using CsCheck;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Criteria;
using D3B.Engine.Maps;
using D3B.Engine.World;

namespace D3B.Engine.Tests.World;

public sealed class WorldPathfinderTests
{
    private const int East = 0;
    private const int South = 2;
    private const int West = 4;
    private const int North = 6;

    private static readonly WorldPathRules _subscribed = new() { Facts = new CriterionFacts { Level = 50, Subscribed = true } };

    // Nine maps, 1 to 9, three rows of three, each a scroll from its neighbours.
    private static TestWorld Grid()
    {
        var world = new TestWorld();

        for (var map = 1; map <= 9; map++)
        {
            if (map % 3 is not 0)
                world.Scroll(map, map + 1, East).Scroll(map + 1, map, West);

            if (map <= 6)
                world.Scroll(map, map + 3, South).Scroll(map + 3, map, North);
        }

        return world;
    }

    private static int Vertex(WorldPathfinder pathfinder, long map, int zone = 1)
    {
        Assert.True(pathfinder.Graph.TryGetVertex(map, zone, out var vertex));

        return vertex;
    }

    private static long[] Maps(WorldPathfinder pathfinder, WorldPath path)
    {
        return [pathfinder.Graph.Vertices.Span[path.From].MapId, .. path.Steps.Select(step => pathfinder.Graph.Vertices.Span[step.To].MapId)];
    }

    private static MapCell Cell(int zone, int mapChange = 0, bool walkable = true)
    {
        return new MapCell(walkable ? MapCellProperties.Walkable : MapCellProperties.None, 0, 0, mapChange, 0, zone << 4, 0);
    }

    // Every cell walkable in zone 1, those of the edges marked as leaving straight across them.
    private static MapCell[] EdgeCells()
    {
        return [.. Enumerable.Range(0, MapData.CellCount).Select(cell =>
        {
            var bits = 0;

            foreach (var direction in new[] { East, South, West, North })
            {
                if (MapGeometry.Neighbour(cell, direction) < 0)
                    bits |= 1 << direction;
            }

            return Cell(1, bits);
        })];
    }

    private static MapData MapOf(MapCell[] cells, params MapInteractive[] interactives)
    {
        return new MapData(1, 0, 0, 0, 0, cells, interactives);
    }

    private static WorldStep StepOf(WorldPathfinder pathfinder, long from, long to)
    {
        Assert.True(pathfinder.TryFind(Vertex(pathfinder, from), new HashSet<long> { to }, _subscribed, out var path));

        return Assert.Single(path.Steps);
    }

    private static WorldPathRules Teleporting(long kamas, params long[] zaaps)
    {
        return _subscribed with { KnownZaaps = zaaps.ToHashSet(), Kamas = kamas };
    }

    private static WorldStep StepFrom(WorldPathfinder pathfinder, long from, int cell)
    {
        var vertex = Vertex(pathfinder, from);

        foreach (var edge in pathfinder.Graph.EdgesFrom(vertex))
        {
            for (var i = 0; i < edge.TransitionCount; i++)
            {
                var transition = pathfinder.Graph.Transitions.Span[edge.FirstTransition + i];

                if (transition.CellId == cell)
                    return new WorldStep(vertex, edge.To, edge.FirstTransition + i, transition);
            }
        }

        throw new InvalidOperationException($"No transition leaves map {from} from cell {cell}.");
    }

    private static int? BreadthFirst(List<(int From, int To)> edges, int from, int to)
    {
        var distance = new Dictionary<int, int> { [from] = 0 };
        var queue = new Queue<int>([from]);

        while (queue.TryDequeue(out var map))
        {
            if (map == to)
                return distance[map];

            foreach (var (_, next) in edges.Where(edge => edge.From == map))
            {
                if (distance.TryAdd(next, distance[map] + 1))
                    queue.Enqueue(next);
            }
        }

        return null;
    }

    [Fact]
    public void APathHasTheFewestMapChanges()
    {
        var pathfinder = Grid().Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, _subscribed, out var path));

        Assert.Equal(4, path.Steps.Count);
        Assert.Equal(1L, Maps(pathfinder, path)[0]);
        Assert.Equal(9L, Maps(pathfinder, path)[^1]);
        Assert.All(path.Steps, step => Assert.Equal(WorldTransitionType.Scroll, step.Transition.Type));
    }

    [Fact]
    public void AMapActionShortensThePath()
    {
        var pathfinder = Grid().MapAction(1, 9, 300).Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, _subscribed, out var path));

        var step = Assert.Single(path.Steps);
        Assert.Equal(WorldTransitionType.MapAction, step.Transition.Type);
        Assert.Equal(300, step.Transition.CellId);
    }

    [Fact]
    public void AScrollWithAnActionLeadsWhereTheServerSendsTheCharacter()
    {
        // The client asks for map 2, its neighbour, and lands on map 5.
        var pathfinder = new TestWorld().ScrollAction(1, 2, 5, East, 27).Pathfinder();

        var step = StepOf(pathfinder, 1, 5);

        Assert.Equal(2L, step.Transition.MapId);
        Assert.Equal(5L, pathfinder.Graph.Vertices.Span[step.To].MapId);
    }

    [Fact]
    public void ADoorIsFollowedAndEveryOtherInteractiveAsADoorIs()
    {
        // Stairs, portals and "go to"s have skills of their own, used as a door's is (D248).
        var pathfinder = new TestWorld().Interactive(1, 2, 288).Interactive(1, 3, 300, skill: 469).Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 2 }, _subscribed, out var path));
        Assert.Equal(WorldTransitionType.Interactive, Assert.Single(path.Steps).Transition.Type);
        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 3 }, _subscribed, out var portal));
        Assert.Equal(469, Assert.Single(portal.Steps).Transition.SkillId);
    }

    [Fact]
    public void ADoorWhoseCriterionIsNotKnownIsNotFollowed()
    {
        var pathfinder = new TestWorld().Interactive(1, 2, 288, criterion: "Qf=1201").Pathfinder();

        Assert.False(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 2 }, _subscribed, out _));
    }

    [Theory]
    [InlineData("PL>49", true)]
    [InlineData("PL>50", false)]
    [InlineData("Qf=2450", false)]
    [InlineData("PL>49|Qf=2450", true)]
    [InlineData("PL>49&Qf=2450", false)]
    public void ATransitionIsFollowedOnlyWhenItsCriterionIsMet(string criterion, bool followed)
    {
        var pathfinder = new TestWorld().MapAction(1, 2, 300, criterion).Pathfinder();

        Assert.Equal(followed, pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 2 }, _subscribed, out _));
    }

    [Fact]
    public void ACriterionIsAnsweredOnTheMapItsTransitionLeaves()
    {
        var pathfinder = new TestWorld().MapAction(1, 2, 300, "Pm=1").MapAction(2, 3, 300, "Pm=1").Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 2 }, _subscribed, out _));
        Assert.False(pathfinder.TryFind(Vertex(pathfinder, 2), new HashSet<long> { 3 }, _subscribed, out _));

        var placed = new TestWorld().MapAction(1, 2, 300, $"PB={TestWorld.SubAreaId}&Po={TestWorld.AreaId}").Pathfinder();

        Assert.True(placed.TryFind(Vertex(placed, 1), new HashSet<long> { 2 }, _subscribed, out _));
    }

    [Fact]
    public void ATransitionBannedIsAvoided()
    {
        var pathfinder = Grid().Pathfinder();
        var first = StepOf(pathfinder, 1, 2);
        var rules = _subscribed with { Banned = new HashSet<int> { first.TransitionIndex } };

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 2 }, rules, out var path));
        Assert.Equal(new long[] { 1, 4, 5, 2 }, Maps(pathfinder, path));
    }

    [Fact]
    public void ASubAreaClosedToAccountsWithoutASubscriptionIsEnteredOnlyWhenSubscribed()
    {
        var pathfinder = Grid().Map(2, TestWorld.ClosedSubAreaId).Pathfinder();
        var from = Vertex(pathfinder, 1);
        var target = new HashSet<long> { 3 };

        Assert.True(pathfinder.TryFind(from, target, _subscribed, out var subscribed));
        Assert.Equal(2, subscribed.Steps.Count);

        foreach (var facts in new[] { new CriterionFacts { Subscribed = false }, new CriterionFacts() })
        {
            Assert.True(pathfinder.TryFind(from, target, new WorldPathRules { Facts = facts }, out var basic));
            Assert.DoesNotContain(2L, Maps(pathfinder, basic));
            Assert.False(pathfinder.TryFind(from, new HashSet<long> { 2 }, new WorldPathRules { Facts = facts }, out _));
        }
    }

    [Fact]
    public void AMapSoughtAlreadyNeedsNoStep()
    {
        var pathfinder = Grid().Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 5), new HashSet<long> { 5 }, _subscribed, out var path));
        Assert.Empty(path.Steps);
        Assert.Equal(path.From, path.To);
    }

    [Fact]
    public void TheNearestOfSeveralMapsIsReached()
    {
        var pathfinder = Grid().Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9, 3 }, _subscribed, out var path));
        Assert.Equal(3L, Maps(pathfinder, path)[^1]);
    }

    [Fact]
    public void AMapWithNoWayInCannotBeReached()
    {
        var pathfinder = Grid().Scroll(10, 1, West).Pathfinder();

        Assert.False(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 10 }, _subscribed, out _));
        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 10), new HashSet<long> { 9 }, _subscribed, out _));
    }

    [Fact]
    public void TheVertexIsTheZoneOfTheCellTheCharacterStandsOn()
    {
        var pathfinder = new TestWorld().Scroll(1, 2, East, zone: 2).Map(1).Pathfinder();
        var cells = Enumerable.Range(0, MapData.CellCount).Select(cell => Cell(cell < 280 ? 1 : 2)).ToArray();
        var map = new MapData(1, 0, 0, 0, 0, cells, Array.Empty<MapInteractive>());

        Assert.True(pathfinder.TryGetVertex(map, 300, out var vertex));
        Assert.Equal(new WorldVertex(1, 2), pathfinder.Graph.Vertices.Span[vertex]);
        Assert.True(pathfinder.TryGetVertex(map, 10, out vertex));
        Assert.Equal(1, pathfinder.Graph.Vertices.Span[vertex].Zone);
    }

    [Fact]
    public void EveryPathHasTheFewestMapChangesABreadthFirstSearchFinds()
    {
        var edge = Gen.Select(Gen.Int[1, 12], Gen.Int[1, 12], Gen.Int[0, 3]);

        Gen.Select(edge.Array[0, 40], Gen.Int[1, 12], Gen.Int[1, 12]).Sample((edges, from, to) =>
        {
            var world = new TestWorld();

            for (var map = 1; map <= 12; map++)
                world.Map(map);

            foreach (var (source, destination, kind) in edges.Where(e => e.Item1 != e.Item2))
            {
                if (kind is 0)
                    world.MapAction(source, destination, 300);
                else
                    world.Scroll(source, destination, 2 * kind);
            }

            var pathfinder = world.Pathfinder();
            var fewest = BreadthFirst(edges.Where(e => e.Item1 != e.Item2).Select(e => (e.Item1, e.Item2)).ToList(), from, to);

            if (!pathfinder.TryFind(Vertex(pathfinder, from), new HashSet<long> { to }, _subscribed, out var path))
                return fewest is null;

            var maps = Maps(pathfinder, path);

            return fewest == path.Steps.Count && maps[0] == from && maps[^1] == to;
        }, iter: 200);
    }

    [Fact]
    public void AScrollIsLeftFromTheCellsOfItsEdge()
    {
        var pathfinder = new TestWorld().Scroll(1, 2, East, 13).Pathfinder();
        var exits = pathfinder.ExitCells(MapOf(EdgeCells()), StepOf(pathfinder, 1, 2), _subscribed);

        Assert.Equal(MapGeometry.Rows, exits.Count);
        Assert.All(exits, cell => Assert.Equal(MapGeometry.Columns - 1, cell % MapGeometry.Columns));
    }

    [Fact]
    public void AnEdgeCellMarkedAlongADiagonalNextToTheDirectionLeavesTooButNotOneUnmarked()
    {
        // Cell 447 left east though the client marks it south-east only (client.md).
        var cells = Enumerable.Range(0, MapData.CellCount).Select(_ => Cell(1)).ToArray();

        cells[447] = Cell(1, 1 << 1);
        cells[475] = Cell(1, 1 << 7);
        cells[503] = Cell(1, 1 << 2);
        cells[300] = Cell(1, 1 << East);

        var pathfinder = new TestWorld().Scroll(1, 2, East, 41).Pathfinder();
        var exits = pathfinder.ExitCells(MapOf(cells), StepOf(pathfinder, 1, 2), _subscribed);

        // Cell 41, the transition's own, leaves though unmarked; 300 is marked but not on the edge.
        Assert.Equal([41, 447, 475], exits);
    }

    [Fact]
    public void ACornerLeavesOnlyByTheEdgeItIsMarkedStraightFor()
    {
        // Cell 546, south-west corner, marked as a south exit: the server ignored a request for the west from it (client.md).
        var cells = Enumerable.Range(0, MapData.CellCount).Select(_ => Cell(1)).ToArray();

        cells[546] = Cell(1, (1 << 1) | (1 << 2) | (1 << 3));
        cells[518] = Cell(1, 1 << 3);
        cells[504] = Cell(1, 1 << West);

        var pathfinder = new TestWorld().Scroll(1, 2, West, 322).Scroll(1, 3, South, 538).Pathfinder();

        Assert.Equal([322, 504, 518], pathfinder.ExitCells(MapOf(cells), StepOf(pathfinder, 1, 2), _subscribed));
        Assert.Equal([538, 546], pathfinder.ExitCells(MapOf(cells), StepOf(pathfinder, 1, 3), _subscribed));
    }

    [Fact]
    public void OnlyWalkableCellsOfTheVertexsZoneLeave()
    {
        var cells = EdgeCells();

        cells[13] = Cell(1, 1 << East, walkable: false);

        for (var cell = 279; cell < MapData.CellCount; cell++)
            cells[cell] = Cell(2, cells[cell].MapChangeData);

        var pathfinder = new TestWorld().Scroll(1, 2, East, 27).Pathfinder();
        var exits = pathfinder.ExitCells(MapOf(cells), StepOf(pathfinder, 1, 2), _subscribed);

        Assert.DoesNotContain(13, exits);
        Assert.All(exits, cell => Assert.True(cell < 279));
        Assert.NotEmpty(exits);
    }

    [Fact]
    public void ACriterionOnTheCellNamesTheCellsAScrollWithAnActionIsLeftFrom()
    {
        var pathfinder = new TestWorld().ScrollAction(1, 2, 5, East, 27, "#(PC=27|PC=55|PC=83)").Pathfinder();
        var exits = pathfinder.ExitCells(MapOf(EdgeCells()), StepOf(pathfinder, 1, 5), _subscribed);

        Assert.Equal([27, 55, 83], exits);
    }

    [Fact]
    public void ScrollsInOneDirectionShareTheEdgeByTheNearestCell()
    {
        // Two scrolls east, to two zones of map 2: the north half of the edge leads to the first, the south half to the second.
        var pathfinder = new TestWorld().Scroll(1, 2, East, 27, toZone: 1).Scroll(1, 2, East, 531, toZone: 2).Pathfinder();
        var north = pathfinder.ExitCells(MapOf(EdgeCells()), StepFrom(pathfinder, 1, 27), _subscribed);
        var south = pathfinder.ExitCells(MapOf(EdgeCells()), StepFrom(pathfinder, 1, 531), _subscribed);

        Assert.Contains(27, north);
        Assert.Contains(531, south);
        Assert.All(north, cell => Assert.True(cell < 280));
        Assert.All(south, cell => Assert.True(cell >= 280));
        Assert.Equal(MapGeometry.Rows, north.Count + south.Count);
    }

    [Fact]
    public void ADoorIsUsedFromTheWalkableNeighboursOfItsElement()
    {
        // The door of the bank of Astrub: element 515308 on cell 288, of which 273, 260, 274 and 289 cannot be walked on.
        var cells = Enumerable.Range(0, MapData.CellCount).Select(cell => Cell(1, walkable: cell is not (273 or 260 or 274 or 289))).ToArray();
        var pathfinder = new TestWorld().Interactive(1, 2, 288).Pathfinder();

        Assert.Equal([302, 316, 301, 287], pathfinder.ExitCells(MapOf(cells, new MapInteractive(515308, 288, 0)), StepOf(pathfinder, 1, 2), _subscribed));
    }

    [Fact]
    public void ADoorIsUsedAroundItsElementWhereverTheTransitionPutsIt()
    {
        var pathfinder = new TestWorld().Interactive(1, 2, 288).Pathfinder();
        var exits = pathfinder.ExitCells(MapOf(EdgeCells(), new MapInteractive(515308, 300, 0)), StepOf(pathfinder, 1, 2), _subscribed);

        Assert.Equal(8, exits.Count);
        Assert.All(exits, cell => Assert.NotEqual(-1, MapGeometry.Direction(300, cell)));
    }

    [Fact]
    public void ADoorsCriterionOnTheCellKeepsTheCellsThatMeetIt()
    {
        var pathfinder = new TestWorld().Interactive(1, 2, 288, criterion: "PC=287|PC=301").Pathfinder();

        Assert.Equal([301, 287], pathfinder.ExitCells(MapOf(EdgeCells(), new MapInteractive(515308, 288, 0)), StepOf(pathfinder, 1, 2), _subscribed));
    }

    [Fact]
    public void AMapActionIsLeftFromItsCell()
    {
        var pathfinder = new TestWorld().MapAction(1, 2, 409).Pathfinder();

        Assert.Equal([409], pathfinder.ExitCells(MapOf(EdgeCells()), StepOf(pathfinder, 1, 2), _subscribed));
    }

    [Theory]
    [InlineData(5, -18, 1, 1, -32, 1, 140)]
    [InlineData(1, -31, 1, 5, -18, 1, 130)]
    [InlineData(4, -18, -1, 1, -32, 1, 140)]
    [InlineData(35, 12, 1, 13, 26, 1, 260)]
    [InlineData(5, -18, 1, 2, -5, 2, 1_130)]
    public void ATeleportCostsTenKamasAWholeCoordinateAndAThousandMoreTowardsAnotherWorldMap(int x, int y, int worldMap, int toX, int toY, int toWorldMap, long kamas)
    {
        // Measured: Astrub's zaap, a map beside Tainéla's, the bank of Astrub's inside, the Turtle beach's, to the zaaps of
        // Tainéla, Astrub, Sufokia and Incarnam's pastures.
        var from = new MapPosition(1, (short)x, (short)y, 0, (sbyte)worldMap, 0, 0);
        var to = new MapPosition(2, (short)toX, (short)toY, 0, (sbyte)toWorldMap, 0, 0);

        Assert.Equal(kamas, WorldPathfinder.TeleportCost(from, to));
    }

    [Fact]
    public void ASubscribedCharacterTeleportsToAZaapItKnowsWhenItSavesMapChanges()
    {
        var pathfinder = Grid().Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, Teleporting(0, 9), out var path));

        var step = Assert.Single(path.Steps);

        Assert.True(step.IsTeleport);
        Assert.Equal(Vertex(pathfinder, 1), step.From);
        Assert.Equal(Vertex(pathfinder, 9), step.To);
        Assert.Empty(pathfinder.ExitCells(MapOf(EdgeCells()), step, _subscribed));
    }

    [Fact]
    public void NoTeleportIsTakenFromAMapTheServerRefusedOneFrom()
    {
        var pathfinder = Grid().Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, Teleporting(0, 9) with { NoTeleportFrom = new HashSet<long> { 1 } }, out var path));
        Assert.DoesNotContain(path.Steps, step => step.IsTeleport && step.From == Vertex(pathfinder, 1));
    }

    [Fact]
    public void AZaapNearTheMapSoughtIsTeleportedToThenWalkedFrom()
    {
        var pathfinder = Grid().Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, Teleporting(0, 8), out var path));

        Assert.Equal([1, 8, 9], Maps(pathfinder, path));
        Assert.True(path.Steps[0].IsTeleport);
        Assert.False(path.Steps[1].IsTeleport);
    }

    [Fact]
    public void ACharacterWalksWhenTeleportingSavesNothing()
    {
        var pathfinder = Grid().Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 2 }, Teleporting(0, 2), out var path));

        Assert.False(Assert.Single(path.Steps).IsTeleport);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void AZaapIsTakenOnlyWhenItSavesAsManyChangesAsATeleportCounts(int changes, bool teleports)
    {
        var pathfinder = Grid().Pathfinder();

        // Walked, 1 to 9 is 4 changes: the zaap of 9 saves 3.
        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, Teleporting(0, 9) with { TeleportChanges = changes }, out var path));

        Assert.Equal(teleports, path.Steps.Any(step => step.IsTeleport));
        Assert.Equal(teleports ? 1 : 4, path.Steps.Count);
    }

    [Fact]
    public void ATeleportCountsAsOneChangeAtLeast()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _subscribed with { TeleportChanges = 0 });
    }

    [Fact]
    public void AnAccountWithoutASubscriptionDoesNotTeleport()
    {
        var pathfinder = Grid().Pathfinder();
        var rules = Teleporting(0, 9) with { Facts = new CriterionFacts { Level = 50, Subscribed = false } };

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, rules, out var path));

        Assert.Equal(4, path.Steps.Count);
        Assert.DoesNotContain(path.Steps, step => step.IsTeleport);
    }

    [Fact]
    public void ATeleportTheCharacterCannotPayIsLeftOut()
    {
        var pathfinder = Grid().Place(9, 30, 0).Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, Teleporting(299, 9), out var walked));
        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, Teleporting(300, 9), out var teleported));

        Assert.Equal(4, walked.Steps.Count);
        Assert.True(Assert.Single(teleported.Steps).IsTeleport);
    }

    [Fact]
    public void NoOneTeleportsFromAMapThatForbidsIt()
    {
        var pathfinder = Grid().Place(1, 0, 0, teleport: false).Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, Teleporting(0, 9), out var path));

        Assert.Equal([1, 2, 9], Maps(pathfinder, path));
        Assert.True(path.Steps[1].IsTeleport);
    }

    [Fact]
    public void AZaapsMapWithSeveralZonesIsNotTeleportedTo()
    {
        // Where the character lands on such a map is not measured.
        var pathfinder = Grid().Scroll(6, 9, South, toZone: 2).Pathfinder();

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, Teleporting(0, 9), out var path));

        Assert.DoesNotContain(path.Steps, step => step.IsTeleport);
    }

    // Maps 10 and 11 hold together, and nothing of the graph leads from them to the grid: an NPC's dialog on 11 leads to 1,
    // as Incarnam's portal leads to Astrub (D253).
    private static WorldPathfinder Island(params NpcWay[] ways)
    {
        return Grid().Scroll(10, 11, East).Scroll(11, 10, West).Pathfinder(ways);
    }

    private static NpcWay WayOf(long from, long to)
    {
        return new NpcWay(from, 4398, new HashSet<int> { 36977 }.ToFrozenSet(), to);
    }

    [Fact]
    public void AnNpcsWayLeadsWhereTheGraphHoldsNone()
    {
        var pathfinder = Island(WayOf(11, 1));

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 10), new HashSet<long> { 9 }, _subscribed, out var path));

        Assert.Equal([10, 11, 1], Maps(pathfinder, path)[..3]);
        Assert.Equal(6, path.Steps.Count);

        var step = path.Steps[1];

        Assert.True(step.IsNpcWay);
        Assert.False(step.IsTeleport);
        Assert.Equal(0, step.NpcWay);
        Assert.Equal(Vertex(pathfinder, 11), step.From);
        Assert.Equal(Vertex(pathfinder, 1), step.To);
        Assert.Empty(pathfinder.ExitCells(MapOf(EdgeCells()), step, _subscribed));
        Assert.False(Island().TryFind(Vertex(pathfinder, 10), new HashSet<long> { 9 }, _subscribed, out _));
    }

    [Fact]
    public void AnNpcsWayCostsAMapChangeAsAScrollDoes()
    {
        var pathfinder = Grid().Pathfinder(WayOf(1, 9));

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 9 }, _subscribed, out var through));
        Assert.True(Assert.Single(through.Steps).IsNpcWay);
        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 1), new HashSet<long> { 2 }, _subscribed, out var walked));
        Assert.False(Assert.Single(walked.Steps).IsNpcWay);
    }

    [Fact]
    public void AnNpcsWayBannedOrRefusedIsNotTaken()
    {
        var pathfinder = Island(WayOf(11, 1));
        var from = Vertex(pathfinder, 10);
        HashSet<long> sought = [9];

        Assert.False(pathfinder.TryFind(from, sought, _subscribed with { Banned = new HashSet<int> { WorldStep.OfNpcWay(0) } }, out _));
        Assert.False(pathfinder.TryFind(from, sought, _subscribed with { NoNpcWays = new HashSet<int> { 0 } }, out _));
        Assert.True(pathfinder.TryFind(from, sought, _subscribed with { NoNpcWays = new HashSet<int> { 1 } }, out _));
    }

    [Fact]
    public void AnNpcsWayToAMapOfSeveralZonesOrOffTheGraphIsNeverTaken()
    {
        // Where the dialog lands the character on such a map is not measured.
        var pathfinder = Grid().Scroll(10, 11, East).Scroll(11, 10, West).Scroll(6, 9, South, toZone: 2)
            .Pathfinder(WayOf(11, 9), WayOf(11, 99), WayOf(98, 1));

        Assert.False(pathfinder.TryFind(Vertex(pathfinder, 10), new HashSet<long> { 9, 1 }, _subscribed, out _));
        Assert.Equal(3, pathfinder.NpcWays.Count);
    }

    [Fact]
    public void AnNpcsWayIntoASubAreaClosedToAccountsWithoutASubscriptionIsTakenOnlyWhenSubscribed()
    {
        var pathfinder = Grid().Map(1, TestWorld.ClosedSubAreaId).Scroll(10, 11, East).Scroll(11, 10, West).Pathfinder(WayOf(11, 1));

        Assert.True(pathfinder.TryFind(Vertex(pathfinder, 10), new HashSet<long> { 1 }, _subscribed, out _));
        Assert.False(pathfinder.TryFind(Vertex(pathfinder, 10), new HashSet<long> { 1 }, new WorldPathRules { Facts = new CriterionFacts { Subscribed = false } }, out _));
    }

    [Fact]
    public void AnNpcsWayIsNamedByAnIndexBelowATeleports()
    {
        Assert.Equal(WorldStep.FirstNpcWay, WorldStep.OfNpcWay(0));
        Assert.Equal(3, new WorldStep(0, 1, WorldStep.OfNpcWay(3), default).NpcWay);
        Assert.False(new WorldStep(0, 1, WorldStep.Teleport, default).IsNpcWay);
        Assert.False(new WorldStep(0, 1, 0, default).IsNpcWay);
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldStep.OfNpcWay(-1));
    }
}
