namespace D3B.Bot.Journal;

/// <summary>What becomes of a bot, apart from what it does.</summary>
public enum BotEvent
{
    /// <summary>A game connection plays the character.</summary>
    Connected,

    /// <summary>The game connection that played the character closed.</summary>
    Disconnected,

    /// <summary>The player acted on the character during a behavior: the bot pauses until it is resumed (D66).</summary>
    PausedByPlayer,

    /// <summary>The bot was resumed.</summary>
    Resumed,

    /// <summary>A fight began, which interrupts the bot's behavior (D88).</summary>
    FightBegan,

    /// <summary>An activity paused the bot after a fight, as its settings say: it waits until it is resumed (D144).</summary>
    PausedAfterFight,
}
