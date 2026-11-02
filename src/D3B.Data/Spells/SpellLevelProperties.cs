namespace D3B.Data.Spells;

/// <summary>A spell level's flags, the client's 16-bit <c>m_flags</c>, with the bits its code gives them (client.md).</summary>
[Flags]
public enum SpellLevelProperties : ushort
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary>The spell is cast in a straight line only.</summary>
    CastInLine = 1,

    /// <summary>The spell is cast in a diagonal only, or in a line too with <see cref="CastInLine"/>.</summary>
    CastInDiagonal = 2,

    /// <summary>The target cell must be in the caster's line of sight.</summary>
    CastTestLos = 4,

    /// <summary>The target cell must hold no fighter.</summary>
    NeedFreeCell = 8,

    /// <summary>The target cell must hold a fighter.</summary>
    NeedTakenCell = 16,

    /// <summary>The target cell must hold no trap.</summary>
    NeedFreeTrapCell = 32,

    /// <summary>The caster's range bonus adds to the spell's range.</summary>
    RangeCanBeBoosted = 64,

    /// <summary>The client's <c>hideEffects</c>.</summary>
    HideEffects = 128,

    /// <summary>The client's <c>hidden</c>.</summary>
    Hidden = 256,

    /// <summary>The client's <c>playAnimation</c>.</summary>
    PlayAnimation = 512,

    /// <summary>The target cell must hold a fighter that is seen.</summary>
    NeedVisibleEntity = 1024,

    /// <summary>The target cell must hold no portal.</summary>
    NeedCellWithoutPortal = 2048,

    /// <summary>The spell is not cast through portals.</summary>
    PortalProjectionForbidden = 4096,

    /// <summary>The client's <c>proportionalDiagonal</c>, set on 6 levels of client 3.7.12.12.</summary>
    ProportionalDiagonal = 8192,
}
