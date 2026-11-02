using D3B.Data.Maps;

namespace D3B.Engine.Maps;

/// <summary>How the cells of a map lie: 14 columns of 40 staggered rows, and the eight directions between neighbours.</summary>
/// <remarks>
/// <para>
/// A cell's identifier is its row times 14 plus its column. Odd rows lie half a cell east of even ones, so a cell has
/// eight neighbours: east and west in its row, north and south two rows away, and the four diagonals in the rows next to
/// it. Directions run from 0, east, clockwise to 7, as in the client's data (<see cref="MapCell.LeavesMapTowards"/>).
/// </para>
/// <para>
/// Measured on every path of the character the server sent in the captures, 797 cells: each cell is the neighbour of
/// the one before it, and each turning point of the requests leaves in the direction this geometry gives (client.md).
/// </para>
/// </remarks>
public static class MapGeometry
{
    /// <summary>The number of cells in a row.</summary>
    public const int Columns = 14;

    /// <summary>The number of rows.</summary>
    public const int Rows = MapData.CellCount / Columns;

    /// <summary>Gets the neighbour of a cell in a direction.</summary>
    /// <param name="cell">A cell of the map.</param>
    /// <param name="direction">A direction, from 0 (east) clockwise to 7.</param>
    /// <returns>The neighbour, or -1 when the direction leads off the map.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cell or the direction does not exist.</exception>
    public static int Neighbour(int cell, int direction)
    {
        CheckCell(cell);
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(direction, MapCell.DirectionCount);

        var row = cell / Columns;
        var column = cell % Columns;
        var odd = row % 2;

        // Within a diagonal's row, an odd row's neighbour is one column further east than an even row's.
        var (columns, rows) = direction switch
        {
            0 => (1, 0),
            1 => (odd, 1),
            2 => (0, 2),
            3 => (odd - 1, 1),
            4 => (-1, 0),
            5 => (odd - 1, -1),
            6 => (0, -2),
            _ => (odd, -1),
        };

        column += columns;
        row += rows;

        return column is < 0 or >= Columns || row is < 0 or >= Rows ? -1 : row * Columns + column;
    }

    /// <summary>Gets the direction from a cell to one of its neighbours.</summary>
    /// <param name="from">A cell of the map.</param>
    /// <param name="to">Another cell of the map.</param>
    /// <returns>The direction, or -1 when the cells are not neighbours.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A cell does not exist.</exception>
    public static int Direction(int from, int to)
    {
        CheckCell(to);

        for (var direction = 0; direction < MapCell.DirectionCount; direction++)
        {
            if (Neighbour(from, direction) == to)
                return direction;
        }

        return -1;
    }

    /// <summary>Tells whether a direction crosses between two cells: east, south, west and north, as the screen shows them.</summary>
    /// <param name="direction">A direction, from 0 (east) clockwise to 7.</param>
    /// <returns>Whether the direction is even: its move passes between the neighbours on either side of it.</returns>
    public static bool IsAcross(int direction)
    {
        return direction % 2 is 0;
    }

    /// <summary>Tells whether a cell of a map's edge leaves the map in a straight direction.</summary>
    /// <param name="mapCell">The cell's data.</param>
    /// <param name="cell">The cell.</param>
    /// <param name="direction">A straight direction: 0 east, 2 south, 4 west or 6 north.</param>
    /// <returns>Whether the client's data marks the cell as leaving that way.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cell or the direction does not exist.</exception>
    public static bool LeavesTowards(MapCell mapCell, int cell, int direction)
    {
        if (mapCell.LeavesMapTowards(direction))
            return true;

        // A corner lies on two edges, which share its diagonal marks: only the straight one tells which edge it leaves by.
        var edges = 0;

        for (var straight = 0; straight < MapCell.DirectionCount; straight += 2)
        {
            if (Neighbour(cell, straight) < 0)
                edges++;
        }

        return edges is 1
            && (mapCell.LeavesMapTowards((direction + 1) % MapCell.DirectionCount) || mapCell.LeavesMapTowards((direction + MapCell.DirectionCount - 1) % MapCell.DirectionCount));
    }

    /// <summary>Gets a cell's coordinates.</summary>
    /// <param name="cell">A cell of the map.</param>
    /// <returns>The coordinates.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cell does not exist.</exception>
    public static MapPoint PointOf(int cell)
    {
        CheckCell(cell);

        var row = cell / Columns;
        var column = cell % Columns;

        return new MapPoint(column + (row + 1) / 2, column - row / 2);
    }

    /// <summary>Gets the distance between two cells: the sum of the differences of their coordinates.</summary>
    /// <param name="from">A cell of the map.</param>
    /// <param name="to">Another cell of the map.</param>
    /// <returns>The distance: 1 to a neighbour along a diagonal, 2 to one east, south, west or north.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A cell does not exist.</exception>
    public static int Distance(int from, int to)
    {
        var a = PointOf(from);
        var b = PointOf(to);

        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    /// <summary>Gets the cell at coordinates.</summary>
    /// <param name="point">The coordinates.</param>
    /// <param name="cell">The cell, when the map has one there.</param>
    /// <returns>Whether the coordinates name a cell of the map.</returns>
    public static bool TryGetCell(MapPoint point, out int cell)
    {
        // X - Y is the row, and the column follows from either coordinate.
        var row = point.X - point.Y;
        var column = point.Y + row / 2;

        if (row is >= 0 and < Rows && column is >= 0 and < Columns)
        {
            cell = row * Columns + column;

            return true;
        }

        cell = -1;

        return false;
    }

    private static void CheckCell(int cell)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cell);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cell, MapData.CellCount);
    }
}
