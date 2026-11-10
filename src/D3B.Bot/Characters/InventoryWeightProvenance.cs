namespace D3B.Bot.Characters;

/// <summary>Where the character's inventory weight came from.</summary>
public enum InventoryWeightProvenance
{
    /// <summary>The server has not reported inventory weight in this session.</summary>
    Unknown,

    /// <summary>The server reported inventory weight.</summary>
    ServerEvent,
}
