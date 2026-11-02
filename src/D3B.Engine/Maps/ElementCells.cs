using D3B.Data.Maps;

namespace D3B.Engine.Maps;

/// <summary>The cells a character uses an interactive element of its map from, as the client chooses them.</summary>
/// <remarks>
/// Measured on the client's uses (client.md): it walks to a walkable neighbour of the element's cell, one of the eight,
/// never onto the element's cell, even when that can be walked on. When no neighbour can be walked on, as by water, it
/// walks to the walkable cell nearest the element (<see cref="MapGeometry.Distance"/>). The server accepted every one of
/// these, the client's and D3B's.
/// </remarks>
public static class ElementCells
{
    /// <summary>Finds the cells of a zone that an element is used from.</summary>
    /// <param name="cells">The map's cells, by identifier.</param>
    /// <param name="element">The element's cell.</param>
    /// <param name="zone">The zone the character walks in (<see cref="MapCell.Zone"/>): only its cells can be reached.</param>
    /// <returns>The walkable neighbours of the element's cell in the zone, or else the zone's walkable cells nearest it; none when the zone has no walkable cell.</returns>
    /// <exception cref="ArgumentException">The map does not have <see cref="MapData.CellCount"/> cells.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The element's cell does not exist.</exception>
    public static IReadOnlyList<int> UsedFrom(ReadOnlySpan<MapCell> cells, int element, int zone)
    {
        if (cells.Length != MapData.CellCount)
            throw new ArgumentException($"A map has {MapData.CellCount} cells, not {cells.Length}.", nameof(cells));

        ArgumentOutOfRangeException.ThrowIfNegative(element);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(element, MapData.CellCount);

        var used = new List<int>();

        for (var direction = 0; direction < MapCell.DirectionCount; direction++)
        {
            var neighbour = MapGeometry.Neighbour(element, direction);

            if (neighbour >= 0 && IsIn(cells[neighbour], zone))
                used.Add(neighbour);
        }

        if (used.Count > 0)
            return used;

        var nearest = int.MaxValue;

        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            if (cell == element || !IsIn(cells[cell], zone))
                continue;

            var distance = MapGeometry.Distance(cell, element);

            if (distance > nearest)
                continue;

            if (distance < nearest)
            {
                nearest = distance;
                used.Clear();
            }

            used.Add(cell);
        }

        return used;
    }

    private static bool IsIn(MapCell cell, int zone)
    {
        return cell.IsWalkable && cell.Zone == zone;
    }
}
