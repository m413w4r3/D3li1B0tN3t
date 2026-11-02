namespace D3B.Engine.Tactics;

/// <summary>One thing the fighter playing does: walk to a cell, or cast a spell's level on one.</summary>
/// <param name="Kind">A walk or a cast.</param>
/// <param name="Cell">The cell walked to, or cast on.</param>
/// <param name="SpellId">The spell cast, or 0 for a walk.</param>
/// <param name="LevelId">The spell's level cast, by its id in the pack, or 0 for a walk.</param>
public readonly record struct TacticAction(TacticActionKind Kind, int Cell, int SpellId, int LevelId)
{
    /// <summary>Gets a walk to a cell.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns>The walk.</returns>
    public static TacticAction Walk(int cell)
    {
        return new TacticAction(TacticActionKind.Walk, cell, 0, 0);
    }

    /// <summary>Gets a cast of a level on a cell.</summary>
    /// <param name="spellId">The spell.</param>
    /// <param name="levelId">Its level, by id.</param>
    /// <param name="cell">The cell.</param>
    /// <returns>The cast.</returns>
    public static TacticAction Cast(int spellId, int levelId, int cell)
    {
        return new TacticAction(TacticActionKind.Cast, cell, spellId, levelId);
    }
}
