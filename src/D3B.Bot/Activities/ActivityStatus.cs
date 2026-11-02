namespace D3B.Bot.Activities;

/// <summary>What an activity does now (D87).</summary>
public enum ActivityStatus
{
    /// <summary>Waits until the bot can act: a game connection plays the character, placed and out of a fight, and the player has not taken it over.</summary>
    Waiting,

    /// <summary>Runs a step.</summary>
    Stepping,

    /// <summary>Unloads the character, which is full.</summary>
    Unloading,

    /// <summary>Puts the character back in order after an interruption.</summary>
    Recovering,

    /// <summary>Is over: <see cref="ActivityRun.Completion"/> says how.</summary>
    Over,
}
