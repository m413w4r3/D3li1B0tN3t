using D3B.Data.Maps;

namespace D3B.Data.Tests.Maps;

public sealed class MapCellTests
{
    [Theory]
    [InlineData(40_000, 0, 0, 0)]
    [InlineData(0, 200, 0, 0)]
    [InlineData(0, 0, 256, 0)]
    [InlineData(0, 0, 0, -1)]
    public void AValueThatDoesNotFitItsFieldIsRejected(int floor, int speed, int linkedZone, int mapChangeData)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapCell(MapCellProperties.Walkable, floor, speed, mapChangeData, 0, linkedZone, 0));
    }

    [Fact]
    public void UnknownPropertiesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapCell((MapCellProperties)(1 << 12), 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void TheZoneIsTheHighBitsOfTheLinkedZone()
    {
        var cell = new MapCell(MapCellProperties.Walkable, 0, 0, 0, 0, 0x21, 0);

        Assert.Equal(2, cell.Zone);
        Assert.Equal(0x21, cell.LinkedZone);
    }

    [Fact]
    public void ACellLeavesTheMapOnlyTowardsItsDirections()
    {
        var cell = new MapCell(MapCellProperties.Walkable, 0, 0, 0b0100_0001, 0, 0x11, 0);

        Assert.True(cell.LeavesMapTowards(0));
        Assert.True(cell.LeavesMapTowards(6));
        Assert.False(cell.LeavesMapTowards(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => cell.LeavesMapTowards(MapCell.DirectionCount));
    }

    [Fact]
    public void ACellMarkedNonWalkableOutsideFightsIsNotWalkable()
    {
        Assert.True(new MapCell(MapCellProperties.Walkable, 0, 0, 0, 0, 0x11, 0).IsWalkable);
        Assert.False(new MapCell(MapCellProperties.Walkable | MapCellProperties.NonWalkableDuringRoleplay, 0, 0, 0, 0, 0x11, 0).IsWalkable);
        Assert.False(new MapCell(MapCellProperties.LineOfSight, 0, 0, 0, 0, 0, 0).IsWalkable);
    }
}
