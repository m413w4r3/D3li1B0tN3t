namespace D3B.Engine.Fights;

/// <summary>When a mark on cells casts its spell, as the effect that set it says.</summary>
public enum CellMarkKind
{
    /// <summary>A mark the engine does not foresee, such as a portal.</summary>
    Other,

    /// <summary>A glyph cast on a fighter there as its turn begins: effect 401.</summary>
    TurnStart,

    /// <summary>A glyph cast on a fighter there as its turn ends: effect 402.</summary>
    TurnEnd,

    /// <summary>A glyph cast on the fighters there as it comes, and on one that comes into it: effect 1091.</summary>
    Aura,

    /// <summary>A trap, cast as a fighter comes onto it: effect 400.</summary>
    Trap,
}
