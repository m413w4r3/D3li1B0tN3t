namespace D3B.Data.Spells;

/// <summary>A spell's flags, the client's <c>m_flags</c>, with the bits its code gives them (client.md).</summary>
[Flags]
public enum SpellProperties
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary>The client's <c>verboseCast</c>.</summary>
    VerboseCast = 1,

    /// <summary>The spell summons beyond the most summons a fighter may have.</summary>
    BypassSummoningLimit = 2,

    /// <summary>Effects the spell triggers may trigger spells again, where a chain of triggers otherwise stops.</summary>
    CanAlwaysTriggerSpells = 4,

    /// <summary>The client's <c>hideCastConditions</c>.</summary>
    HideCastConditions = 8,
}
