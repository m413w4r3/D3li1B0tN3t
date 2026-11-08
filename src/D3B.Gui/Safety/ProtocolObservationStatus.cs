namespace D3B.Gui.Safety;

/// <summary>Describes how complete and trustworthy the protocol observation is.</summary>
public enum ProtocolObservationStatus
{
    /// <summary>No protocol observation is available.</summary>
    Unknown,

    /// <summary>The required map and cell are known without a reported contradiction.</summary>
    Complete,

    /// <summary>Some required map or cell information is missing.</summary>
    Partial,

    /// <summary>Movement evidence conflicts.</summary>
    Contradictory,

    /// <summary>Protocol translation failed for the current session.</summary>
    TranslationBlocked,
}
