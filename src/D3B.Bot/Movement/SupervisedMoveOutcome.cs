namespace D3B.Bot.Movement;

/// <summary>How a supervised one-step map move ended.</summary>
internal enum SupervisedMoveOutcome
{
    /// <summary>The character arrived on the target map and the server placed it there on a trusted cell.</summary>
    Pass,

    /// <summary>A request went, but the move did not end on the target with a trusted placement.</summary>
    Failed,

    /// <summary>No map-change request went: the session, position, transition or single-use authorization refused it.</summary>
    Blocked,

    /// <summary>The player, a fight, the game connection or the host stopped the move.</summary>
    Interrupted,
}
