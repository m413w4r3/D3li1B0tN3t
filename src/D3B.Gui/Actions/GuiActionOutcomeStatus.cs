namespace D3B.Gui.Actions;

/// <summary>Describes the terminal result of one submitted action intention.</summary>
public enum GuiActionOutcomeStatus
{
    /// <summary>A click and the matching client/server contract were confirmed.</summary>
    Pass,

    /// <summary>A safety guard or disabled input policy rejected the action before a click.</summary>
    Blocked,

    /// <summary>Input failed before a click was reported as emitted.</summary>
    Failed,

    /// <summary>The action was canceled or interrupted after submission.</summary>
    Interrupted,

    /// <summary>A click occurred but the matching confirmation did not arrive.</summary>
    Unconfirmed
}
