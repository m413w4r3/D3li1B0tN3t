namespace D3B.Bot.Inventory;

/// <summary>How using an object of the inventory ended (D153).</summary>
public enum ObjectUseOutcome
{
    /// <summary>The server used it: the stack lessened, or went once its last object was.</summary>
    Used,

    /// <summary>The server refused, as food at full life, with a text and nothing used.</summary>
    Refused,

    /// <summary>The character carries no such stack, or wears it: nothing was sent.</summary>
    NotCarried,

    /// <summary>The character is in the middle of something: nothing was sent.</summary>
    Busy,

    /// <summary>The server did not answer in time.</summary>
    NoAnswer,
}
