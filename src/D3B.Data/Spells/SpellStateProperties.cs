namespace D3B.Data.Spells;

/// <summary>What a state does to the fighter in it, the client's booleans of <c>SpellStateData</c>, one bit each.</summary>
[Flags]
public enum SpellStateProperties : ushort
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary>The fighter casts no spell.</summary>
    PreventsSpellCast = 1,

    /// <summary>The client's <c>preventsFight</c>.</summary>
    PreventsFight = 2,

    /// <summary>The client's <c>isSilent</c>.</summary>
    IsSilent = 4,

    /// <summary>The fighter cannot be moved.</summary>
    CantBeMoved = 8,

    /// <summary>The fighter cannot be pushed.</summary>
    CantBePushed = 16,

    /// <summary>The fighter deals no damage.</summary>
    CantDealDamage = 32,

    /// <summary>The fighter takes no damage.</summary>
    Invulnerable = 64,

    /// <summary>The fighter cannot switch position with another.</summary>
    CantSwitchPosition = 128,

    /// <summary>The fighter cannot be healed.</summary>
    Incurable = 256,

    /// <summary>The fighter takes no damage in melee.</summary>
    InvulnerableMelee = 512,

    /// <summary>The fighter takes no damage at range.</summary>
    InvulnerableRange = 1024,

    /// <summary>The fighter does not tackle.</summary>
    CantTackle = 2048,

    /// <summary>The fighter is not tackled.</summary>
    CantBeTackled = 4096,

    /// <summary>The client shows the turns the state has left.</summary>
    DisplayTurnRemaining = 8192,

    /// <summary>The client's <c>isMainState</c>.</summary>
    IsMainState = 16384,
}
