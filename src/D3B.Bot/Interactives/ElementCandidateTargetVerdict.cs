namespace D3B.Bot.Interactives;

/// <summary>Whether the current candidates identify one exact harvest target.</summary>
internal enum ElementCandidateTargetVerdict
{
    /// <summary>No candidate was found.</summary>
    Unknown,

    /// <summary>Exactly one candidate has verified protocol identity.</summary>
    Identified,

    /// <summary>More than one candidate or an uncertain candidate remains.</summary>
    Ambiguous,
}
