namespace D3B.Bot.Interactives;

/// <summary>Whether a supervised element-use target is ready, blocked, or lacks data.</summary>
internal enum ElementUsePreparationState
{
    /// <summary>The exact requested use passed every passive check.</summary>
    Ready,

    /// <summary>A current safety or position condition prevents the use.</summary>
    Blocked,

    /// <summary>The map, element, skill, or item data needed to prove the use is unavailable.</summary>
    Unavailable,
}
