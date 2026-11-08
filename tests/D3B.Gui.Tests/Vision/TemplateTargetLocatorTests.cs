using D3B.Engine.Maps;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Tests.Vision;

/// <summary>Tests target matching against deterministic SYNTHETIC pixel fixtures only.</summary>
[Trait("Fixture", "SYNTHETIC")]
public sealed class TemplateTargetLocatorTests
{
    [Fact]
    public void LocatesDistinctiveTemplateInBoundedCalibratedRegion()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var expectedBounds = SyntheticTargetLocatorFixtures.CenteredTargetBounds(calibration);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, expectedBounds);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Verified, result.Status);
        Assert.Null(result.Reason);
        Assert.Equal(expectedBounds, result.VisualBoundingBox);
        Assert.Equal(
            new WindowLocalPixels(expectedBounds.X + (expectedBounds.Width / 2), expectedBounds.Y + (expectedBounds.Height / 2)),
            result.ProposedClickPoint);
        Assert.True(result.ProposedClickPoint!.Value.X >= 8);
        Assert.True(result.ProposedClickPoint.Value.Y >= 8);
        Assert.True(result.ProposedClickPoint.Value.X < frame.PixelWidth - 8);
        Assert.True(result.ProposedClickPoint.Value.Y < frame.PixelHeight - 8);
        Assert.True(result.Confidence.Score >= 0.88);
        Assert.Contains(TargetEvidence.ProjectedByCalibration, result.Confidence.Evidence);
        Assert.Contains(TargetEvidence.UniquePeak, result.Confidence.Evidence);
        Assert.Contains(TargetEvidence.InsideFrame, result.Confidence.Evidence);
        Assert.Contains(TargetEvidence.ClearOfExclusions, result.Confidence.Evidence);
        Assert.Contains(TargetEvidence.NearExpectedCell, result.Confidence.Evidence);
        Assert.Equal(frame.CaptureId, result.CaptureId);
        Assert.Equal(calibration.CalibrationId, result.CalibrationId);
        Assert.Equal(frame.PixelWidth, result.FrameWidth);
        Assert.Equal(frame.PixelHeight, result.FrameHeight);
        Assert.Equal(SyntheticTargetLocatorFixtures.ProjectedCellCenter(calibration), result.ExpectedVisualCenter);
        Assert.Equal(expectation.MapId, result.MapId);
        Assert.Equal(expectation.ElementId, result.ElementId);
        Assert.Equal(expectation.Cell, result.Cell);
        Assert.Equal(expectation.GfxId, result.GfxId);
        Assert.Equal(expectation.ExpectedSkillId, result.ExpectedSkillId);
    }

    [Fact]
    public void MissingTemplateReturnsUnverifiedWithoutFallbackPoint()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image);
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Null(result.ProposedClickPoint);
        Assert.Contains(TargetEvidence.ScoreTooLow, result.Confidence.Evidence);
    }

    [Fact]
    public void SimilarButDifferentTemplateStaysBelowTheScoreThreshold()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        using var similar = SyntheticTargetLocatorFixtures.CreateTemplate(similarVariant: true);
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var bounds = SyntheticTargetLocatorFixtures.CenteredTargetBounds(calibration);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(similar, bounds);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Null(result.ProposedClickPoint);
        Assert.Contains(TargetEvidence.ScoreTooLow, result.Confidence.Evidence);
        Assert.True(result.Confidence.Score < result.Confidence.RequiredScore);
    }

    [Fact]
    public void ExcludedTargetReturnsBlockedWithoutFallbackPoint()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var bounds = SyntheticTargetLocatorFixtures.CenteredTargetBounds(calibration);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, bounds);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template, exclusions: [bounds]);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.BlockedByExclusion, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void TwoIdenticalCopiesProduceAnAmbiguousPeak()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var first = SyntheticTargetLocatorFixtures.CenteredTargetBounds(calibration);
        var second = first with { X = first.X + 40 };
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, first, second);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Ambiguous, result.Status);
        Assert.Equal(TargetEvidence.AmbiguousMatch, result.Reason);
        Assert.Null(result.ProposedClickPoint);
        Assert.True(result.Confidence.Margin < result.Confidence.RequiredMargin);
    }

    [Fact]
    public void OtherKnownElementProjectedInsideCandidateMakesItAmbiguous()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var bounds = SyntheticTargetLocatorFixtures.CenteredTargetBounds(calibration);
        var adjacentCell = new LogicalCell(MapGeometry.Neighbour(SyntheticTargetLocatorFixtures.CellId, 0));
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, bounds);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template, otherElements: [adjacentCell]);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Ambiguous, result.Status);
        Assert.Equal(TargetEvidence.AmbiguousMatch, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void ConstantTemplateIsRejectedAsDegenerate()
    {
        using var template = new Mat(
            SyntheticTargetLocatorFixtures.TemplateHeight,
            SyntheticTargetLocatorFixtures.TemplateWidth,
            MatType.CV_8UC3,
            Scalar.All(80));
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image);
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.TemplateDegenerate, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void ProjectedCellOutsideImageIsRejected()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var mapper = SyntheticTargetLocatorFixtures.CreateMapper(translateX: -500, translateY: -500);
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration(mapper);
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.RegionOutOfFrame, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void SearchRegionClipsSafelyAtImageEdgeWithoutAProposal()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var mapper = SyntheticTargetLocatorFixtures.CreateMapper(translateX: -33, translateY: 244);
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration(mapper);
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.ScoreTooLow, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void CandidateWithoutConfiguredEdgeMarginIsRejected()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var mapper = SyntheticTargetLocatorFixtures.CreateMapper(translateX: -28, translateY: 244);
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration(mapper);
        var bounds = new WindowLocalRect(8, 190, template.Cols, template.Rows);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, bounds);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(
            template,
            searchRadiusPixels: 20,
            minMarginPixels: 24);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.InsufficientEdgeMargin, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void UncapturedFrameIsRejectedAsUnusable()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, status: CaptureStatus.Failed, contentUsable: false);
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.FrameUnusable, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void CalibrationDimensionMismatchIsRejected()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration(captureWidth: 639);
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.CalibrationMismatch, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void CalibrationWindowIdentityMismatchIsRejected()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var otherIdentity = SyntheticTargetLocatorFixtures.WindowIdentity with { Xid = 452 };
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration, identity: otherIdentity);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.CalibrationMismatch, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void CalibrationWindowTitleChangeDoesNotInvalidateTarget()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var expectedBounds = SyntheticTargetLocatorFixtures.CenteredTargetBounds(calibration);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, expectedBounds);
        var renamedIdentity = SyntheticTargetLocatorFixtures.WindowIdentity with { Title = "renamed synthetic window" };
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration, identity: renamedIdentity);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Verified, result.Status);
        Assert.Null(result.Reason);
        Assert.DoesNotContain(TargetEvidence.CalibrationMismatch, result.Confidence.Evidence);
    }

    [Fact]
    public void CalibrationProcessIdMismatchIsRejected()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var otherIdentity = SyntheticTargetLocatorFixtures.WindowIdentity with { ProcessId = 9002 };
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration, identity: otherIdentity);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.CalibrationMismatch, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void CalibrationStatusChangeDoesNotInvalidateTargetWhenProcessIdentityIsUnavailable()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var expectedBounds = SyntheticTargetLocatorFixtures.CenteredTargetBounds(calibration);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, expectedBounds);
        var unavailableIdentity = SyntheticTargetLocatorFixtures.WindowIdentity with
        {
            ProcessId = null,
            ExecutablePath = null,
            Status = GameWindowIdentityStatus.UnverifiedProcessIdUnavailable,
        };
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration, identity: unavailableIdentity);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Verified, result.Status);
        Assert.Null(result.Reason);
        Assert.DoesNotContain(TargetEvidence.CalibrationMismatch, result.Confidence.Evidence);
    }

    [Fact]
    public void CalibrationMapMismatchIsRejected()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template, mapId: calibration.MapId + 1);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.CalibrationMismatch, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void TargetOutsideBoundedSearchRegionIsNotFound()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var center = SyntheticTargetLocatorFixtures.ProjectedCellCenter(calibration);
        var distantTarget = new WindowLocalRect(center.X + 120, center.Y - 10, template.Cols, template.Rows);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, distantTarget);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Null(result.ProposedClickPoint);
        Assert.Contains(TargetEvidence.ScoreTooLow, result.Confidence.Evidence);
    }

    [Fact]
    public void CandidateBeyondMaximumProjectedCellDistanceIsAmbiguous()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var center = SyntheticTargetLocatorFixtures.ProjectedCellCenter(calibration);
        var distantTarget = new WindowLocalRect(center.X + 72, center.Y - 10, template.Cols, template.Rows);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, distantTarget);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Ambiguous, result.Status);
        Assert.Equal(TargetEvidence.TooFarFromExpectedCell, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void TemplateLargerThanSearchRegionIsRejected()
    {
        using var template = new Mat(200, 200, MatType.CV_8UC3, Scalar.All(10));
        template.Set(0, 0, new Vec3b(0, 0, 0));
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image);
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template, searchRadiusPixels: 32);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.TemplateTooLarge, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void TallTemplateRequiresAnAnchorAndLargerSearchRadius()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate(75, 130);
        var projectedCenter = new WindowLocalPixels(1591, 824);
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibrationAt(projectedCenter);
        var bounds = new WindowLocalRect(1554, 705, 75, 130);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(1920, 1175, template, bounds);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var defaultExpectation = SyntheticTargetLocatorFixtures.CreateExpectation(template);
        var adjustedExpectation = SyntheticTargetLocatorFixtures.CreateExpectation(
            template,
            searchRadiusPixels: 128,
            anchorOffset: new WindowLocalOffset(0, -14));

        var defaultResult = new TemplateTargetLocator().Locate(frame, calibration, defaultExpectation);
        var adjustedResult = new TemplateTargetLocator().Locate(frame, calibration, adjustedExpectation);

        Assert.Equal(TargetStatus.Unverified, defaultResult.Status);
        Assert.Null(defaultResult.ProposedClickPoint);
        Assert.Contains(TargetEvidence.ScoreTooLow, defaultResult.Confidence.Evidence);
        Assert.Equal(TargetStatus.Verified, adjustedResult.Status);
        Assert.Equal(bounds, adjustedResult.VisualBoundingBox);
        Assert.Equal(new WindowLocalPixels(1591, 810), adjustedResult.ExpectedVisualCenter);
        Assert.Equal(new WindowLocalPixels(1591, 770), adjustedResult.ProposedClickPoint);
    }

    [Fact]
    public void CandidateDistanceUsesTheAnchorAdjustedVisualCenter()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var projectedCenter = SyntheticTargetLocatorFixtures.ProjectedCellCenter();
        var expectedCenter = new WindowLocalPixels(projectedCenter.X, projectedCenter.Y - 40);
        var targetCenter = new WindowLocalPixels(projectedCenter.X, projectedCenter.Y - 90);
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var bounds = new WindowLocalRect(
            targetCenter.X - (template.Cols / 2),
            targetCenter.Y - (template.Rows / 2),
            template.Cols,
            template.Rows);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, bounds);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(
            template,
            searchRadiusPixels: 128,
            maxCenterDistancePixels: 64,
            anchorOffset: new WindowLocalOffset(0, -40));

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Verified, result.Status);
        Assert.Equal(expectedCenter, result.ExpectedVisualCenter);
        Assert.Equal(targetCenter, result.ProposedClickPoint);
    }

    [Fact]
    public void TargetSpecificClickOffsetMovesTheProposedPoint()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var bounds = SyntheticTargetLocatorFixtures.CenteredTargetBounds(calibration);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, bounds);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(
            template,
            clickOffset: new WindowLocalOffset(3, -4));

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Verified, result.Status);
        var projectedCenter = SyntheticTargetLocatorFixtures.ProjectedCellCenter(calibration);
        var expectedClickPoint = new WindowLocalPixels(projectedCenter.X + 3, projectedCenter.Y - 4);
        Assert.Equal(expectedClickPoint, result.ProposedClickPoint);
    }

    [Fact]
    public void TopLeftNameplateExclusionBlocksCandidate()
    {
        AssertExclusionBlocksCandidate(
            new WindowLocalPixels(120, 80),
            new WindowLocalRect(0, 0, 220, 120));
    }

    [Fact]
    public void TopRightIconsExclusionBlocksCandidate()
    {
        AssertExclusionBlocksCandidate(
            new WindowLocalPixels(1810, 45),
            new WindowLocalRect(1690, 0, 230, 100));
    }

    [Fact]
    public void RightSideBarExclusionBlocksCandidate()
    {
        AssertExclusionBlocksCandidate(
            new WindowLocalPixels(1900, 600),
            new WindowLocalRect(1880, 100, 40, 900));
    }

    [Fact]
    public void ClickPointInsideExclusionBlocksCandidateEvenWhenItsBoxIsClear()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var bounds = SyntheticTargetLocatorFixtures.CenteredTargetBounds(calibration);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, bounds);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var projectedCenter = SyntheticTargetLocatorFixtures.ProjectedCellCenter(calibration);
        var clickPoint = new WindowLocalPixels(projectedCenter.X + 100, projectedCenter.Y);
        var exclusion = new WindowLocalRect(clickPoint.X - 4, clickPoint.Y - 4, 8, 8);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(
            template,
            exclusions: [exclusion],
            clickOffset: new WindowLocalOffset(100, 0));

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.BlockedByExclusion, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void ClickOffsetOutsideFrameIsRejectedWithoutAProposal()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        var bounds = SyntheticTargetLocatorFixtures.CenteredTargetBounds(calibration);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(template, bounds);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(
            template,
            clickOffset: new WindowLocalOffset(8192, 0));

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.ClickPointOutOfFrame, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void OffsetOverflowIsRejectedWithoutAProposal()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var logicalPoint = MapGeometry.PointOf(SyntheticTargetLocatorFixtures.CellId);
        Assert.True(AffineScreenMapper.TryCreate(
            [1, 0, int.MaxValue - logicalPoint.X - 50, 0, 1, 200 - logicalPoint.Y],
            out var mapper));
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration(mapper);
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(
            template,
            anchorOffset: new WindowLocalOffset(100, 0));

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.InvalidOffset, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    [Fact]
    public void InvalidOffsetsSearchBoundsAndExclusionRectanglesAreRejected()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();

        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowLocalOffset(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowLocalOffset(0, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowLocalOffset(8193, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SyntheticTargetLocatorFixtures.CreateExpectation(template, searchRadiusPixels: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SyntheticTargetLocatorFixtures.CreateExpectation(template, searchRadiusPixels: 4097));
        Assert.Throws<ArgumentOutOfRangeException>(() => SyntheticTargetLocatorFixtures.CreateExpectation(template, maxCenterDistancePixels: 8193));
        Assert.Throws<ArgumentException>(() => SyntheticTargetLocatorFixtures.CreateExpectation(template, exclusions: [new WindowLocalRect(1, 1, 0, 5)]));
        Assert.Throws<ArgumentException>(() => SyntheticTargetLocatorFixtures.CreateExpectation(template, exclusions: [new WindowLocalRect(int.MaxValue, 0, 2, 1)]));
        Assert.Throws<ArgumentException>(() => SyntheticTargetLocatorFixtures.CreateExpectation(template, exclusions: [new WindowLocalRect(-1, 0, 2, 1)]));
        AssertInvalidExclusionOutsideFrame(template);
    }

    private static void AssertExclusionBlocksCandidate(WindowLocalPixels center, WindowLocalRect exclusion)
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibrationAt(center);
        Assert.Equal(center, SyntheticTargetLocatorFixtures.ProjectedCellCenter(calibration));
        var bounds = new WindowLocalRect(
            center.X - (template.Cols / 2),
            center.Y - (template.Rows / 2),
            template.Cols,
            template.Rows);
        using var image = SyntheticTargetLocatorFixtures.CreateImage(1920, 1175, template, bounds);
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(template, exclusions: [exclusion]);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.BlockedByExclusion, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }

    private static void AssertInvalidExclusionOutsideFrame(Mat template)
    {
        var calibration = SyntheticTargetLocatorFixtures.CreateCalibration();
        using var image = SyntheticTargetLocatorFixtures.CreateImage();
        using var frame = SyntheticTargetLocatorFixtures.CreateFrame(image, calibration);
        var expectation = SyntheticTargetLocatorFixtures.CreateExpectation(
            template,
            exclusions: [new WindowLocalRect(frame.PixelWidth - 4, 0, 8, 8)]);

        var result = new TemplateTargetLocator().Locate(frame, calibration, expectation);

        Assert.Equal(TargetStatus.Unverified, result.Status);
        Assert.Equal(TargetEvidence.InvalidExclusion, result.Reason);
        Assert.Null(result.ProposedClickPoint);
    }
}
