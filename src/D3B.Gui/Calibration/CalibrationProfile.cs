using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using D3B.Gui.Windowing;

namespace D3B.Gui.Calibration;

/// <summary>An immutable, window-bound affine calibration and its validation evidence.</summary>
public sealed record CalibrationProfile
{
    /// <summary>Gets the exact client build annotated by the calibration.</summary>
    public string ClientBuild { get; }

    /// <summary>Gets the calibrated map identifier.</summary>
    public long MapId { get; }

    /// <summary>Gets the game window identity at calibration time.</summary>
    public GameWindowIdentity WindowIdentity { get; }

    /// <summary>Gets the captured image width.</summary>
    public int CaptureWidth { get; }

    /// <summary>Gets the captured image height.</summary>
    public int CaptureHeight { get; }

    /// <summary>Gets the measured client area geometry.</summary>
    public ClientAreaGeometry ClientAreaGeometry { get; }

    /// <summary>Gets the measured desktop scale.</summary>
    public double DesktopScale { get; }

    /// <summary>Gets the calibration contract version.</summary>
    public int CalibrationVersion { get; }

    /// <summary>Gets the affine screen mapper.</summary>
    public AffineScreenMapper Mapper { get; }

    /// <summary>Gets the immutable fit and validation anchors.</summary>
    public ImmutableArray<CalibrationAnchor> Anchors { get; }

    /// <summary>Gets the independent validation residuals in pixels.</summary>
    public ImmutableArray<double> ValidationErrorsPixels { get; }

    /// <summary>Gets the profile creation time.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets whether the evidence is explicitly synthetic.</summary>
    public bool IsSynthetic { get; }

    /// <summary>Gets a stable SHA-256 identity for linking permissions to this exact calibration.</summary>
    public string CalibrationId { get; }

    /// <summary>Creates an immutable calibration profile.</summary>
    public CalibrationProfile(
        string clientBuild,
        long mapId,
        GameWindowIdentity windowIdentity,
        int captureWidth,
        int captureHeight,
        ClientAreaGeometry clientAreaGeometry,
        double desktopScale,
        int calibrationVersion,
        AffineScreenMapper mapper,
        ImmutableArray<CalibrationAnchor> anchors,
        ImmutableArray<double> validationErrorsPixels,
        DateTimeOffset createdAt,
        bool isSynthetic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientBuild);
        ArgumentNullException.ThrowIfNull(windowIdentity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(captureWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(captureHeight);
        ArgumentNullException.ThrowIfNull(mapper);

        ClientBuild = clientBuild;
        MapId = mapId;
        WindowIdentity = windowIdentity;
        CaptureWidth = captureWidth;
        CaptureHeight = captureHeight;
        ClientAreaGeometry = clientAreaGeometry;
        DesktopScale = desktopScale;
        CalibrationVersion = calibrationVersion;
        Mapper = mapper;
        Anchors = anchors.IsDefault ? ImmutableArray<CalibrationAnchor>.Empty : anchors;
        ValidationErrorsPixels = validationErrorsPixels.IsDefault ? ImmutableArray<double>.Empty : validationErrorsPixels;
        CreatedAt = createdAt;
        IsSynthetic = isSynthetic;
        CalibrationId = CreateCalibrationId();
    }

    private string CreateCalibrationId()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(ClientBuild);
            writer.Write(MapId);
            writer.Write(WindowIdentity.Xid);
            writer.Write(WindowIdentity.ProcessId.HasValue);
            if (WindowIdentity.ProcessId.HasValue)
                writer.Write(WindowIdentity.ProcessId.Value);
            writer.Write(WindowIdentity.ExecutablePath ?? string.Empty);
            writer.Write((int)WindowIdentity.Status);
            writer.Write(CaptureWidth);
            writer.Write(CaptureHeight);
            writer.Write(ClientAreaGeometry.OriginDesktop.X);
            writer.Write(ClientAreaGeometry.OriginDesktop.Y);
            writer.Write(ClientAreaGeometry.OriginInWindow.X);
            writer.Write(ClientAreaGeometry.OriginInWindow.Y);
            writer.Write(ClientAreaGeometry.Width);
            writer.Write(ClientAreaGeometry.Height);
            writer.Write(BitConverter.DoubleToInt64Bits(DesktopScale));
            writer.Write(CalibrationVersion);
            writer.Write(BitConverter.DoubleToInt64Bits(Mapper.A));
            writer.Write(BitConverter.DoubleToInt64Bits(Mapper.B));
            writer.Write(BitConverter.DoubleToInt64Bits(Mapper.Tx));
            writer.Write(BitConverter.DoubleToInt64Bits(Mapper.C));
            writer.Write(BitConverter.DoubleToInt64Bits(Mapper.D));
            writer.Write(BitConverter.DoubleToInt64Bits(Mapper.Ty));
            writer.Write(Anchors.Length);
            foreach (var anchor in Anchors)
            {
                writer.Write(anchor.Cell.Value);
                writer.Write(anchor.LogicalPoint.X);
                writer.Write(anchor.LogicalPoint.Y);
                writer.Write(anchor.MeasuredPixelPoint.X);
                writer.Write(anchor.MeasuredPixelPoint.Y);
                writer.Write((int)anchor.Role);
                writer.Write((int)anchor.Source);
            }
            writer.Write(ValidationErrorsPixels.Length);
            foreach (var error in ValidationErrorsPixels)
                writer.Write(BitConverter.DoubleToInt64Bits(error));
            writer.Write(CreatedAt.UtcDateTime.Ticks);
            writer.Write(IsSynthetic);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
}
