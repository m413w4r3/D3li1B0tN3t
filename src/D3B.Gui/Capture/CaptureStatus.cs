namespace D3B.Gui.Capture;

/// <summary>Describes the outcome of one window capture attempt.</summary>
public enum CaptureStatus
{
    /// <summary>An image was captured and passed the capture-content checks; scene recognition is not implied.</summary>
    Captured,

    /// <summary>The requested X11 window does not exist.</summary>
    WindowNotFound,

    /// <summary>The requested X11 window is unmapped, unviewable, or minimized.</summary>
    WindowHidden,

    /// <summary>The requested window identity, geometry, or mapped state changed during capture.</summary>
    WindowChanged,

    /// <summary>The external capture process exceeded its time limit.</summary>
    TimedOut,

    /// <summary>The PNG output exceeded the configured byte limit.</summary>
    OutputTooLarge,

    /// <summary>The configured capture executable is unavailable.</summary>
    BinaryUnavailable,

    /// <summary>The PNG output could not be decoded.</summary>
    DecodeFailed,

    /// <summary>The decoded image has empty, excessive, or unexpected dimensions.</summary>
    InvalidDimensions,

    /// <summary>The decoded image is constant or almost entirely black.</summary>
    EmptyContent,

    /// <summary>The capture failed for another reason.</summary>
    Failed
}
