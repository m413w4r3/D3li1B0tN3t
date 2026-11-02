namespace D3B.Bot.Fighting;

/// <summary>What the fighting activity does once a fight is lost (D198, D236).</summary>
public enum FightAfterLoss
{
    /// <summary>Goes on once the character regained the share of life it heals under.</summary>
    Resume,

    /// <summary>Pauses the bot, which its card resumes.</summary>
    Pause,

    /// <summary>Ends the activity.</summary>
    Stop,
}
