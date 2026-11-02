namespace D3B.Engine.Fights;

/// <summary>Why a fighter may not cast a spell level on a cell, in the order the rules check it, or <see cref="None"/>.</summary>
public enum CastRefusal
{
    /// <summary>The cast is allowed.</summary>
    None,

    /// <summary>The caster is dead, or on no cell.</summary>
    CasterAbsent,

    /// <summary>The caster is in a state that prevents casting.</summary>
    CasterCannotCast,

    /// <summary>The caster has fewer AP than the level costs.</summary>
    NotEnoughActionPoints,

    /// <summary>The level's criterion on the caster's states fails, or cannot be read.</summary>
    StatesForbid,

    /// <summary>The level's first cast waits for a later round.</summary>
    InitialCooldown,

    /// <summary>The level was cast too few rounds ago.</summary>
    Interval,

    /// <summary>The level was cast as many times as it may be this turn.</summary>
    TooManyCasts,

    /// <summary>The caster summons as many fighters as it may already.</summary>
    TooManySummons,

    /// <summary>The cell is not one a fighter may stand on.</summary>
    CellNotWalkable,

    /// <summary>The level needs a cell no fighter stands on.</summary>
    CellNotFree,

    /// <summary>The level needs a cell a fighter stands on.</summary>
    CellNotTaken,

    /// <summary>The level needs a fighter seen on the cell.</summary>
    TargetNotVisible,

    /// <summary>The level was cast as many times as it may be this turn on the fighter on the cell.</summary>
    TooManyCastsOnTarget,

    /// <summary>The cell lies beyond the level's range, or out of its line or diagonal.</summary>
    OutOfRange,

    /// <summary>A cell between the caster and the cell blocks the line of sight.</summary>
    NoLineOfSight,
}
