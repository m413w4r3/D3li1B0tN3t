namespace D3B.Gui.Safety;

/// <summary>Explains why a GUI action is not ready or was rejected.</summary>
public enum GuiSafetyReason
{
    /// <summary>The GUI feature is disabled by configuration.</summary>
    Disabled,

    /// <summary>The configured or observed desktop is not a usable X11 session.</summary>
    NotX11,

    /// <summary>The explicitly selected window does not exist.</summary>
    WindowNotFound,

    /// <summary>The selected window's process identity has not been verified.</summary>
    WindowIdentityUnverified,

    /// <summary>The window is not mapped, visible, focused, active, or usable for input.</summary>
    WindowNotActive,

    /// <summary>The window changed between capture observations or differs from the current window.</summary>
    WindowChanged,

    /// <summary>The requested frame capture failed.</summary>
    CaptureFailed,

    /// <summary>The capture exists but its image content cannot be used for target analysis.</summary>
    SceneUnverified,

    /// <summary>No calibrated profile is available for the action.</summary>
    NotCalibrated,

    /// <summary>The calibration is expired or belongs to another map, window, or capture geometry.</summary>
    CalibrationStale,

    /// <summary>The visual target has not passed verification or does not match the intention.</summary>
    TargetUnverified,

    /// <summary>More than one plausible visual target remains.</summary>
    AmbiguousTarget,

    /// <summary>The protocol snapshot reports a disconnected game session.</summary>
    Disconnected,

    /// <summary>The map is unknown or differs from the action intention.</summary>
    MapUnknown,

    /// <summary>The current cell is unknown or the target cell differs from the intention.</summary>
    CellUnknown,

    /// <summary>The current position lacks server placement or server confirmation provenance.</summary>
    PositionUntrusted,

    /// <summary>Movement evidence is ambiguous.</summary>
    PositionAmbiguous,

    /// <summary>The session epoch or state revision differs from the intention.</summary>
    StateChanged,

    /// <summary>The character is in a fight.</summary>
    InFight,

    /// <summary>The character is already walking, using an element, or in a dialog.</summary>
    Busy,

    /// <summary>The player has taken control from the automation.</summary>
    UserInterrupted,

    /// <summary>Protocol translation is blocked or the protocol observation is contradictory.</summary>
    ProtocolContradiction,

    /// <summary>All gate checks passed; this readiness still cannot bypass Preview or disabled input policy.</summary>
    Ready,

    /// <summary>The intention has expired or is not yet within its validity interval.</summary>
    IntentExpired
}
