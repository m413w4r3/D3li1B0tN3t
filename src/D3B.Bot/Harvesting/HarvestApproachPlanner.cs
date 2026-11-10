using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.Bot.Harvesting;

/// <summary>Passively plans a path to a cell from which a known map element may be used.</summary>
/// <remarks>
/// The nearest eligible destination and any equal-cost choice are exactly those returned by
/// <see cref="CellPathfinder.TryFindNearest"/>. Destinations retain <see cref="ElementCells.UsedFrom"/> order, and the
/// planner does not re-rank the pathfinder's result. The planner only reads state and map data; it sends no requests and
/// starts no movement.
/// </remarks>
public static class HarvestApproachPlanner
{
    /// <summary>Plans a pack-valid path from a trusted position to an element's use cell.</summary>
    /// <param name="state">The character's server state.</param>
    /// <param name="map">The map data for the character's map.</param>
    /// <param name="elementId">The element's map identifier.</param>
    /// <param name="statedElementCell">An optional server-stated cell for the element.</param>
    /// <returns>A confirmed path or a blocked result with its reason.</returns>
    public static HarvestApproach Plan(CharacterState state, MapData map, int elementId, int? statedElementCell = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(map);

        if (state.MapId != map.Id)
            return Blocked(elementId, -1, "MAP_CHANGED");

        if (state.Cell is not { } start || start is < 0 or >= MapData.CellCount || !state.IsPositionTrusted)
            return Blocked(elementId, -1, "POSITION_UNTRUSTED");

        var serverCell = statedElementCell;

        if (state.Elements.TryGetValue(elementId, out var element) && element.Cell is { } elementCell)
        {
            if (serverCell is { } statedCell && statedCell != elementCell)
                return Blocked(elementId, elementCell, "ELEMENT_CELL_CONFLICT");

            serverCell = elementCell;
        }

        int? packCell = null;

        if (map.TryGetInteractive(elementId, out var interactive) && interactive.CellId != MapInteractive.NoCell)
            packCell = interactive.CellId;

        if (serverCell is { } knownServerCell && packCell is { } knownPackCell && knownServerCell != knownPackCell)
            return Blocked(elementId, knownServerCell, "ELEMENT_CELL_CONFLICT");

        var resolvedCell = serverCell ?? packCell;

        if (resolvedCell is null)
            return Blocked(elementId, -1, "ELEMENT_CELL_UNKNOWN");

        if (resolvedCell is < 0 or >= MapData.CellCount)
            return Blocked(elementId, resolvedCell.Value, "ELEMENT_CELL_INVALID");

        var cells = map.Cells.Span;
        var zone = cells[start].Zone;
        var useCells = ElementCells.UsedFrom(cells, resolvedCell.Value, zone);

        if (useCells.Count is 0)
            return Blocked(elementId, resolvedCell.Value, "NO_USE_CELL");

        if (!CellPathfinder.TryFindNearest(cells, start, useCells.ToArray(), out var path))
            return Blocked(elementId, resolvedCell.Value, "NO_PATH");

        if (!IsValidPath(cells, path, start))
            return Blocked(elementId, resolvedCell.Value, "INVALID_PATH");

        var pathCells = ImmutableArray.CreateRange(path.Cells.ToArray());

        return new HarvestApproach(
            elementId,
            resolvedCell.Value,
            path.End,
            pathCells,
            path.Steps,
            "CONFIRMED",
            "CONFIRMED");
    }

    private static bool IsValidPath(ReadOnlySpan<MapCell> cells, CellPath path, int start)
    {
        var pathCells = path.Cells.Span;

        if (pathCells.Length is 0 || path.Start != start || path.End is < 0 or >= MapData.CellCount)
            return false;

        for (var index = 0; index < pathCells.Length; index++)
        {
            var cell = pathCells[index];

            if (cell is < 0 or >= MapData.CellCount || !cells[cell].IsWalkable)
                return false;

            if (index > 0 && MapGeometry.Direction(pathCells[index - 1], cell) < 0)
                return false;
        }

        return true;
    }

    private static HarvestApproach Blocked(int elementId, int elementCell, string reason)
    {
        return new HarvestApproach(elementId, elementCell, -1, ImmutableArray<int>.Empty, 0, "BLOCKED", reason);
    }
}
