using System.Collections.Immutable;
using D3B.Engine.Maps;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Tests.Reporting;

internal static class SyntheticPreviewFixtures
{
    public const int ImageWidth = 640;
    public const int ImageHeight = 480;
    public const long MapId = 7310042;
    public const int ElementId = 48;
    public const int CellId = 322;
    public const int GfxId = 9123;
    public const int TemplateWidth = 24;
    public const int TemplateHeight = 20;

    private static readonly int[] _fitCells = [0, 13, 153, 280, 293, 420, 433, 547, 558, 559, 546, 140];
    private static readonly int[] _validationCells = [167, 286, 532];

    public static AffineScreenMapper CreateMapper()
    {
        if (!AffineScreenMapper.TryCreate([7, 3, 120, 4, 7, 180], out var mapper) || mapper is null)
        {
            throw new InvalidOperationException("The synthetic preview fixture requires a non-singular affine mapper.");
        }

        return mapper;
    }

    public static CalibrationAnchorDocument CreateDocument(bool noisy = false, int outlierCount = 0, int fitAnchorCount = 12)
    {
        var mapper = CreateMapper();
        var anchors = ImmutableArray.CreateBuilder<CalibrationAnchor>();
        for (var index = 0; index < fitAnchorCount; index++)
        {
            anchors.Add(CreateAnchor(_fitCells[index], CalibrationAnchorRole.Fit, mapper, index, noisy, index < outlierCount));
        }

        for (var index = 0; index < _validationCells.Length; index++)
        {
            anchors.Add(CreateAnchor(_validationCells[index], CalibrationAnchorRole.Validation, mapper, index + 4, noisy, false));
        }

        return new CalibrationAnchorDocument(
            "synthetic-build",
            MapId,
            ImageWidth,
            ImageHeight,
            anchors.ToImmutable(),
            synthetic: true);
    }

    public static CalibrationRequest CreateRequest(CalibrationAnchorDocument? document = null)
    {
        var anchors = document ?? CreateDocument();
        var identity = CreateWindowIdentity();
        return new CalibrationRequest(
            MapId,
            anchors.ClientBuild,
            ImageWidth,
            ImageHeight,
            1.0,
            new ClientAreaGeometry(new DesktopPixels(0, 0), new ClientAreaPixels(0, 0), ImageWidth, ImageHeight),
            identity,
            isSynthetic: true);
    }

    public static CalibrationResult Calibrate(CalibrationAnchorDocument? document = null)
    {
        var anchors = document ?? CreateDocument();
        return CalibrationValidator.Validate(anchors, CreateRequest(anchors));
    }

    public static GameWindowIdentity CreateWindowIdentity()
    {
        return new GameWindowIdentity(451, 9001, "/synthetic/dofus", "synthetic preview", GameWindowIdentityStatus.Verified);
    }

    public static Mat CreateTemplate()
    {
        var template = new Mat(TemplateHeight, TemplateWidth, MatType.CV_8UC3);
        for (var y = 0; y < TemplateHeight; y++)
        {
            for (var x = 0; x < TemplateWidth; x++)
            {
                var blue = (x * 17 + y * 31 + x * y * 3) % 256;
                var green = (x * 43 + y * 11 + x * y * 5 + 61) % 256;
                var red = (x * 7 + y * 53 + x * y * 9 + 127) % 256;
                template.Set(y, x, new Vec3b((byte)blue, (byte)green, (byte)red));
            }
        }

        return template;
    }

    public static WindowLocalRect TargetBounds(CalibrationProfile calibration)
    {
        var center = calibration.Mapper.Map(MapGeometry.PointOf(CellId));
        return new WindowLocalRect(
            center.X - (TemplateWidth / 2),
            center.Y - (TemplateHeight / 2),
            TemplateWidth,
            TemplateHeight);
    }

    public static Mat CreateImage(Mat? template = null, WindowLocalRect? bounds = null)
    {
        var image = new Mat(ImageHeight, ImageWidth, MatType.CV_8UC3, Scalar.All(19));
        if (template is not null && bounds is WindowLocalRect targetBounds)
        {
            using var destination = new Mat(image, new Rect(targetBounds.X, targetBounds.Y, targetBounds.Width, targetBounds.Height));
            template.CopyTo(destination);
        }

        return image;
    }

    public static CapturedFrame CreateFrame(Mat image, CalibrationProfile calibration)
    {
        var timestamp = DateTimeOffset.UnixEpoch;
        var snapshot = new GameWindowSnapshot(
            calibration.WindowIdentity,
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
            ImageWidth,
            ImageHeight,
            0,
            ImageWidth,
            ImageHeight,
            null,
            timestamp);

        return new CapturedFrame(
            Guid.NewGuid(),
            calibration.WindowIdentity,
            timestamp,
            timestamp,
            snapshot,
            snapshot,
            ImageWidth,
            ImageHeight,
            image.Clone(),
            CaptureStatus.Captured,
            null,
            true);
    }

    public static TargetExpectation CreateExpectation(Mat template)
    {
        return new TargetExpectation(MapId, ElementId, new LogicalCell(CellId), GfxId, null, template);
    }

    private static CalibrationAnchor CreateAnchor(
        int cellId,
        CalibrationAnchorRole role,
        AffineScreenMapper mapper,
        int noiseIndex,
        bool noisy,
        bool outlier)
    {
        var cell = new LogicalCell(cellId);
        var logicalPoint = MapGeometry.PointOf(cellId);
        var projected = mapper.Project(logicalPoint);
        var pixelX = (int)Math.Round(projected.X, MidpointRounding.AwayFromZero);
        var pixelY = (int)Math.Round(projected.Y, MidpointRounding.AwayFromZero);

        if (noisy)
        {
            pixelX += (noiseIndex % 3) - 1;
            pixelY += ((noiseIndex * 2) % 3) - 1;
        }

        if (outlier)
        {
            pixelX += 100;
            pixelY += 90;
        }

        return new CalibrationAnchor(
            cell,
            logicalPoint,
            new WindowLocalPixels(pixelX, pixelY),
            role,
            CalibrationAnchorSource.SyntheticFixture);
    }
}
