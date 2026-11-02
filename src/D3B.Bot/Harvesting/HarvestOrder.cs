namespace D3B.Bot.Harvesting;

/// <summary>Which resource of a map the harvesting activity harvests first, by profession (D165).</summary>
public enum HarvestOrder
{
    /// <summary>The nearest, whatever its level.</summary>
    Nearest,

    /// <summary>The highest level first, then the others of the map, none left.</summary>
    HighestLevel,
}
