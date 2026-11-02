using D3B.Data.Maps;

namespace D3B.Data.Tests.Maps;

internal static class TestMaps
{
    public static MapCell[] Cells()
    {
        var cells = new MapCell[MapData.CellCount];

        for (var i = 0; i < cells.Length; i++)
        {
            var walkable = i % 3 is not 0;

            cells[i] = new MapCell(
                walkable ? MapCellProperties.Walkable | MapCellProperties.LineOfSight : MapCellProperties.LineOfSight,
                i % 7 * 10,
                0,
                i < 14 ? 0b1110_0000 : 0,
                0,
                walkable ? 0x11 : 0,
                0);
        }

        return cells;
    }

    public static MapData Map(long id)
    {
        return new MapData(id, id + 1, id + 2, id + 3, id + 4, Cells(), new MapInteractive[] { new(470_001, 101, 650), new(470_007, 254, 3212), new(470_012, MapInteractive.NoCell, 1018) });
    }
}
