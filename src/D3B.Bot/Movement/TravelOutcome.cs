namespace D3B.Bot.Movement;

/// <summary>How a journey through the world ended.</summary>
public enum TravelOutcome
{
    /// <summary>The character is on one of the maps sought, placed.</summary>
    Arrived,

    /// <summary>The character walks already, uses an element, is in a dialog or an exchange, or where it stands is not known yet: nothing was sent.</summary>
    Busy,

    /// <summary>No path the character may follow leads to the maps sought from where it stands, the transitions that failed left out.</summary>
    NoPath,

    /// <summary>Map changes failed too often, the journey went on too long, or where the character stands is no longer known.</summary>
    Failed,
}
