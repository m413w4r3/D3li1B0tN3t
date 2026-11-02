using CsCheck;
using D3B.Data.Maps;
using D3B.Engine.Fights;
using D3B.Engine.Maps;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Fights;

public sealed class LineOfSightTests
{
    [Fact]
    public void NoCellLiesBetweenACellAndItsNeighbours()
    {
        Assert.Equal([], Between(Middle, Middle));
        Assert.Equal([], Between(Middle, Off(1, 0)));
        Assert.Equal([], Between(Middle, Off(1, 1)));
    }

    [Fact]
    public void ALineAlongAnAxisPassesOverEveryCellBetween()
    {
        Assert.Equal([Off(1, 0), Off(2, 0)], Between(Middle, Off(3, 0)));
        Assert.Equal([Off(0, -1), Off(0, -2)], Between(Middle, Off(0, -3)));
    }

    [Fact]
    public void ALineAlongADiagonalPassesOverItsCellsAlone()
    {
        Assert.Equal([Off(1, 1), Off(2, 2)], Between(Middle, Off(3, 3)));
    }

    [Fact]
    public void ALineThroughACornerOnlyTouchesTheCellsBesideIt()
    {
        var target = Off(3, 1);

        Assert.Equal([Off(1, 0), Off(2, 1)], Between(Middle, target));
        Assert.True(LineOfSight.IsClear(WithPillars(Off(2, 0), Off(1, 1)), default, Middle, target));
        Assert.False(LineOfSight.IsClear(WithPillars(Off(2, 1)), default, Middle, target));
    }

    [Fact]
    public void ACellThatBlocksSightOrAFighterBetweenBlocks()
    {
        var target = Off(3, 0);

        Assert.True(LineOfSight.IsClear(Open(), default, Middle, target));
        Assert.False(LineOfSight.IsClear(WithPillars(Off(2, 0)), default, Middle, target));
        Assert.False(LineOfSight.IsClear(Open(Off(1, 0)), default, Middle, target));
        Assert.False(LineOfSight.IsClear(Open(), CellSet.Of(Off(1, 0)), Middle, target));
    }

    [Fact]
    public void TheEndsOfALineNeverBlockIt()
    {
        var target = Off(3, 0);

        Assert.True(LineOfSight.IsClear(WithPillars(Middle, target), CellSet.Of(Middle, target), Middle, target));
    }

    [Fact]
    public void ALineAndItsReversePassOverTheSameCells()
    {
        Gen.Select(Gen.Int[0, MapData.CellCount - 1], Gen.Int[0, MapData.CellCount - 1]).Sample((from, to) =>
        {
            var forth = Between(from, to);
            var back = Between(to, from);

            back.Reverse();

            return forth.SequenceEqual(back) && forth.Distinct().Count() == forth.Count && !forth.Contains(from) && !forth.Contains(to);
        });
    }

    [Fact]
    public void EachCellALinePassesOverLiesNearerTheEndThanTheOneBefore()
    {
        Gen.Select(Gen.Int[0, MapData.CellCount - 1], Gen.Int[0, MapData.CellCount - 1]).Sample((from, to) =>
        {
            var cells = Between(from, to);
            var distances = cells.Select(cell => MapGeometry.Distance(cell, to)).Prepend(MapGeometry.Distance(from, to)).ToList();

            return distances.Zip(distances.Skip(1)).All(pair => pair.Second < pair.First);
        });
    }

    private static List<int> Between(int from, int to)
    {
        Span<int> cells = stackalloc int[LineOfSight.MaxCellsBetween];
        var count = LineOfSight.CellsBetween(from, to, cells);

        return [.. cells[..count]];
    }
}
