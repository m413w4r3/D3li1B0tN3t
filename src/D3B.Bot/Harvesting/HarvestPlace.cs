namespace D3B.Bot.Harvesting;

/// <summary>How the harvesting activity chooses where it harvests (D166).</summary>
public enum HarvestPlace
{
    /// <summary>For each character, at each tour, the sub-area richest in what it can harvest at its levels.</summary>
    Automatic,

    /// <summary>The area and sub-areas the settings name.</summary>
    Chosen,
}
