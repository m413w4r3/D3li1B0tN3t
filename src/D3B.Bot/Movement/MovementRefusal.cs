namespace D3B.Bot.Movement;

/// <summary>Why an automated movement request cannot start from the current state.</summary>
internal enum MovementRefusal
{
    /// <summary>The position is safe for automated movement.</summary>
    None,

    /// <summary>No game connection is active.</summary>
    Disconnected,

    /// <summary>The planned action's map differs from the current map.</summary>
    MapChanged,

    /// <summary>The current map is unknown.</summary>
    MapUnknown,

    /// <summary>The current cell is unknown.</summary>
    CellUnknown,

    /// <summary>The character is walking.</summary>
    Walking,

    /// <summary>The server may still hold an unsettled walk.</summary>
    WalkHeld,

    /// <summary>The character is in a fight.</summary>
    Fighting,

    /// <summary>The character is using an element or in a dialog.</summary>
    Busy,

    /// <summary>The cell came from the client's cancellation report.</summary>
    ClientReported,

    /// <summary>The cell is an estimate from an unconfirmed walk.</summary>
    Estimated,

    /// <summary>The cell has no server-derived provenance.</summary>
    ProvenanceUnknown,
}
