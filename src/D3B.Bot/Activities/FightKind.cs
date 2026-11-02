namespace D3B.Bot.Activities;

/// <summary>What a fight that cut an activity's step was, told by when it began (D144).</summary>
/// <remarks>
/// No message the readable protocol reads gives a fight's kind. Measured on client 3.7.12.12, a resource's protector attacks
/// 2 ms after the end of the character's own harvest, with no request from the client; the player's own fight comes after
/// the player's walk, which paused the bot first (D66), so it never cuts a step.
/// </remarks>
public enum FightKind
{
    /// <summary>Begun as a harvest of the character's ended: a resource's protector.</summary>
    Protector,

    /// <summary>Begun while the bot acted otherwise, nobody having attacked: an aggression.</summary>
    Aggression,
}
