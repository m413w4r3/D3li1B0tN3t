namespace D3B.Bot.Interactives;

/// <summary>The supervised verdict for one element-use attempt.</summary>
internal enum SupervisedElementUseOutcome
{
    /// <summary>The server started and ended the use, and the expected item count increased.</summary>
    PassHarvest,

    /// <summary>A local safety or behavior condition prevented the request.</summary>
    Blocked,

    /// <summary>The server explicitly refused the request.</summary>
    Refused,

    /// <summary>The available evidence did not prove whether the harvest completed.</summary>
    Unconfirmed,

    /// <summary>The bot, connection, or caller stopped the attempt.</summary>
    Interrupted,
}
