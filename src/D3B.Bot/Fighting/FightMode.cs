namespace D3B.Bot.Fighting;

/// <summary>Which groups the fighting activity attacks (D236).</summary>
public enum FightMode
{
    /// <summary>The groups at the character's level: none of their monsters above it.</summary>
    Automatic,

    /// <summary>The groups holding one of the monsters chosen.</summary>
    Chosen,
}
