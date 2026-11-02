using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.Engine.Tests.Maps;

public sealed class ElementCellsTests
{
    [Fact]
    public void AnElementIsUsedFromItsWalkableNeighboursNotFromItsOwnCell()
    {
        // The client's harvest of an element on a walkable cell, 176: it stood on 190, a neighbour (client.md).
        var cells = TestCells.OpenBut(162, 204);

        Assert.Equal([177, 190, 189, 175, 161, 148], ElementCells.UsedFrom(cells, 176, 1));
    }

    [Fact]
    public void WithoutAWalkableNeighbourAnElementIsUsedFromTheNearestWalkableCells()
    {
        // Water all around cell 408, as for the client's fishing, which stood on one of the nearest cells (client.md).
        var cells = TestCells.Where(cell => MapGeometry.Distance(cell, 408) >= 3);
        var used = ElementCells.UsedFrom(cells, 408, 1);

        Assert.NotEmpty(used);
        Assert.All(used, cell => Assert.Equal(3, MapGeometry.Distance(cell, 408)));
        Assert.Equal(Enumerable.Range(0, MapData.CellCount).Count(cell => MapGeometry.Distance(cell, 408) == 3), used.Count);
    }

    [Fact]
    public void OnlyCellsOfTheCharactersZoneCount()
    {
        var cells = TestCells.OpenBut();

        cells[177] = new MapCell(MapCellProperties.Walkable, 0, 0, 0, 0, 2 << 4, 0);

        Assert.DoesNotContain(177, ElementCells.UsedFrom(cells, 176, 1));
        Assert.Equal([177], ElementCells.UsedFrom(cells, 176, 2));
        Assert.Empty(ElementCells.UsedFrom(cells, 176, 3));
    }

    [Fact]
    public void AMapOfAnotherSizeOrACellOffItIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ElementCells.UsedFrom(new MapCell[10], 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ElementCells.UsedFrom(TestCells.OpenBut(), MapData.CellCount, 1));
    }
}
