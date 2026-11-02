namespace D3B.Engine.Maps;

/// <summary>A path on a map: every cell from where the walk starts to where it ends, each the neighbour of the one before.</summary>
public sealed class CellPath
{
    private readonly int[] _cells;

    /// <summary>Gets every cell of the path, the first where the walk starts.</summary>
    public ReadOnlyMemory<int> Cells => _cells;

    /// <summary>Gets the cell the walk starts on.</summary>
    public int Start => _cells[0];

    /// <summary>Gets the cell the walk ends on.</summary>
    public int End => _cells[^1];

    /// <summary>Gets the number of steps, from one cell to the next: 0 when the walk starts where it ends.</summary>
    public int Steps => _cells.Length - 1;

    /// <summary>Initializes a new instance of the <see cref="CellPath"/> class.</summary>
    /// <param name="cells">Every cell of the path, the first where it starts, each the neighbour of the one before.</param>
    /// <exception cref="ArgumentException">The path has no cell, or a cell is not the neighbour of the one before it.</exception>
    public CellPath(IReadOnlyList<int> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        if (cells.Count is 0)
            throw new ArgumentException("A path has at least the cell it starts on.", nameof(cells));

        for (var i = 1; i < cells.Count; i++)
        {
            if (MapGeometry.Direction(cells[i - 1], cells[i]) < 0)
                throw new ArgumentException($"Cell {cells[i]} is not a neighbour of cell {cells[i - 1]}, which comes before it.", nameof(cells));
        }

        _cells = [.. cells];
    }

    /// <summary>Gets the path's turning points, as a request to walk it names them.</summary>
    /// <returns>
    /// The first cell and every cell where the direction changes, each with the direction the walk leaves it in, then the
    /// last cell with the direction the walk arrives on it in; none when the path has no step.
    /// </returns>
    public IReadOnlyList<PathTurn> TurningPoints()
    {
        if (Steps is 0)
            return [];

        var turns = new List<PathTurn>();
        var previous = -1;

        for (var i = 0; i < Steps; i++)
        {
            var direction = MapGeometry.Direction(_cells[i], _cells[i + 1]);

            if (direction != previous)
                turns.Add(new PathTurn(_cells[i], direction));

            previous = direction;
        }

        turns.Add(new PathTurn(End, previous));

        return turns;
    }
}
