using System.Collections.Immutable;
using D3B.Engine.Maps;
using D3B.Gui.Calibration;
using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Calibration;

internal static class SyntheticCalibrationFixtures
{
    public const int ImageWidth = 800;
    public const int ImageHeight = 600;
    public const long MapId = 191105026;
    public const string ClientBuild = "3.7.13.17-synthetic";

    private static readonly int[] _defaultFitCells = [0, 13, 153, 280, 293, 420, 433, 547, 558, 559, 546, 140];
    private static readonly int[] _defaultValidationCells = [167, 286, 532];

    public static AffineScreenMapper CreateMapper()
    {
        if (!AffineScreenMapper.TryCreate([15, 8, 70, 8, -3, 80], out var mapper) || mapper is null)
            throw new InvalidOperationException("The synthetic affine fixture must be non-singular.");

        return mapper;
    }

    public static CalibrationAnchorDocument CreateDocument(
        bool noisy = false,
        bool outliers = false,
        bool synthetic = true,
        IEnumerable<int>? fitCells = null,
        IEnumerable<int>? validationCells = null,
        AffineScreenMapper? mapper = null)
    {
        var actualMapper = mapper ?? CreateMapper();
        var actualFitCells = fitCells?.ToArray() ?? _defaultFitCells;
        var actualValidationCells = validationCells?.ToArray() ?? _defaultValidationCells;
        var anchors = ImmutableArray.CreateBuilder<CalibrationAnchor>();

        for (var index = 0; index < actualFitCells.Length; index++)
        {
            var anchor = CreateAnchor(actualFitCells[index], CalibrationAnchorRole.Fit, actualMapper, noisy, index);
            if (outliers && (index == 2 || index == 10))
            {
                anchor = new CalibrationAnchor(
                    anchor.Cell,
                    anchor.LogicalPoint,
                    new WindowLocalPixels(anchor.MeasuredPixelPoint.X + 100, anchor.MeasuredPixelPoint.Y + 90),
                    anchor.Role,
                    anchor.Source);
            }

            anchors.Add(anchor);
        }

        for (var index = 0; index < actualValidationCells.Length; index++)
        {
            anchors.Add(CreateAnchor(actualValidationCells[index], CalibrationAnchorRole.Validation, actualMapper, noisy, index + 3));
        }

        return new CalibrationAnchorDocument(ClientBuild, MapId, ImageWidth, ImageHeight, anchors.ToImmutable(), synthetic);
    }

    public static CalibrationRequest CreateRequest(
        TimeProvider? timeProvider = null,
        bool isSynthetic = true,
        double desktopScale = 1.0,
        GameWindowIdentity? windowIdentity = null,
        int captureWidth = ImageWidth,
        int captureHeight = ImageHeight,
        ClientAreaGeometry? clientAreaGeometry = null,
        int calibrationVersion = 1,
        TimeSpan? maxAge = null,
        long mapId = MapId,
        string? clientBuild = null)
    {
        return new CalibrationRequest(
            mapId,
            clientBuild ?? ClientBuild,
            captureWidth,
            captureHeight,
            desktopScale,
            clientAreaGeometry ?? CreateClientAreaGeometry(),
            windowIdentity ?? CreateWindowIdentity(),
            calibrationVersion,
            timeProvider,
            maxAge,
            isSynthetic);
    }

    public static GameWindowIdentity CreateWindowIdentity(long xid = 451, int processId = 9001)
    {
        return new GameWindowIdentity(xid, processId, "/synthetic/dofus", "synthetic title", GameWindowIdentityStatus.Verified);
    }

    public static ClientAreaGeometry CreateClientAreaGeometry(int originX = 10, int originY = 20, int width = 780, int height = 560)
    {
        return new ClientAreaGeometry(new DesktopPixels(-1910, 40), new ClientAreaPixels(originX, originY), width, height);
    }

    public static CalibrationResult Calibrate(CalibrationAnchorDocument? document = null, CalibrationRequest? request = null)
    {
        return CalibrationValidator.Validate(document ?? CreateDocument(), request ?? CreateRequest());
    }

    public static CalibrationEnvironment CreateEnvironment(CalibrationProfile profile)
    {
        return new CalibrationEnvironment(
            profile.WindowIdentity,
            profile.CaptureWidth,
            profile.CaptureHeight,
            profile.ClientAreaGeometry,
            profile.DesktopScale,
            profile.MapId,
            profile.CalibrationVersion);
    }

    public static TimeProvider CreateTimeProvider(DateTimeOffset utcNow)
    {
        return new FixedTimeProvider(utcNow);
    }

    public static CalibrationAnchor CreateAnchor(
        int cellId,
        CalibrationAnchorRole role,
        AffineScreenMapper mapper,
        bool noisy = false,
        int noiseIndex = 0,
        CalibrationAnchorSource source = CalibrationAnchorSource.SyntheticFixture)
    {
        var cell = new LogicalCell(cellId);
        var point = MapGeometry.PointOf(cell.Value);
        var projected = mapper.Project(point);
        var pixelX = (int)Math.Round(projected.X, MidpointRounding.AwayFromZero);
        var pixelY = (int)Math.Round(projected.Y, MidpointRounding.AwayFromZero);

        if (noisy)
        {
            pixelX += (noiseIndex % 3) - 1;
            pixelY += ((noiseIndex * 2) % 3) - 1;
        }

        return new CalibrationAnchor(cell, point, new WindowLocalPixels(pixelX, pixelY), role, source);
    }
}
