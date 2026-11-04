using D3B.Bot.Characters;
using D3B.Data.World;

namespace D3B.Bot.Movement;

/// <summary>The non-secret evidence of one supervised move: where it started, the single transition planned, and where it ended.</summary>
internal sealed record SupervisedMoveEvidence
{
    /// <summary>Gets the map the supervisor named as the start.</summary>
    public required long SourceMapId { get; init; }

    /// <summary>Gets the map the supervisor named as the destination.</summary>
    public required long TargetMapId { get; init; }

    /// <summary>Gets the map the character was on when the move was planned.</summary>
    public long? StartMapId { get; init; }

    /// <summary>Gets the cell the character was on when the move was planned.</summary>
    public int? StartCell { get; init; }

    /// <summary>Gets where that cell came from.</summary>
    public CellProvenance StartProvenance { get; init; }

    /// <summary>Gets the transition type planned, when one was.</summary>
    public WorldTransitionType? Transition { get; init; }

    /// <summary>Gets the planned transition's index in the pack's world graph, when one was.</summary>
    public int? TransitionIndex { get; init; }

    /// <summary>Gets a cell the character can leave the source map from through that transition, when one was found.</summary>
    public int? ExitCell { get; init; }

    /// <summary>Gets the map the character was on when the move ended.</summary>
    public long? EndMapId { get; init; }

    /// <summary>Gets the cell the character was on when the move ended.</summary>
    public int? EndCell { get; init; }

    /// <summary>Gets where that cell came from when the move ended.</summary>
    public CellProvenance EndProvenance { get; init; }

    /// <summary>Gets whether the end position was trusted when the move ended.</summary>
    public bool EndPositionTrusted { get; init; }
}
