using System.Collections.Immutable;
using D3B.Engine.Maps;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Tests.Vision;

/// <summary>Deterministic synthetic-only inputs for target localization tests.</summary>
internal static class SyntheticTargetLocatorFixtures
{
    public const int ImageWidth = 640;
    public const int ImageHeight = 480;
    public const long MapId = 7310042;
    public const int CellId = 322;
    public const int TemplateWidth = 24;
    public const int TemplateHeight = 20;

    public static GameWindowIdentity WindowIdentity { get; } = new(
        451,
        9001,
        "/synthetic/dofus",
        "synthetic target fixture",
        GameWindowIdentityStatus.Verified);

    public static AffineScreenMapper CreateMapper(int translateX = 260, int translateY = 200)
    {
        if (!AffineScreenMapper.TryCreate([4, 0, translateX, 0, 4, translateY], out var mapper) || mapper is null)
        {
            throw new InvalidOperationException("The synthetic target fixture requires a non-singular affine mapper.");
        }

        return mapper;
    }

    public static CalibrationProfile CreateCalibration(
        AffineScreenMapper? mapper = null,
        long mapId = MapId,
        GameWindowIdentity? windowIdentity = null,
        int captureWidth = ImageWidth,
        int captureHeight = ImageHeight)
    {
        var identity = windowIdentity ?? WindowIdentity;
        var actualMapper = mapper ?? CreateMapper();

        return new CalibrationProfile(
            "synthetic-build",
            mapId,
            identity,
            captureWidth,
            captureHeight,
            new ClientAreaGeometry(new DesktopPixels(0, 0), new ClientAreaPixels(0, 0), captureWidth, captureHeight),
            1.0,
            1,
            actualMapper,
            ImmutableArray<CalibrationAnchor>.Empty,
            ImmutableArray<double>.Empty,
            DateTimeOffset.UnixEpoch,
            true);
    }

    public static Mat CreateTemplate(bool similarVariant = false)
    {
        return CreateTemplate(TemplateWidth, TemplateHeight, similarVariant);
    }

    public static Mat CreateTemplate(int width, int height, bool similarVariant = false)
    {
        var template = new Mat(height, width, MatType.CV_8UC3);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var blue = (x * 17 + y * 31 + x * y * 3) % 256;
                var green = (x * 43 + y * 11 + x * y * 5 + 61) % 256;
                var red = (x * 7 + y * 53 + x * y * 9 + 127) % 256;

                if (similarVariant && ((x + y) % 3 == 0))
                {
                    blue = 255 - blue;
                    green = 255 - green;
                    red = 255 - red;
                }

                template.Set(y, x, new Vec3b((byte)blue, (byte)green, (byte)red));
            }
        }

        return template;
    }

    public static Mat CreateImage(Mat? target = null, params WindowLocalRect[] targetBounds)
    {
        return CreateImage(ImageWidth, ImageHeight, target, targetBounds);
    }

    public static Mat CreateImage(int width, int height, Mat? target = null, params WindowLocalRect[] targetBounds)
    {
        var image = new Mat(height, width, MatType.CV_8UC3, Scalar.All(19));

        if (target is null)
        {
            return image;
        }

        foreach (var bounds in targetBounds)
        {
            using var destination = new Mat(image, new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
            target.CopyTo(destination);
        }

        return image;
    }

    public static WindowLocalPixels ProjectedCellCenter(CalibrationProfile? calibration = null)
    {
        var profile = calibration ?? CreateCalibration();
        return profile.Mapper.Map(MapGeometry.PointOf(CellId));
    }

    public static CalibrationProfile CreateCalibrationAt(
        WindowLocalPixels center,
        int width = 1920,
        int height = 1175)
    {
        var logicalPoint = MapGeometry.PointOf(CellId);
        if (!AffineScreenMapper.TryCreate(
                [1, 0, center.X - logicalPoint.X, 0, 1, center.Y - logicalPoint.Y],
                out var mapper)
            || mapper is null)
        {
            throw new InvalidOperationException("The synthetic target fixture requires a non-singular affine mapper.");
        }

        return CreateCalibration(mapper, captureWidth: width, captureHeight: height);
    }

    public static WindowLocalRect CenteredTargetBounds(CalibrationProfile? calibration = null)
    {
        var center = ProjectedCellCenter(calibration);
        return new WindowLocalRect(
            center.X - (TemplateWidth / 2),
            center.Y - (TemplateHeight / 2),
            TemplateWidth,
            TemplateHeight);
    }

    public static TargetExpectation CreateExpectation(
        Mat template,
        ImmutableArray<WindowLocalRect> exclusions = default,
        ImmutableArray<LogicalCell> otherElements = default,
        int searchRadiusPixels = 96,
        int minMarginPixels = 8,
        int maxCenterDistancePixels = 64,
        int cellId = CellId,
        long mapId = MapId,
        WindowLocalOffset anchorOffset = default,
        WindowLocalOffset clickOffset = default)
    {
        return new TargetExpectation(
            mapId,
            48,
            new LogicalCell(cellId),
            9123,
            6,
            template,
            searchRadiusPixels,
            minMarginPixels,
            maxCenterDistancePixels,
            exclusions,
            otherElements,
            anchorOffset,
            clickOffset);
    }

    public static CapturedFrame CreateFrame(
        Mat image,
        CalibrationProfile? calibration = null,
        CaptureStatus status = CaptureStatus.Captured,
        bool contentUsable = true,
        GameWindowIdentity? identity = null)
    {
        var actualCalibration = calibration ?? CreateCalibration();
        var actualIdentity = identity ?? actualCalibration.WindowIdentity;
        var timestamp = DateTimeOffset.UnixEpoch;
        var snapshot = new GameWindowSnapshot(
            actualIdentity,
            true,
            true,
            true,
            false,
            false,
            false,
            true,
            true,
            true,
            true,
            new DesktopPixels(0, 0),
            image.Cols,
            image.Rows,
            0,
            image.Cols,
            image.Rows,
            null,
            timestamp);

        return new CapturedFrame(
            new Guid("3e6e44f7-a93a-48d0-8cf0-9b7af8b4df10"),
            actualIdentity,
            timestamp,
            timestamp,
            snapshot,
            snapshot,
            image.Cols,
            image.Rows,
            image.Clone(),
            status,
            status == CaptureStatus.Captured ? null : "Synthetic fixture status.",
            contentUsable);
    }
}
