using D3B.Gui.Windowing;

namespace D3B.Gui.Calibration;

/// <summary>The measured environment against which a profile is checked.</summary>
public sealed record CalibrationEnvironment
{
    /// <summary>Gets the current game-window identity.</summary>
    public GameWindowIdentity WindowIdentity { get; }

    /// <summary>Gets the current capture width.</summary>
    public int CaptureWidth { get; }

    /// <summary>Gets the current capture height.</summary>
    public int CaptureHeight { get; }

    /// <summary>Gets the current measured client-area geometry.</summary>
    public ClientAreaGeometry ClientAreaGeometry { get; }

    /// <summary>Gets the current measured desktop scale.</summary>
    public double DesktopScale { get; }

    /// <summary>Gets the active map identifier.</summary>
    public long MapId { get; }

    /// <summary>Gets the active calibration contract version.</summary>
    public int CalibrationVersion { get; }

    /// <summary>Creates an environment snapshot.</summary>
    public CalibrationEnvironment(
        GameWindowIdentity windowIdentity,
        int captureWidth,
        int captureHeight,
        ClientAreaGeometry clientAreaGeometry,
        double desktopScale,
        long mapId,
        int calibrationVersion)
    {
        ArgumentNullException.ThrowIfNull(windowIdentity);
        WindowIdentity = windowIdentity;
        CaptureWidth = captureWidth;
        CaptureHeight = captureHeight;
        ClientAreaGeometry = clientAreaGeometry;
        DesktopScale = desktopScale;
        MapId = mapId;
        CalibrationVersion = calibrationVersion;
    }
}
