using CsCheck;
using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.Engine.Tests.Maps;

public sealed class CellPathfinderTests
{
    // Seven cells in ten walkable, on floors 0 to 30; on half the maps, a cell in ten of a stair, so that floors count.
    private static readonly Gen<MapCell[]> _maps = Gen.Select(
        Gen.Int[0, 9].Select(roll => roll < 7).Array[MapData.CellCount],
        Gen.Int[0, 3].Select(floor => floor * 10).Array[MapData.CellCount],
        Gen.Int[0, 9].Array[MapData.CellCount],
        Gen.Bool,
        (walkable, floors, rolls, stairs) => TestCells.Of(walkable, floors, [.. rolls.Select(roll => stairs && roll is 0 ? 1 : 0)]));

    [Fact]
    public void APathLeadsFromCellToCellOverWalkableNeighbours()
    {
        var cells = TestCells.OpenBut(200, 201, 214, 215);

        Assert.True(CellPathfinder.TryFind(cells, 186, 243, out var path));
        Assert.Equal(186, path.Start);
        Assert.Equal(243, path.End);
        Assert.All(path.Cells.ToArray().Skip(1), cell => Assert.True(cells[cell].IsWalkable));
    }

    [Fact]
    public void ACellThatCannotBeWalkedOnIsNoDestination()
    {
        Assert.False(CellPathfinder.TryFind(TestCells.OpenBut(300), 100, 300, out _));
    }

    [Fact]
    public void ACellWalledInCannotBeReached()
    {
        const int inside = 20 * 14 + 6;
        var walls = Enumerable.Range(0, 8).Select(direction => MapGeometry.Neighbour(inside, direction)).ToArray();

        Assert.False(CellPathfinder.TryFind(TestCells.OpenBut(walls), 10, inside, out _));
    }

    [Fact]
    public void TheWalkMayStartOnACellThatCannotBeWalkedOn()
    {
        Assert.True(CellPathfinder.TryFind(TestCells.OpenBut(100), 100, 160, out var path));
        Assert.Equal(100, path.Start);
    }

    [Fact]
    public void AWalkThatStartsWhereItEndsHasNoStep()
    {
        Assert.True(CellPathfinder.TryFind(TestCells.OpenBut(), 100, 100, out var path));
        Assert.Equal(0, path.Steps);
    }

    [Fact]
    public void AStepAcrossNeverPassesBetweenTwoCellsThatCannotBeWalkedOn()
    {
        // From an even row, east passes between the north-east and south-east neighbours: with both blocked, the walk
        // goes round.
        const int from = 10 * 14 + 5;
        var cells = TestCells.OpenBut(from - 14, from + 14);

        Assert.True(CellPathfinder.TryFind(cells, from, from + 1, out var path));
        Assert.True(path.Steps > 1);

        Assert.True(CellPathfinder.TryFind(TestCells.OpenBut(from - 14), from, from + 1, out var across));
        Assert.Equal([from, from + 1], across.Cells.ToArray());
    }

