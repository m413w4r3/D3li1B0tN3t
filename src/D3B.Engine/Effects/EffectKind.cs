namespace D3B.Engine.Effects;

/// <summary>What an effect does in a fight, in D3B's words: the same for every spell naming it.</summary>
/// <remarks>
/// Each effect id is given one from the pack's effects (D182) and the client's names of its actions (D183); the bench
/// measures those the 49 fights captured cast (D195). An id with none is <see cref="Other"/>: the effects' engine leaves it
/// out, and says so.
/// </remarks>
public enum EffectKind
{
    /// <summary>An effect the engine does not foresee yet.</summary>
    Other,

    /// <summary>Damage of an element: 96 to 100, the best element's 2822.</summary>
    Damage,

    /// <summary>Damage of an element whose half heals the caster: 91 to 95, the best element's 2828, the worst's 2890.</summary>
    Steal,

    /// <summary>Damage no characteristic of the caster's raises: 144, and 82, a steal.</summary>
    FixedDamage,

    /// <summary>A heal of an element: 108, 2998 to 3001, the best element's 81 and 3002.</summary>
    Heal,

    /// <summary>A heal no characteristic raises: 143 and 407.</summary>
    FixedHeal,

    /// <summary>A heal of a share of the most life: 1109.</summary>
    PercentHeal,

    /// <summary>A shield of a fixed value: 1040.</summary>
    Shield,

    /// <summary>A shield of a share of the caster's most life: 1039.</summary>
    LifeShield,

    /// <summary>A shield of a share of the caster's level: 1020.</summary>
    LevelShield,

    /// <summary>The target pushed away from the cell cast on, by cells: 5, and 1021 and 1103 with no damage.</summary>
    Push,

    /// <summary>The target pulled towards the cell cast on: 6.</summary>
    Pull,

    /// <summary>The caster teleported onto the cell cast on: 4.</summary>
    Teleport,

    /// <summary>The caster and the target swap their cells: 8 and 1023.</summary>
    Swap,

    /// <summary>A monster summoned, its id and grade in the dice: 181, 1008 and 1011.</summary>
    Summon,

    /// <summary>A state set, its id the effect's value: 950.</summary>
    StateSet,

    /// <summary>A state removed: 951.</summary>
    StateRemoved,

    /// <summary>A state disabled: 952.</summary>
    StateDisabled,

    /// <summary>A characteristic raised, as the pack's effect names it with <c>+</c>.</summary>
    Boost,

    /// <summary>A characteristic lowered, as the pack's effect names it with <c>-</c>: AP and MP lost among them.</summary>
    Deboost,

    /// <summary>A change to one of the target's spells: the pack's effects of category 3.</summary>
    SpellModifier,

    /// <summary>A spell cast by the effect, its id and grade in the dice, by the caster or the fighter reached (<see cref="SpellCaster"/>): 792, 793, 1017 to 1019, 1160, 2017, 2160, 2792 to 2795 and 2960.</summary>
    CastSpell,

    /// <summary>The buffs a spell gave removed: 406.</summary>
    DispelSpell,

    /// <summary>Every buff removed: 132.</summary>
    Dispel,

    /// <summary>The target killed: 141.</summary>
    Kill,

    /// <summary>A bomb set off, the bombs its chain reaction reaches after it: 1009 (<see cref="Fights.Bombs"/>).</summary>
    Detonate,

    /// <summary>The first fighter in line from the caster pushed to the cell cast on: 783, a Roublabot's.</summary>
    PushToCell,
}
