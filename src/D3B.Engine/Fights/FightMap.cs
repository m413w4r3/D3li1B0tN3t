using D3B.Data.Maps;

namespace D3B.Engine.Fights;

/// <summary>A map as a fight sees it: the cells a fighter may stand on, and those a line of sight passes over.</summary>
/// <remarks>
/// <para>
/// A cell holds a fighter when the client's data marks it walkable and not <c>nonWalkableDuringFight</c>, and lets a line
/// of sight through when it marks it <c>los</c>. Measured on the 49 fights captured (client.md): every cell of the 298 walks,
/// and every cell the 396 casts the fighters chose were cast on, is walkable this way.
/// </para>
/// <para>Built once per map and shared: nothing in it changes.</para>
/// </remarks>
public sealed class FightMap
{
    private readonly CellSet _walkable;
    private readonly CellSet _sight;

    /// <summary>Gets the map's identifier.</summary>
    public long Id { get; }

    /// <summary>Gets the cells a fighter may stand on.</summary>
    public CellSet WalkableCells => _walkable;

    /// <summary>Gets the cells a line of sight passes over.</summary>
    public CellSet SightCells => _sight;

    /// <summary>Initializes a new instance of the <see cref="FightMap"/> class.</summary>
    /// <param name="id">The map's identifier.</param>
    /// <param name="cells">Its cells, as many as a map has.</param>
    /// <exception cref="ArgumentException">There are not as many cells as a map has.</exception>
    public FightMap(long id, ReadOnlySpan<MapCell> cells)
    {
        if (cells.Length != MapData.CellCount)
            throw new ArgumentException($"A map has {MapData.CellCount} cells, not {cells.Length}.", nameof(cells));

        for (var cell = 0; cell < cells.Length; cell++)
        {
            var properties = cells[cell].Properties;

            if ((properties & (MapCellProperties.Walkable | MapCellProperties.NonWalkableDuringFight)) is MapCellProperties.Walkable)
                _walkable.Add(cell);

            if ((properties & MapCellProperties.LineOfSight) is not MapCellProperties.None)
                _sight.Add(cell);
        }

        Id = id;
    }

    /// <summary>Makes the fight's view of a map of the pack.</summary>
    /// <param name="map">The map.</param>
    /// <returns>Its fight's view.</returns>
    public static FightMap Of(MapData map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return new FightMap(map.Id, map.Cells.Span);
    }

    /// <summary>Tells whether a fighter may stand on a cell.</summary>
    /// <param name="cell">A cell, which may lie off the map.</param>
    /// <returns>Whether the cell is the map's and a fighter may stand on it.</returns>
    public bool IsWalkable(int cell)
    {
        return _walkable.Contains(cell);
    }

    /// <summary>Tells whether a line of sight passes over a cell.</summary>
    /// <param name="cell">A cell, which may lie off the map.</param>
    /// <returns>Whether the cell is the map's and lets a line of sight through.</returns>
    public bool LetsSightThrough(int cell)
    {
        return _sight.Contains(cell);
    }
}
