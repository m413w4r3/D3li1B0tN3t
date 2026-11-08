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
    public void SearchRegionClippedByImageEdgeIsRejected()
    {
        using var template = SyntheticTargetLocatorFixtures.CreateTemplate();
        var mapper = SyntheticTargetLocatorFixtures.CreateMapper(translateX: -33, translateY: 244);
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
}
