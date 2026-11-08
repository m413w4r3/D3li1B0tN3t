using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.Bot.Movement;

/// <summary>Plans a same-map walk to the quickest reachable terrain cell in a set of candidates.</summary>
/// <remarks>
/// <para>
/// The planner accepts one map's cell span and has no map transition or projection input. Equal-cost candidates follow
/// <see cref="CellPathfinder.TryFindNearest"/>: its A* search stops at the first goal dequeued and explores neighbours
/// in direction order 0 through 7. Candidate enumeration order does not break ties.
/// </para>
/// </remarks>
public static class WalkToCellPlanner
{
    /// <summary>Plans the quickest path to a walkable candidate other than the current or excluded cells.</summary>
    /// <param name="cells">The cells of one map, by logical cell identifier.</param>
    /// <param name="fromCell">The current cell.</param>
    /// <param name="candidateCells">The possible terrain cells to walk to.</param>
    /// <param name="excludedCells">Cell identifiers occupied by known interactive elements.</param>
    /// <returns>The reachable candidate and its path, or the reason no plan could be produced.</returns>
    public static WalkToCellPlan Plan(
        ReadOnlySpan<MapCell> cells,
        int fromCell,
        IReadOnlyCollection<int> candidateCells,
        IReadOnlySet<int> excludedCells)
    {
        if (cells.Length != MapData.CellCount)
            return Result(WalkToCellPlanStatus.InvalidInput, $"A map has {MapData.CellCount} cells, not {cells.Length}.");

        if (!IsCellId(fromCell))
            return Result(WalkToCellPlanStatus.InvalidInput, $"The current cell {fromCell} is outside this map.");

        if (candidateCells is null)
            return Result(WalkToCellPlanStatus.InvalidInput, "Candidate cells are required.");

        if (excludedCells is null)
            return Result(WalkToCellPlanStatus.InvalidInput, "Excluded cells are required.");

        foreach (var excludedCell in excludedCells)
        {
            if (!IsCellId(excludedCell))
                return Result(WalkToCellPlanStatus.InvalidInput, $"Excluded cell {excludedCell} is outside this map.");
        }

        var walkableCandidates = new List<int>(candidateCells.Count);

        foreach (var candidateCell in candidateCells)
        {
            if (!IsCellId(candidateCell))
                return Result(WalkToCellPlanStatus.InvalidInput, $"Candidate cell {candidateCell} is outside this map.");

            if (candidateCell == fromCell || excludedCells.Contains(candidateCell) || !cells[candidateCell].IsWalkable)
                continue;

            walkableCandidates.Add(candidateCell);
        }

        if (walkableCandidates.Count is 0)
            return Result(WalkToCellPlanStatus.NoCandidates, "No walkable, non-current, non-excluded candidate cells remain.");

        var candidates = walkableCandidates.ToArray();

        if (!CellPathfinder.TryFindNearest(cells, fromCell, candidates, out var path) || path is null)
            return Result(WalkToCellPlanStatus.Unreachable, "No candidate cell is reachable from the current cell.");

        if (path.Steps is 0 || path.Start != fromCell || !walkableCandidates.Contains(path.End))
            return Result(WalkToCellPlanStatus.Unreachable, "The pathfinder did not return a walk to a remaining candidate.");

        return new WalkToCellPlan(WalkToCellPlanStatus.Reachable, path.End, path.Cells, null);
    }

    private static bool IsCellId(int cell)
    {
        return cell is >= 0 and < MapData.CellCount;
    }

    private static WalkToCellPlan Result(WalkToCellPlanStatus status, string detail)
    {
        return new WalkToCellPlan(status, null, ReadOnlyMemory<int>.Empty, detail);
    }
}
