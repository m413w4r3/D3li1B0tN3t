namespace D3B.Bot.Movement;

/// <summary>The result of planning a walk to one of a map's candidate cells.</summary>
public enum WalkToCellPlanStatus
{
    /// <summary>A reachable candidate and a path were found.</summary>
    Reachable,

    /// <summary>No candidate remained after filtering invalid walk targets.</summary>
    NoCandidates,

    /// <summary>Valid candidates remained, but none could be reached.</summary>
    Unreachable,

    /// <summary>The map cells, current cell, candidate cells, or excluded cells were invalid.</summary>
    InvalidInput,
}
