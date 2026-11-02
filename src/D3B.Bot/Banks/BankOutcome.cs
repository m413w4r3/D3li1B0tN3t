namespace D3B.Bot.Banks;

/// <summary>How a step of banking, or banking whole, ended.</summary>
public enum BankOutcome
{
    /// <summary>The step is over: the character stands where a banker is, the bank is open, the stacks and kamas moved, or the bank is closed.</summary>
    Done,

    /// <summary>The character walks, uses an element, or is in a dialog or an exchange already, or where it stands is not known yet; or, for a deposit, the bank is not open: nothing was sent.</summary>
    Busy,

    /// <summary>No bank's inside where a banker may stand can be reached, or no banker stands on the map: nothing was sent.</summary>
    NoBank,

    /// <summary>The journey to a bank failed on the way.</summary>
    NotTravelled,

    /// <summary>The server refused: the NPC's action, the reply, or every stack and kama the bot moved, which it answers with nothing.</summary>
    Refused,

    /// <summary>The server did not answer in time.</summary>
    NoAnswer,
}
