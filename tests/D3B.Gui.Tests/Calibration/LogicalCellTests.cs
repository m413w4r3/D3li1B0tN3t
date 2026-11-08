using D3B.Data.Maps;
using D3B.Gui.Calibration;

namespace D3B.Gui.Tests.Calibration;

public sealed class LogicalCellTests
{
    [Fact]
    public void ALogicalCellRejectsIdentifiersOutsideTheMap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogicalCell(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogicalCell(MapData.CellCount));
    }

    [Fact]
    public void ALogicalCellAcceptsTheFirstAndLastMapCells()
    {
        Assert.Equal(0, new LogicalCell(0).Value);
        Assert.Equal(MapData.CellCount - 1, new LogicalCell(MapData.CellCount - 1).Value);
    }
}
