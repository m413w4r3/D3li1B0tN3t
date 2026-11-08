namespace D3B.Gui.Safety;

/// <summary>Configuration for GUI capture and preview.</summary>
public sealed record GuiOptions
{
    /// <summary>Gets whether GUI capture is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the desktop capture and input backend.</summary>
    public GuiBackend Backend { get; init; } = GuiBackend.X11;

    /// <summary>Gets the GUI operating mode.</summary>
    public GuiMode Mode { get; init; } = GuiMode.Preview;

    /// <summary>Gets whether real GUI input is allowed.</summary>
    public bool AllowRealInput { get; init; }

    /// <summary>Gets the capture timeout in milliseconds.</summary>
    public int CaptureTimeoutMs { get; init; } = 2000;

    /// <summary>Gets the maximum capture size in bytes.</summary>
    public int MaxCaptureBytes { get; init; } = 16777216;

    /// <summary>Gets the maximum capture width in pixels.</summary>
    public int MaxWidth { get; init; } = 3840;

    /// <summary>Gets the maximum capture height in pixels.</summary>
    public int MaxHeight { get; init; } = 2160;

    /// <summary>Gets the maximum age of a calibration in seconds.</summary>
    public int CalibrationMaxAgeSeconds { get; init; } = 30;
}
