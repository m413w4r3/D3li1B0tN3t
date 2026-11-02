namespace D3B.Bot.Harvesting;

/// <summary>What the harvesting activity does once a fight cut one of its steps (D144).</summary>
public enum HarvestAfterFight
{
    /// <summary>Goes on once the fight is over, as D130 had it.</summary>
    Resume,

    /// <summary>Pauses the bot, which its card resumes, its tour and counts kept.</summary>
    Pause,

    /// <summary>Ends the activity.</summary>
    Stop,
}
