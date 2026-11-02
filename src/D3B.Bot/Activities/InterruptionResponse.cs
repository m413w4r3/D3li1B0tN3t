namespace D3B.Bot.Activities;

/// <summary>What an activity does once an interruption cancelled its step (D87).</summary>
public enum InterruptionResponse
{
    /// <summary>Waits until the bot can act again, recovers the character, and goes on.</summary>
    Resume,

    /// <summary>Ends the activity.</summary>
    Stop,

    /// <summary>Pauses the bot as the player's hand does (D66), then, once it is resumed from its card and can act, recovers the character and goes on (D144).</summary>
    Pause,
}
