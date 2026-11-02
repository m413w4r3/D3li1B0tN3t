using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.Engine.Tests.Maps;

public sealed class MapGeometryTests
{
    [Fact]
    public void NeighboursAreTheOffsetsMeasuredOnTheServersPaths()
    {
        // Row 10 is even, row 11 odd: an odd row lies half a cell east of an even one.
        const int even = 10 * MapGeometry.Columns + 5;
        const int odd = 11 * MapGeometry.Columns + 5;

        Assert.Equal([even + 1, even + 14, even + 28, even + 13, even - 1, even - 15, even - 28, even - 14], Enumerable.Range(0, 8).Select(direction => MapGeometry.Neighbour(even, direction)));
        Assert.Equal([odd + 1, odd + 15, odd + 28, odd + 14, odd - 1, odd - 14, odd - 28, odd - 13], Enumerable.Range(0, 8).Select(direction => MapGeometry.Neighbour(odd, direction)));
    }

    [Fact]
    public void EveryStepLeadsBackTheOppositeWay()
    {
        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            for (var direction = 0; direction < MapCell.DirectionCount; direction++)
            {
                var next = MapGeometry.Neighbour(cell, direction);

                if (next >= 0)
                {
                    Assert.Equal(cell, MapGeometry.Neighbour(next, (direction + 4) % 8));
                    Assert.Equal(direction, MapGeometry.Direction(cell, next));
                }
            }
        }
    }

    [Fact]
    public void TheEdgesLeadOffTheMap()
    {
        Assert.Equal(-1, MapGeometry.Neighbour(0, 4));
        Assert.Equal(-1, MapGeometry.Neighbour(0, 6));
        Assert.Equal(-1, MapGeometry.Neighbour(0, 5));
        Assert.Equal(-1, MapGeometry.Neighbour(13, 0));
        Assert.Equal(-1, MapGeometry.Neighbour(MapData.CellCount - 1, 2));
        Assert.Equal(-1, MapGeometry.Neighbour(MapData.CellCount - 1, 1));
        Assert.Equal(-1, MapGeometry.Direction(0, 2));
    }

    [Fact]
    public void ACellOrADirectionThatDoesNotExistIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGeometry.Neighbour(MapData.CellCount, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGeometry.Neighbour(0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGeometry.PointOf(-1));
    }

    [Fact]
    public void CoordinatesNameEveryCellOnce()
    {
        var points = new HashSet<MapPoint>();

        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            var point = MapGeometry.PointOf(cell);

            Assert.True(points.Add(point));
            Assert.True(MapGeometry.TryGetCell(point, out var back));
            Assert.Equal(cell, back);
        }

        Assert.False(MapGeometry.TryGetCell(new MapPoint(-1, 0), out _));
        Assert.False(MapGeometry.TryGetCell(new MapPoint(0, 1), out _));
    }

    [Fact]
    public void EachDirectionMovesTheCoordinatesByOneStep()
    {
        const int cell = 20 * MapGeometry.Columns + 6;
        var point = MapGeometry.PointOf(cell);
        (int X, int Y)[] moves = [(1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1), (0, 1)];

        for (var direction = 0; direction < MapCell.DirectionCount; direction++)
        {
            var next = MapGeometry.PointOf(MapGeometry.Neighbour(cell, direction));

            Assert.Equal(moves[direction], (next.X - point.X, next.Y - point.Y));
        }

        Assert.Equal(new MapPoint(0, 0), MapGeometry.PointOf(0));
        Assert.Equal(new MapPoint(1, 0), MapGeometry.PointOf(14));
    }

    [Fact]
    public void TheDistanceCountsADiagonalAsOneStepAndTheOthersAsTwo()
    {
        const int cell = 20 * MapGeometry.Columns + 6;

        Assert.Equal([2, 1, 2, 1, 2, 1, 2, 1], Enumerable.Range(0, MapCell.DirectionCount).Select(direction => MapGeometry.Distance(cell, MapGeometry.Neighbour(cell, direction))));
        Assert.Equal(0, MapGeometry.Distance(cell, cell));

        // The client's fishing measured in session 4c: from cell 388 to the element on 231, 11; from 243, 3.
        Assert.Equal(11, MapGeometry.Distance(388, 231));
        Assert.Equal(3, MapGeometry.Distance(243, 231));
    }

    [Fact]
    public void OnlyEastSouthWestAndNorthCrossBetweenTwoCells()
    {
        Assert.Equal([0, 2, 4, 6], Enumerable.Range(0, 8).Where(MapGeometry.IsAcross));
    }
}
