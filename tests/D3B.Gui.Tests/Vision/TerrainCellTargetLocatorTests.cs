using System.Collections.Immutable;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Tests.Vision;

public sealed class TerrainCellTargetLocatorTests
{
    [Fact]
    public void ProjectsAndVerifiesTheMeasuredTerrainCellCenter()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();

        TargetRegion target = fixture.Locate();

        Assert.Equal(TargetStatus.Verified, target.Status);
        Assert.Equal(fixture.Center, target.ExpectedVisualCenter);
        Assert.Equal(fixture.Center, target.ProposedClickPoint);
        Assert.Equal(fixture.Center, target.ProjectedCellCenter);
        Assert.Equal(SyntheticTerrainCellTargetFixtures.MapId, target.MapId);
        Assert.Equal(SyntheticTerrainCellTargetFixtures.CellId, target.Cell.Value);
        Assert.Equal(0, target.ElementId);
        Assert.Equal(0, target.GfxId);
        Assert.Equal(fixture.Frame.CaptureId, target.CaptureId);
        Assert.Equal(fixture.Calibration.CalibrationId, target.CalibrationId);
        Assert.Equal(SyntheticTerrainCellTargetFixtures.Width, target.FrameWidth);
        Assert.Equal(SyntheticTerrainCellTargetFixtures.Height, target.FrameHeight);
        Assert.Equal(
            ImmutableArray.Create(
                TargetEvidence.ProjectedByCalibration,
                TargetEvidence.InsideFrame,
                TargetEvidence.ClearOfExclusions),
            target.Confidence.Evidence);
    }

    [Fact]
    public void RejectsAProjectedPointOutsideTheFrame()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();
        fixture.SetCenter(new WindowLocalPixels(-10, 200));

        AssertUnverifiedWithoutPoint(fixture.Locate(), TargetEvidence.RegionOutOfFrame);
    }

    [Fact]
    public void RejectsAPointOutsideTheMeasuredClientArea()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();
        ClientAreaGeometry clientArea = new(new DesktopPixels(250, 250), new ClientAreaPixels(250, 250), 300, 100);
        fixture.SetClientArea(clientArea);
        fixture.SetCenter(new WindowLocalPixels(200, 200));

        AssertUnverifiedWithoutPoint(fixture.Locate(), TargetEvidence.OutsideClientArea);
    }

    [Fact]
    public void RejectsAPointInsideTheClientAreaButWithinTheSafetyMargin()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();
        ClientAreaGeometry clientArea = new(new DesktopPixels(250, 250), new ClientAreaPixels(250, 250), 300, 100);
        fixture.SetClientArea(clientArea);
        fixture.SetCenter(new WindowLocalPixels(400, 300));

        AssertUnverifiedWithoutPoint(fixture.Locate(safetyMargin: 60), TargetEvidence.InsufficientClientAreaMargin);
    }

    [Theory]
    [InlineData("nameplate", 240, 180, 40, 40)]
    [InlineData("top icons", 250, 0, 20, 240)]
    [InlineData("right bar", 250, 190, 390, 20)]
    public void RejectsAPointInsideEachConfiguredExclusion(
        string exclusionClass,
        int x,
        int y,
        int width,
        int height)
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();
        ImmutableArray<WindowLocalRect> exclusions = [new WindowLocalRect(x, y, width, height)];

        TargetRegion target = fixture.Locate(exclusions: exclusions);

        AssertUnverifiedWithoutPoint(target, TargetEvidence.BlockedByExclusion);
        Assert.False(string.IsNullOrWhiteSpace(exclusionClass));
    }

    [Fact]
    public void RejectsAKnownInteractiveElementCell()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();

        TargetRegion target = fixture.Locate(interactiveElementCells: new HashSet<int> { SyntheticTerrainCellTargetFixtures.CellId });

        AssertUnverifiedWithoutPoint(target, TargetEvidence.InteractiveElementCell);
    }

    [Fact]
    public void RejectsAMapMismatch()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();

        AssertUnverifiedWithoutPoint(fixture.Locate(mapId: SyntheticTerrainCellTargetFixtures.MapId + 1), TargetEvidence.MapMismatch);
    }

    [Fact]
    public void RejectsAWindowMismatch()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();
        GameWindowIdentity otherIdentity = SyntheticTerrainCellTargetFixtures.Identity with { Xid = 452 };
        CapturedFrame mismatched = SyntheticTerrainCellTargetFixtures.CreateFrame(
            fixture.Calibration,
            fixture.ClientAreaGeometry,
            frameIdentity: otherIdentity);
        fixture.SetFrame(mismatched);

        AssertUnverifiedWithoutPoint(fixture.Locate(), TargetEvidence.WindowMismatch);
    }

    [Fact]
    public void RejectsAFrameDimensionMismatch()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();
        CapturedFrame mismatched = SyntheticTerrainCellTargetFixtures.CreateFrame(
            fixture.Calibration,
            fixture.ClientAreaGeometry,
            width: SyntheticTerrainCellTargetFixtures.Width - 1);
        fixture.SetFrame(mismatched);

        AssertUnverifiedWithoutPoint(fixture.Locate(), TargetEvidence.FrameBindingMismatch);
    }

    [Fact]
    public void RejectsAFrameWhoseWindowObservationsChanged()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();
        GameWindowSnapshot before = fixture.Frame.WindowSnapshotBefore;
        GameWindowSnapshot after = before with { ClientWidth = before.ClientWidth - 1 };
        CapturedFrame changed = SyntheticTerrainCellTargetFixtures.CreateFrame(
            fixture.Calibration,
            fixture.ClientAreaGeometry,
            before: before,
            after: after);
        fixture.SetFrame(changed);

        AssertUnverifiedWithoutPoint(fixture.Locate(), TargetEvidence.FrameBindingMismatch);
    }

    [Fact]
    public void RejectsAFrameWithAnUncapturedStatus()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();
        fixture.SetFrame(SyntheticTerrainCellTargetFixtures.CreateFrame(
            fixture.Calibration,
            fixture.ClientAreaGeometry,
            status: CaptureStatus.Failed));

        AssertUnverifiedWithoutPoint(fixture.Locate(), TargetEvidence.FrameUnusable);
    }

    [Fact]
    public void RejectsAnEmptyFrame()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();
        fixture.SetFrame(SyntheticTerrainCellTargetFixtures.CreateFrame(
            fixture.Calibration,
            fixture.ClientAreaGeometry,
            width: 0,
            height: 0,
            image: new Mat()));

        AssertUnverifiedWithoutPoint(fixture.Locate(), TargetEvidence.FrameUnusable);
    }

    [Fact]
    public void RejectsAnInvalidExclusionRectangle()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();

        AssertUnverifiedWithoutPoint(
            fixture.Locate(exclusions: [new WindowLocalRect(-1, 0, 10, 10)]),
            TargetEvidence.InvalidExclusion);
    }

    [Fact]
    public void RejectsANegativeSafetyMargin()
    {
        using SyntheticTerrainCellTargetFixtures fixture = new();

        AssertUnverifiedWithoutPoint(fixture.Locate(safetyMargin: -1), TargetEvidence.InvalidSafetyMargin);
    }

    private static void AssertUnverifiedWithoutPoint(TargetRegion target, TargetEvidence reason)
    {
        Assert.Equal(TargetStatus.Unverified, target.Status);
        Assert.Equal(reason, target.Reason);
        Assert.Null(target.ProposedClickPoint);
        Assert.Contains(reason, target.Confidence.Evidence);
    }
}
