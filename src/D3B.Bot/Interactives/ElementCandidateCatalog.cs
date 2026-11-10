namespace D3B.Bot.Interactives;

/// <summary>How observed skill evidence matches the job catalog.</summary>
internal enum ElementCandidateCatalog
{
    /// <summary>No skill evidence was available.</summary>
    Unknown,

    /// <summary>Every evidence skill is in the job catalog.</summary>
    Matched,

    /// <summary>Some evidence skills are in the job catalog.</summary>
    Partial,

    /// <summary>No evidence skill is in the job catalog.</summary>
    Unmatched,
}
