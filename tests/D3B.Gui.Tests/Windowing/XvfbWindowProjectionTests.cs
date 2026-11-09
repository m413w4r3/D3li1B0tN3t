using System.Collections.Immutable;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Tests.Calibration;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Windowing;

public sealed class XvfbWindowProjectionTests
{
    private static readonly int[] _fitCells = [0, 13, 153, 280, 293, 420, 433, 547, 558, 559, 546, 140];
    private static readonly int[] _validationCells = [167, 286, 532];

    [Fact]
    public async Task LocatesFakeXvfbWindowCapturesSnapshotAndProjectsCalibratedCell()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        GameWindowExpectation expectation = new("xterm");
        GameWindowSnapshot window = await locator.LocateAsync(
            display.WindowId,
            expectation,
            TestContext.Current.CancellationToken);
        XvfbSnapshotCapture capture = new(locator);

        using var frame = await capture.CaptureAsync(
            display.WindowId,
            expectation,
            TestContext.Current.CancellationToken);

        Assert.Equal(GameWindowIdentityStatus.Verified, window.Identity.Status);
        Assert.Equal(CaptureStatus.Captured, frame.Status);
        Assert.True(frame.ContentUsable);
        Assert.Equal(1, capture.CaptureCount);
        Assert.Equal(display.WindowId, frame.WindowIdentity.Xid);
        Assert.Equal(window.WindowWidth, frame.PixelWidth);
        Assert.Equal(window.WindowHeight, frame.PixelHeight);

        ClientAreaGeometry geometry = new(
            new DesktopPixels(window.DesktopOrigin.X + window.BorderWidth, window.DesktopOrigin.Y + window.BorderWidth),
            new ClientAreaPixels(window.BorderWidth, window.BorderWidth),
            window.ClientWidth,
            window.ClientHeight);
        ImmutableArray<CalibrationAnchor> anchors = CreateAnchors(frame.PixelWidth, frame.PixelHeight);
        CalibrationAnchorDocument document = new(
            SyntheticCalibrationFixtures.ClientBuild,
            SyntheticCalibrationFixtures.MapId,
            frame.PixelWidth,
            frame.PixelHeight,
            anchors,
            synthetic: true);
        DateTimeOffset now = frame.CaptureCompletedAt;
        CalibrationRequest calibrationRequest = SyntheticCalibrationFixtures.CreateRequest(
            new FixedTimeProvider(now),
            true,
            1,
            frame.WindowIdentity,
            frame.PixelWidth,
            frame.PixelHeight,
            geometry,
            CalibrationAnchorDocument.CurrentFormatVersion,
            TimeSpan.FromSeconds(CalibrationValidator.CalibrationMaxAgeSeconds),
            SyntheticCalibrationFixtures.MapId);
        CalibrationResult calibration = CalibrationValidator.Validate(document, calibrationRequest);
        Assert.True(
            calibration.IsCalibrated,
            string.Join(" | ", calibration.Problems.Select(problem => $"{problem.Code}: {problem.Detail}")));
        Assert.NotNull(calibration.Profile);

        TargetRegion target = TerrainCellTargetLocator.Locate(
            frame,
            calibration.Profile,
            SyntheticCalibrationFixtures.MapId,
            new LogicalCell(301),
            geometry,
            4,
            ImmutableArray<WindowLocalRect>.Empty,
            new HashSet<int>());

        Assert.Equal(TargetStatus.Verified, target.Status);
        Assert.NotNull(target.ProposedClickPoint);
        Assert.Equal(301, target.Cell.Value);
    }

    private static ImmutableArray<CalibrationAnchor> CreateAnchors(int width, int height)
    {
        if (!AffineScreenMapper.TryCreate(
                [7, 3, (width - 213) / 2.0, -1, 4, (height - 135) / 2.0 + 96],
                out AffineScreenMapper? mapper)
            || mapper is null)
        {
            throw new InvalidOperationException("The Xvfb test calibration mapper must be non-singular.");
        }

        ImmutableArray<CalibrationAnchor>.Builder anchors = ImmutableArray.CreateBuilder<CalibrationAnchor>();
        foreach (int cell in _fitCells)
        {
            anchors.Add(SyntheticCalibrationFixtures.CreateAnchor(cell, CalibrationAnchorRole.Fit, mapper));
        }

        foreach (int cell in _validationCells)
        {
            anchors.Add(SyntheticCalibrationFixtures.CreateAnchor(cell, CalibrationAnchorRole.Validation, mapper));
        }

        return anchors.ToImmutable();
    }
}
