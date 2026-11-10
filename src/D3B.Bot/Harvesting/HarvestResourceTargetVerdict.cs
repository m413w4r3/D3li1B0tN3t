namespace D3B.Bot.Harvesting;

/// <summary>The certainty of the exact resource target selection.</summary>
internal enum HarvestResourceTargetVerdict
{
    Identified,
    Multiple,
    Ambiguous,
    NotFound,
}
