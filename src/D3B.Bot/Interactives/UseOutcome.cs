namespace D3B.Bot.Interactives;

/// <summary>How the use of an element ended.</summary>
public enum UseOutcome
{
    /// <summary>The use is over: the server ended it after its duration, or started one with none, such as a door's.</summary>
    Used,

    /// <summary>The character walks or uses an element already, is in a dialog or an exchange, or where it stands is not known yet: nothing was sent.</summary>
    Busy,

    /// <summary>The server lists no such element on the map, or the element does not offer the skill now, as a resource being harvested or harvested: nothing was sent.</summary>
    Unavailable,

    /// <summary>The pack lacks the map or places the element on no cell, or no cell it is used from can be reached: nothing was sent.</summary>
    NoPath,

    /// <summary>The walk to the cell the element is used from did not arrive.</summary>
    NotWalked,

    /// <summary>The server refused the use.</summary>
    Refused,

    /// <summary>The server neither started the use nor refused it in time.</summary>
    NoAnswer,

    /// <summary>The server started the use, but did not end it in time.</summary>
    NotEnded,
}
