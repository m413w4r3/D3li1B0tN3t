namespace D3B.Bot.Runtime;

/// <summary>Why a bot's behavior stopped before it was done (D9).</summary>
public enum Interruption
{
    /// <summary>The player acted on the character: the bot pauses until it is resumed (D66).</summary>
    Player,

    /// <summary>The game connection closed.</summary>
    Disconnected,

    /// <summary>A fight began: the server created a fight's context (D88).</summary>
    Fight,

    /// <summary>The host is stopping.</summary>
    Stopped,
}
