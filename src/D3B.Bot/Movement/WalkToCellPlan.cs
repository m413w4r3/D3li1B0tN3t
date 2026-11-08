namespace D3B.Bot.Movement;

/// <summary>A path from the current cell to a reachable candidate on the same map.</summary>
public sealed class WalkToCellPlan
{
    private readonly int[] _pathCells;

    /// <summary>Gets the result of planning the walk.</summary>
    public WalkToCellPlanStatus Status { get; }

    /// <summary>Gets the chosen target cell, or <see langword="null"/> when no path was found.</summary>
    public int? TargetCell { get; }

    /// <summary>Gets each cell of the path, including its start and target; empty when no path was found.</summary>
    public ReadOnlyMemory<int> PathCells => _pathCells;

    /// <summary>Gets the number of steps in the path, or 0 when no path was found.</summary>
    public int Steps { get; }

    /// <summary>Gets why planning could not produce a path, when a detail is available.</summary>
    public string? Detail { get; }

    internal WalkToCellPlan(WalkToCellPlanStatus status, int? targetCell, ReadOnlyMemory<int> pathCells, string? detail)
    {
        Status = status;
        TargetCell = targetCell;
        _pathCells = pathCells.ToArray();
        Steps = _pathCells.Length is 0 ? 0 : _pathCells.Length - 1;
        Detail = detail;
    }
}
