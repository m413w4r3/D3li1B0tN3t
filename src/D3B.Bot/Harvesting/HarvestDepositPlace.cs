namespace D3B.Bot.Harvesting;

/// <summary>Where the harvesting activity deposits what it banks (D147).</summary>
public enum HarvestDepositPlace
{
    /// <summary>The character's own chest, at the nearest bank's banker.</summary>
    OwnChest,

    /// <summary>The guild's chest of the nearest bank holding one; what it refuses goes to the character's own chest.</summary>
    GuildChest,
}
