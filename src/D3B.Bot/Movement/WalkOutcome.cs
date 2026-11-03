namespace D3B.Bot.Movement;

/// <summary>How a walk on the character's map ended.</summary>
public enum WalkOutcome
{
    /// <summary>The character stands on the cell: the server answered the client's confirmation, or it stood there already.</summary>
    Arrived,

    /// <summary>The character is walking or held, fighting, busy, disconnected, or has an unknown or untrusted position: this walk sent no request.</summary>
    Busy,

    /// <summary>The pack's cells hold no path to the cell, or the pack lacks the map: nothing was sent.</summary>
    NoPath,

    /// <summary>The server refused the path: it did not start where the character stands, which the state now holds.</summary>
    Refused,

    /// <summary>The server neither showed the walk nor refused it in time.</summary>
    NoAnswer,

    /// <summary>The server showed the walk, but did not answer the client's confirmation in time.</summary>
    NotConfirmed,
}
