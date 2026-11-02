namespace D3B.Data.Spells;

/// <summary>An effect's flags, the client's 8-bit <c>m_flags</c> of an effect instance, with the bits its code gives them (client.md).</summary>
[Flags]
public enum SpellEffectProperties : byte
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary>The client shows the effect in the spell's tooltip.</summary>
    VisibleInTooltip = 1,

    /// <summary>The client shows the effect among a fighter's buffs.</summary>
    VisibleInBuffUi = 2,

    /// <summary>The client writes the effect in the fight's log.</summary>
    VisibleInFightLog = 4,

    /// <summary>The client shows the effect on the ground.</summary>
    VisibleOnTerrain = 8,

    /// <summary>The effect is the client's alone: the server does not apply it.</summary>
    ForClientOnly = 16,

    /// <summary>The client's <c>trigger</c>.</summary>
    Trigger = 32,

    /// <summary>The mark the effect leaves on the ground is seen.</summary>
    GroundMarkVisible = 64,

    /// <summary>The mark the effect leaves on the ground is seen by the caster's allies only.</summary>
    GroundMarkVisibleOnlyAlly = 128,
}
