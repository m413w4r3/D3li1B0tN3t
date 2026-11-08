namespace D3B.Gui.Safety;

/// <summary>Where the known logical cell came from.</summary>
public enum GuiCellProvenance
{
    /// <summary>No usable cell evidence is available.</summary>
    Unknown = 0,

    /// <summary>The client reported the cell.</summary>
    ClientReported = 1,

    /// <summary>The cell was estimated from a walk.</summary>
    Estimated = 2,

    /// <summary>The server placed the character on the cell.</summary>
    ServerPlaced = 3,

    /// <summary>The server confirmed the character on the cell.</summary>
    ServerConfirmed = 4,

    /// <summary>The server named the cell while refusing movement.</summary>
    ServerRefused = 5,
}
