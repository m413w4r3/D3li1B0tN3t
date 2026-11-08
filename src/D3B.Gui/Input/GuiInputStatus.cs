namespace D3B.Gui.Input;

/// <summary>Describes the outcome of one GUI input request.</summary>
public enum GuiInputStatus
{
    /// <summary>The global pointer move and left click were emitted.</summary>
    Sent,

    /// <summary>Real input is disabled by this backend.</summary>
    Disabled,

    /// <summary>The expected window is not currently focused and active.</summary>
    WindowNotActive,

    /// <summary>The expected window identity, state, or geometry changed.</summary>
    WindowChanged,

    /// <summary>The requested desktop point is outside the safe client area.</summary>
    OutsideWindow,

    /// <summary>The request was canceled before input was emitted.</summary>
    Canceled,

    /// <summary>An external input command exceeded its time limit.</summary>
    TimedOut,

    /// <summary>The input request failed for another reason.</summary>
    Failed
}
