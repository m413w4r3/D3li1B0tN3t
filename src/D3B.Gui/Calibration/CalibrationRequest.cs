using D3B.Gui.Windowing;

namespace D3B.Gui.Calibration;

/// <summary>Expected map, build, capture, and freshness inputs for one calibration attempt.</summary>
public sealed record CalibrationRequest
{
    /// <summary>Gets the expected map identifier.</summary>
    public long MapId { get; }

    /// <summary>Gets the expected client build.</summary>
    public string ClientBuild { get; }

    /// <summary>Gets the expected capture width.</summary>
    public int CaptureWidth { get; }

    /// <summary>Gets the expected capture height.</summary>
    public int CaptureHeight { get; }

    /// <summary>Gets the measured desktop scale.</summary>
    public double DesktopScale { get; }

    /// <summary>Gets the measured client-area geometry.</summary>
    public ClientAreaGeometry ClientAreaGeometry { get; }

    /// <summary>Gets the target window identity.</summary>
    public GameWindowIdentity WindowIdentity { get; }

    /// <summary>Gets the current calibration contract version.</summary>
    public int CalibrationVersion { get; }

    /// <summary>Gets the clock used to timestamp the profile.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>Gets the permitted profile age.</summary>
    public TimeSpan MaxAge { get; }

    /// <summary>Gets whether this request is explicitly synthetic.</summary>
    public bool IsSynthetic { get; }

    /// <summary>Creates a calibration request.</summary>
    public CalibrationRequest(
        long mapId,
        string clientBuild,
        int captureWidth,
        int captureHeight,
        double desktopScale,
        ClientAreaGeometry clientAreaGeometry,
        GameWindowIdentity windowIdentity,
        int calibrationVersion = 1,
        TimeProvider? timeProvider = null,
        TimeSpan? maxAge = null,
        bool isSynthetic = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientBuild);
        ArgumentNullException.ThrowIfNull(windowIdentity);

        MapId = mapId;
        ClientBuild = clientBuild;
        CaptureWidth = captureWidth;
        CaptureHeight = captureHeight;
        DesktopScale = desktopScale;
        ClientAreaGeometry = clientAreaGeometry;
        WindowIdentity = windowIdentity;
        CalibrationVersion = calibrationVersion;
        TimeProvider = timeProvider ?? TimeProvider.System;
        MaxAge = maxAge ?? TimeSpan.FromSeconds(CalibrationValidator.CalibrationMaxAgeSeconds);
        IsSynthetic = isSynthetic;
    }
}
