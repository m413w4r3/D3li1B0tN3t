using D3B.Data.Maps;

namespace D3B.Engine.Tests.Maps;

/// <summary>The cells of a map a test draws: walkable or not, and their floor and moveZone where the test sets them.</summary>
internal static class TestCells
{
    private static readonly MapCell _walkable = At(0, 0);
    private static readonly MapCell _blocked = new(MapCellProperties.None, 0, 0, 0, 0, 0, 0);

    /// <summary>A walkable cell on a floor, of a moveZone: 1 on a stair.</summary>
    public static MapCell At(int floor, int moveZone)
    {
        return new MapCell(MapCellProperties.Walkable | MapCellProperties.LineOfSight, floor, 0, 0, moveZone, 1 << 4, 0);
    }

    /// <summary>A map whose every cell may be walked on but the ones listed.</summary>
    public static MapCell[] OpenBut(params int[] blocked)
    {
        return Where(cell => !blocked.Contains(cell));
    }

    /// <summary>A map whose cells may be walked on where a predicate says so.</summary>
    public static MapCell[] Where(Func<int, bool> walkable)
    {
        return [.. Enumerable.Range(0, MapData.CellCount).Select(cell => walkable(cell) ? _walkable : _blocked)];
    }

    /// <summary>A map whose cells may be walked on as a list says, by identifier.</summary>
    public static MapCell[] Of(bool[] walkable)
    {
        return Where(cell => walkable[cell]);
    }

    /// <summary>A map whose cells may be walked on as a list says, each on its floor and of its moveZone.</summary>
    public static MapCell[] Of(bool[] walkable, int[] floors, int[] moveZones)
    {
        return [.. Enumerable.Range(0, MapData.CellCount).Select(cell => walkable[cell] ? At(floors[cell], moveZones[cell]) : _blocked)];
    }
}
