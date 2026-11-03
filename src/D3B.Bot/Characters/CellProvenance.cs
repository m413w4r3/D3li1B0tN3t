namespace D3B.Bot.Characters;

/// <summary>Where the character's known cell came from.</summary>
public enum CellProvenance
{
    /// <summary>No usable position evidence is known.</summary>
    Unknown,

    /// <summary>The client reported this cell while cancelling its walk.</summary>
    ClientReported,

    /// <summary>The cell is the end of a walk whose confirmation timed out.</summary>
    Estimated,

    /// <summary>The server placed the character on this cell with map information.</summary>
    ServerPlaced,

    /// <summary>The server confirmed the character on this cell.</summary>
    ServerConfirmed,

    /// <summary>The server named this cell while refusing a movement.</summary>
    ServerRefused,
}
