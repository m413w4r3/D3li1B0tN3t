namespace D3B.Engine.Fights;

/// <summary>What a buff changes in a spell: each value the id of the effect that gives it, as the pack's effects describe it.</summary>
public enum SpellModifierKind
{
    /// <summary>More least range: effect 280.</summary>
    MinRange = 280,

    /// <summary>More most range: effect 281.</summary>
    Range = 281,

    /// <summary>The range may be boosted: effect 282.</summary>
    RangeBoostable = 282,

    /// <summary>More damage: effect 283, 9d's.</summary>
    Damage = 283,

    /// <summary>More heals: effect 284, 9d's.</summary>
    Heals = 284,

    /// <summary>Fewer AP to cast: effect 285.</summary>
    ApCost = 285,

    /// <summary>Fewer rounds between two casts: effect 286.</summary>
    Interval = 286,

    /// <summary>More critical chance: effect 287, 9d's.</summary>
    Critical = 287,

    /// <summary>No longer cast in line only: effect 288.</summary>
    LineDisabled = 288,

    /// <summary>No line of sight tested: effect 289.</summary>
    LineOfSightDisabled = 289,

    /// <summary>More casts a turn: effect 290.</summary>
    CastsPerTurn = 290,

    /// <summary>More casts a target: effect 291.</summary>
    CastsPerTarget = 291,

    /// <summary>The rounds between two casts set: effect 292.</summary>
    IntervalSet = 292,

    /// <summary>More base damage: effect 293, 9d's.</summary>
    BaseDamage = 293,

    /// <summary>Less most range: effect 294.</summary>
    RangeLess = 294,

    /// <summary>Less least range: effect 295.</summary>
    MinRangeLess = 295,

    /// <summary>More AP to cast: effect 296.</summary>
    ApCostMore = 296,

    /// <summary>No taken cell needed: effect 297.</summary>
    TakenCellDisabled = 297,

    /// <summary>No free cell needed: effect 298.</summary>
    FreeCellDisabled = 298,

    /// <summary>A free cell needed: effect 299.</summary>
    FreeCellEnabled = 299,

    /// <summary>The most range set: effect 2905, measured twice (D193).</summary>
    RangeSet = 2905,

    /// <summary>The least range set: effect 2906, measured twice (D193).</summary>
    MinRangeSet = 2906,
}
