namespace D3B.Bot.Interactives;

/// <summary>Whether a supervised action would be allowed by the current diagnostics.</summary>
internal enum ElementCandidateActionVerdict
{
    /// <summary>A session, target, preparation, or action setting blocks use.</summary>
    Blocked,

    /// <summary>The identified target is prepared and the action is enabled.</summary>
    Ready,
}
