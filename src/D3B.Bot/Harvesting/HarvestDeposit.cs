namespace D3B.Bot.Harvesting;

/// <summary>What the harvesting activity puts in the bank.</summary>
public enum HarvestDeposit
{
    /// <summary>What harvesting brings: every resource a profession gathers, and every item a sub-area lists as harvestable, such as the boxes found with them.</summary>
    Harvest,

    /// <summary>Everything the character carries but what it wears.</summary>
    AllButWorn,
}