    [Fact]
    public void StepsAreWeighedByHowLongTheCharacterTakes()
    {
        // Two cells east: one step east costs 3, against 2 for each of the two diagonals around it.
        const int from = 10 * 14 + 5;

        Assert.True(CellPathfinder.TryFind(TestCells.OpenBut(), from, from + 2, out var east));
        Assert.Equal(2, east.Steps);
        Assert.Equal(6, Cost(east));

        // North, two rows up, costs 2 against 4 for two diagonals.
        Assert.True(CellPathfinder.TryFind(TestCells.OpenBut(), from, from - 28, out var north));
        Assert.Equal([from, from - 28], north.Cells.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OnAMapWithStairsAStepOffOneChangesFloorByTenAtMost(int moveZone)
    {
        // West of column 7 lies floor 0; from it, a plateau of floor 20 east, of moveZone 0 or a stair's, is reached only
        // through cells of floor 10. A stair elsewhere makes floors count.
        const int from = 10 * 14 + 2;
        const int to = 10 * 14 + 10;

        Assert.False(CellPathfinder.TryFind(Terraced(column => column < 7 ? (0, 0) : (20, moveZone)), from, to, out _));

        var cells = Terraced(column => column switch
        {
            < 7 => (0, 0),
            7 => (10, moveZone),
            _ => (20, moveZone),
        });

        Assert.True(CellPathfinder.TryFind(cells, from, to, out var path));
        Assert.All(path.Cells.ToArray().Zip(path.Cells.ToArray().Skip(1)), step => Assert.True(Math.Abs(cells[step.First].Floor - cells[step.Second].Floor) <= 10));
    }

    [Fact]
    public void AlongAStairAStepChangesFloorAsMuchAsItDoes()
    {
        // Columns 6 and 7 of rows 9 to 11 are a stair from floor 0 to floor 130, the only way up.
        const int from = 10 * 14 + 2;
        const int to = 10 * 14 + 10;
        var cells = Terraced(column => column < 7 ? (0, 0) : (130, 0));

        foreach (var row in (int[])[9, 10, 11])
        {
            cells[row * 14 + 6] = TestCells.At(0, 1);
            cells[row * 14 + 7] = TestCells.At(130, 1);
        }

        Assert.True(CellPathfinder.TryFind(cells, from, to, out var path));
        Assert.Contains(path.Cells.ToArray().Zip(path.Cells.ToArray().Skip(1)), step => cells[step.First].Floor != cells[step.Second].Floor);
    }

    [Fact]
    public void OnAMapWithoutStairsFloorsDoNotCount()
    {
        var cells = TestCells.OpenBut();

        cells[10 * 14 + 6] = TestCells.At(170, 0);

        Assert.True(CellPathfinder.TryFind(cells, 10 * 14 + 5, 10 * 14 + 6, out var path));
        Assert.Equal(1, path.Steps);
    }

    [Fact]
    public void AMapWithoutEveryCellIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CellPathfinder.TryFind(new MapCell[10], 0, 1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CellPathfinder.TryFind(TestCells.OpenBut(), 0, MapData.CellCount, out _));
    }

    [Fact]
    public void EveryPathFoundIsValidAndAsQuickAsAnExhaustiveSearchFinds()
    {
        var cell = Gen.Int[0, MapData.CellCount - 1];

        Gen.Select(_maps, cell, cell).Sample((cells, from, to) =>
        {
            var quickest = Exhaustive(cells, from, to);

            if (!CellPathfinder.TryFind(cells, from, to, out var path))
                return quickest is null;

            var steps = path.Cells.ToArray();
            var stairs = HasStairs(cells);

            return quickest == Cost(path) && steps[0] == from && steps[^1] == to
                && steps.Skip(1).All(step => cells[step].IsWalkable)
                && steps.Zip(steps.Skip(1)).All(step => MayStep(cells, stairs, step.First, MapGeometry.Direction(step.First, step.Second)));
        }, iter: 100);
    }

    [Fact]
    public void TheNearestOfSeveralCellsIsTheOneReachedQuickest()
    {
        // Two rows north costs 2, two cells east 6.
        const int from = 10 * 14 + 5;

        Assert.True(CellPathfinder.TryFindNearest(TestCells.OpenBut(), from, [from + 2, from - 28], out var path));
        Assert.Equal(from - 28, path.End);
    }

    [Fact]
    public void CellsThatCannotBeWalkedOnAreNoDestinationsButTheStart()
    {
        Assert.False(CellPathfinder.TryFindNearest(TestCells.OpenBut(300, 301), 100, [300, 301], out _));
        Assert.False(CellPathfinder.TryFindNearest(TestCells.OpenBut(), 100, [], out _));

        Assert.True(CellPathfinder.TryFindNearest(TestCells.OpenBut(100), 100, [300, 100], out var path));
        Assert.Equal(0, path.Steps);
    }

    [Fact]
    public void EveryPathToTheNearestCellIsAsQuickAsTheQuickestOfThem()
    {
        var cell = Gen.Int[0, MapData.CellCount - 1];

        Gen.Select(_maps, cell, cell.Array[1, 6]).Sample((cells, from, destinations) =>
        {
            var quickest = destinations.Select(to => Exhaustive(cells, from, to)).Where(cost => cost is not null).Min();

            if (!CellPathfinder.TryFindNearest(cells, from, destinations, out var path))
                return quickest is null;

            return quickest == Cost(path) && path.Start == from && destinations.Contains(path.End);
        }, iter: 100);
    }

    private static MapCell[] Terraced(Func<int, (int Floor, int MoveZone)> byColumn)
    {
        // Cell 0, in column 0 and of a stair, keeps floors counting on the map whatever the columns hold.
        return [.. Enumerable.Range(0, MapData.CellCount).Select(cell =>
        {
            var (floor, moveZone) = byColumn(cell % MapGeometry.Columns);

            return TestCells.At(floor, cell is 0 ? 1 : moveZone);
        })];
    }

    private static int Cost(CellPath path)
    {
        var steps = path.Cells.ToArray();

        return steps.Zip(steps.Skip(1)).Sum(step => CostOf(MapGeometry.Direction(step.First, step.Second)));
    }

    private static int CostOf(int direction)
    {
        return direction switch
        {
            0 or 4 => 3,
            _ => 2,
        };
    }

    private static bool HasStairs(MapCell[] cells)
    {
        return cells.Select(cell => cell.MoveZone).Distinct().Count() > 1;
    }

    private static bool MayStep(MapCell[] cells, bool stairs, int from, int direction)
    {
        var to = MapGeometry.Neighbour(from, direction);

        if (to < 0 || !cells[to].IsWalkable)
            return false;

        // Floors count where the map has stairs, but along one, whose cells are of moveZone 1.
        if (stairs && !(cells[from].MoveZone is 1 && cells[to].MoveZone is 1) && Math.Abs(cells[from].Floor - cells[to].Floor) > 10)
            return false;

        if (!MapGeometry.IsAcross(direction))
            return true;

        var left = MapGeometry.Neighbour(from, (direction + 7) % 8);
        var right = MapGeometry.Neighbour(from, (direction + 1) % 8);

        return (left >= 0 && cells[left].IsWalkable) || (right >= 0 && cells[right].IsWalkable);
    }

    // Dijkstra over every cell, without a heuristic or a queue: slow, and plainly right.
    private static int? Exhaustive(MapCell[] cells, int from, int to)
    {
        var costs = Enumerable.Repeat(int.MaxValue, MapData.CellCount).ToArray();
        var done = new bool[MapData.CellCount];
        var stairs = HasStairs(cells);

        costs[from] = 0;

        while (true)
        {
            var cell = -1;

            for (var candidate = 0; candidate < MapData.CellCount; candidate++)
            {
                if (!done[candidate] && costs[candidate] != int.MaxValue && (cell < 0 || costs[candidate] < costs[cell]))
                    cell = candidate;
            }

            if (cell < 0)
                break;

            done[cell] = true;

            for (var direction = 0; direction < MapCell.DirectionCount; direction++)
            {
                if (!MayStep(cells, stairs, cell, direction))
                    continue;

                var next = MapGeometry.Neighbour(cell, direction);

                costs[next] = Math.Min(costs[next], costs[cell] + CostOf(direction));
            }
        }

        return from == to ? 0 : costs[to] == int.MaxValue ? null : costs[to];
    }
}
