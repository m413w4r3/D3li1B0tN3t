namespace D3B.Bot.Movement;

/// <summary>How a journey through the world ended.</summary>
public enum TravelOutcome
{
    /// <summary>The character is on one of the maps sought, placed.</summary>
    Arrived,

    /// <summary>The first journey state is walking or held, fighting, busy, disconnected, or has an unknown or untrusted position: no request was sent.</summary>
    Busy,

    /// <summary>No path the character may follow leads to the maps sought from where it stands, the transitions that failed left out.</summary>
    NoPath,

    /// <summary>Map changes failed too often, the journey went on too long, or a later state became unknown or untrusted; no further request is sent after that refusal.</summary>
    Failed,
}
