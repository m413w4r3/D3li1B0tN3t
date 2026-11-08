namespace D3B.Gui.Windowing;

/// <summary>Describes whether a game window's process identity has been verified.</summary>
public enum GameWindowIdentityStatus
{
    /// <summary>The window's process identity and executable were verified.</summary>
    Verified,

    /// <summary>The window manager did not expose a process identifier.</summary>
    UnverifiedProcessIdUnavailable,

    /// <summary>The process executable could not be read.</summary>
    UnverifiedExecutableUnreadable,

    /// <summary>The process executable did not match the authorized client.</summary>
    UnverifiedExecutableMismatch,

    /// <summary>No matching game window was found.</summary>
    NotFound
}
