using System.Collections.Immutable;
using D3B.Engine.Maps;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Tests.Vision;

internal sealed class SyntheticTerrainCellTargetFixtures : IDisposable
{
    public const int Width = 800;
    public const int Height = 600;
    public const long MapId = 7310042;
    public const int CellId = 322;

    public static GameWindowIdentity Identity { get; } = new(
        451,
        9001,
        "/synthetic/dofus",
        "synthetic terrain fixture",
        GameWindowIdentityStatus.Verified);

    public CapturedFrame Frame { get; private set; }

    public CalibrationProfile Calibration { get; private set; }

    public ClientAreaGeometry ClientAreaGeometry { get; private set; }

    public WindowLocalPixels Center { get; private set; }

    public SyntheticTerrainCellTargetFixtures()
    {
        Center = new WindowLocalPixels(260, 200);
        ClientAreaGeometry = new ClientAreaGeometry(new DesktopPixels(0, 0), new ClientAreaPixels(0, 0), Width, Height);
        Calibration = CreateCalibration(Center, ClientAreaGeometry);
        Frame = CreateFrame(Calibration, ClientAreaGeometry);
    }

    public void SetCenter(WindowLocalPixels center)
    {
        Replace(center: center);
    }

    public void SetClientArea(ClientAreaGeometry clientAreaGeometry)
    {
        Replace(clientAreaGeometry: clientAreaGeometry);
    }

    public void SetCalibration(CalibrationProfile calibration)
    {
        Calibration = calibration;
    }

    public void SetFrame(CapturedFrame frame)
    {
        Frame.Dispose();
        Frame = frame;
    }

    public TargetRegion Locate(
        int safetyMargin = 0,
        ImmutableArray<WindowLocalRect> exclusions = default,
        IReadOnlySet<int>? interactiveElementCells = null,
        long? mapId = null)
    {
        return TerrainCellTargetLocator.Locate(
            Frame,
            Calibration,
            mapId ?? MapId,
            new LogicalCell(CellId),
            ClientAreaGeometry,
            safetyMargin,
            exclusions,
            interactiveElementCells ?? new HashSet<int>());
    }

    public static CalibrationProfile CreateCalibration(
        WindowLocalPixels center,
        ClientAreaGeometry geometry,
        long mapId = MapId,
        GameWindowIdentity? identity = null,
        int captureWidth = Width,
        int captureHeight = Height)
    {
        MapPoint logicalPoint = MapGeometry.PointOf(CellId);

        if (!AffineScreenMapper.TryCreate(
                [1, 0, center.X - logicalPoint.X, 0, 1, center.Y - logicalPoint.Y],
                out AffineScreenMapper? mapper)
            || mapper is null)
        {
            throw new InvalidOperationException("The synthetic terrain fixture requires a non-singular affine mapper.");
        }

        return new CalibrationProfile(
            "synthetic-build",
            mapId,
            identity ?? Identity,
            captureWidth,
            captureHeight,
            geometry,
            1,
            1,
            mapper,
            ImmutableArray<CalibrationAnchor>.Empty,
            ImmutableArray<double>.Empty,
            DateTimeOffset.UnixEpoch,
            true);
    }

    public static CapturedFrame CreateFrame(
        CalibrationProfile calibration,
        ClientAreaGeometry geometry,
        int width = Width,
        int height = Height,
        GameWindowIdentity? frameIdentity = null,
        GameWindowSnapshot? before = null,
        GameWindowSnapshot? after = null,
        CaptureStatus status = CaptureStatus.Captured,
        bool contentUsable = true,
        Mat? image = null)
    {
        GameWindowIdentity actualIdentity = frameIdentity ?? calibration.WindowIdentity;
        GameWindowSnapshot snapshot = CreateSnapshot(actualIdentity, geometry, width, height);
        Mat actualImage = image ?? new Mat(height, width, MatType.CV_8UC3, Scalar.All(17));
        DateTimeOffset timestamp = DateTimeOffset.UnixEpoch;

        return new CapturedFrame(
            Guid.NewGuid(),
            actualIdentity,
            timestamp,
            timestamp,
            before ?? snapshot,
            after ?? snapshot,
            width,
            height,
            actualImage,
            status,
            status == CaptureStatus.Captured ? null : "Synthetic capture failure.",
            contentUsable);
    }

    public static GameWindowSnapshot CreateSnapshot(
        GameWindowIdentity identity,
        ClientAreaGeometry geometry,
        int width = Width,
        int height = Height)
    {
        int border = geometry.OriginInWindow.X;
        int windowWidth = width;
        int windowHeight = height;
        int clientWidth = geometry.Width;
        int clientHeight = geometry.Height;
        DesktopPixels desktopOrigin = new(
            geometry.OriginDesktop.X - border,
            geometry.OriginDesktop.Y - border);

        return new GameWindowSnapshot(
            identity,
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
            desktopOrigin,
            windowWidth,
            windowHeight,
            border,
            clientWidth,
            clientHeight,
            null,
            DateTimeOffset.UnixEpoch);
    }

    public void Dispose()
    {
        Frame.Dispose();
    }

    private void Replace(
        WindowLocalPixels? center = null,
        ClientAreaGeometry? clientAreaGeometry = null)
    {
        ClientAreaGeometry = clientAreaGeometry ?? ClientAreaGeometry;
        Center = center ?? Center;
        Calibration = CreateCalibration(this.Center, ClientAreaGeometry);
        CapturedFrame oldFrame = Frame;
        Frame = CreateFrame(Calibration, ClientAreaGeometry);
        oldFrame.Dispose();
    }
}
