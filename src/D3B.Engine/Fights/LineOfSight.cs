using D3B.Engine.Maps;

namespace D3B.Engine.Fights;

/// <summary>Whether a fighter sees a cell: the cells a straight line between two cells passes over, none of them blocking.</summary>
/// <remarks>
/// <para>
/// The line joins the centres of the two cells on the map's coordinates (<see cref="MapPoint"/>), on which each cell is a
/// square. It passes over every cell whose square it enters, in order; where it passes exactly through a corner, it
/// enters the cell across that corner and only touches the two beside it, which do not block. So a line along a diagonal
/// of the screen passes over no cell but those of the diagonal, and a line along a column of the screen between two
/// cells.
/// </para>
/// <para>
/// A cell between blocks when the map's data does not let a line of sight through it, or when a fighter stands on it;
/// the two ends never do. A point of the line that is no cell of the map, beyond its edges, blocks nothing. Counted in
/// integers, so that a line and its reverse pass over the same cells.
/// </para>
/// <para>
/// Measured on the 49 fights captured (client.md): of the 396 casts the fighters chose, 286 test the line of sight, 199 of
/// them with cells between, and none has a cell between that blocks; 44 pass a corner beside a cell that blocks, which the
/// server let through. The idea of the traversal is the references' (D181); that the server refuses what blocks needs casts
/// it refused, which no capture holds.
/// </para>
/// </remarks>
public static class LineOfSight
{
    /// <summary>The most cells a line between two cells of a map passes over, its ends left out.</summary>
    public const int MaxCellsBetween = 66;

    /// <summary>Lists the cells of the map a line passes over between two cells, in order from the first.</summary>
    /// <param name="from">The cell the line starts on.</param>
    /// <param name="to">The cell it ends on.</param>
    /// <param name="cells">Where to write them: <see cref="MaxCellsBetween"/> always suffice.</param>
    /// <returns>How many it wrote: none between a cell and itself or a neighbour.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A cell does not exist.</exception>
    /// <exception cref="ArgumentException">The span is too short.</exception>
    public static int CellsBetween(int from, int to, Span<int> cells)
    {
        var count = 0;
        var walker = new Walker(from, to);

        while (walker.TryStep(out var point))
        {
            if (!MapGeometry.TryGetCell(point, out var cell))
                continue;

            if (count == cells.Length)
                throw new ArgumentException($"{cells.Length} cells are too few for the line from {from} to {to}.", nameof(cells));

            cells[count++] = cell;
        }

        return count;
    }

    /// <summary>Tells whether a line of sight passes from a cell to another.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">The cells fighters stand on.</param>
    /// <param name="from">The cell seen from, the caster's.</param>
    /// <param name="to">The cell seen.</param>
    /// <returns>Whether no cell between blocks.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A cell does not exist.</exception>
    public static bool IsClear(FightMap map, in CellSet fighters, int from, int to)
    {
        ArgumentNullException.ThrowIfNull(map);

        var walker = new Walker(from, to);

        while (walker.TryStep(out var point))
        {
            if (MapGeometry.TryGetCell(point, out var cell) && (!map.LetsSightThrough(cell) || fighters.Contains(cell)))
                return false;
        }

        return true;
    }

    // Steps along the line from square to square, choosing the axis whose next edge the line meets first: the edge
    // between x and x + 1 lies at (2i + 1) / (2|dx|) of the way, compared without division.
    private ref struct Walker
    {
        private readonly int _sx;
        private readonly int _sy;
        private readonly int _ax;
        private readonly int _ay;
        private int _x;
        private int _y;
        private int _ix;
        private int _iy;

        public Walker(int from, int to)
        {
            var start = MapGeometry.PointOf(from);
            var end = MapGeometry.PointOf(to);

            _sx = Math.Sign(end.X - start.X);
            _sy = Math.Sign(end.Y - start.Y);
            _ax = Math.Abs(end.X - start.X);
            _ay = Math.Abs(end.Y - start.Y);
            _x = start.X;
            _y = start.Y;
        }

        public bool TryStep(out MapPoint point)
        {
            long order = _ix == _ax ? 1 : _iy == _ay ? -1 : ((2L * _ix) + 1) * _ay - ((2L * _iy) + 1) * _ax;

            if (order <= 0)
            {
                _ix++;
                _x += _sx;
            }

            if (order >= 0)
            {
                _iy++;
                _y += _sy;
            }

            point = new MapPoint(_x, _y);

            return _ix < _ax || _iy < _ay;
        }
    }
}
