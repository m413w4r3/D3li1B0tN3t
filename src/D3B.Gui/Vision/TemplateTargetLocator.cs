using System.Collections.Immutable;
using D3B.Engine.Maps;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Vision;

/// <summary>Finds a non-degenerate template with normalized correlation in a bounded cell-centered region.</summary>
public sealed class TemplateTargetLocator : ITargetLocator
{
    /// <summary>The initial minimum accepted normalized correlation score.</summary>
    public const double MinimumScore = 0.88;

    /// <summary>The initial minimum accepted difference between independent peaks.</summary>
    public const double MinimumMargin = 0.10;

    private const double TemplateVarianceEpsilon = 1e-6;

    /// <inheritdoc />
    public TargetRegion Locate(CapturedFrame frame, CalibrationProfile calibration, TargetExpectation expectation)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(expectation);

        var evidence = ImmutableArray.CreateBuilder<TargetEvidence>();
        var score = 0.0;
        var runnerUpScore = -1.0;
        var margin = 0.0;
        var projectedCenter = default(WindowLocalPixels);
        WindowLocalRect? boundingBox = null;
        var imageWidth = frame.Image.Cols;
        var imageHeight = frame.Image.Rows;

        if (frame.Status != CaptureStatus.Captured || !frame.ContentUsable
            || frame.Image.Empty() || imageWidth != frame.PixelWidth || imageHeight != frame.PixelHeight)
        {
            return Build(TargetStatus.Unverified, TargetEvidence.FrameUnusable);
        }

        if (frame.WindowIdentity != calibration.WindowIdentity
            || frame.PixelWidth != calibration.CaptureWidth
            || frame.PixelHeight != calibration.CaptureHeight
            || expectation.MapId != calibration.MapId)
        {
            return Build(TargetStatus.Unverified, TargetEvidence.CalibrationMismatch);
        }

        if (expectation.Template.Empty())
        {
            return Build(TargetStatus.Unverified, TargetEvidence.TemplateDegenerate);
        }

        var templateWidth = expectation.Template.Cols;
        var templateHeight = expectation.Template.Rows;

        if (expectation.Template.Type() != frame.Image.Type())
        {
            return Build(TargetStatus.Unverified, TargetEvidence.TemplateIncompatible);
        }

        if (!HasSpatialVariance(expectation.Template))
        {
            return Build(TargetStatus.Unverified, TargetEvidence.TemplateDegenerate);
        }

        try
        {
            projectedCenter = calibration.Mapper.Map(MapGeometry.PointOf(expectation.Cell.Value));
        }
        catch (OverflowException)
        {
            evidence.Add(TargetEvidence.ProjectedByCalibration);
            return Build(TargetStatus.Unverified, TargetEvidence.RegionOutOfFrame);
        }

        evidence.Add(TargetEvidence.ProjectedByCalibration);

        if (projectedCenter.X < 0 || projectedCenter.Y < 0
            || projectedCenter.X >= imageWidth || projectedCenter.Y >= imageHeight)
        {
            return Build(TargetStatus.Unverified, TargetEvidence.RegionOutOfFrame);
        }

        if (!TryCreateSearchRegion(projectedCenter, expectation.SearchRadiusPixels, imageWidth, imageHeight, out var region))
        {
            return Build(TargetStatus.Unverified, TargetEvidence.RegionOutOfFrame);
        }

        if (templateWidth > region.Width || templateHeight > region.Height)
        {
            return Build(TargetStatus.Unverified, TargetEvidence.TemplateTooLarge);
        }

        using var imageRegion = new Mat(frame.Image, region);
        using var response = new Mat();
        Cv2.MatchTemplate(imageRegion, expectation.Template, response, TemplateMatchModes.CCoeffNormed, default);

        var responseRows = response.Rows;
        var responseColumns = response.Cols;

        if (response.Empty() || responseRows <= 0 || responseColumns <= 0)
        {
            return Build(TargetStatus.Unverified, TargetEvidence.NoMatch);
        }

        Cv2.MinMaxLoc(response, out _, out var bestScore, out _, out var bestLocation);

        if (!double.IsFinite(bestScore))
        {
            return Build(TargetStatus.Unverified, TargetEvidence.NoMatch);
        }

        score = bestScore;
        runnerUpScore = FindIndependentRunnerUp(response, bestLocation, templateWidth, templateHeight);
        margin = score - runnerUpScore;
        evidence.Add(TargetEvidence.TemplateMatched);

        var candidate = new WindowLocalRect(
            region.X + bestLocation.X,
            region.Y + bestLocation.Y,
            templateWidth,
            templateHeight);
        boundingBox = candidate;

        if (score < MinimumScore)
        {
            return Build(TargetStatus.Unverified, TargetEvidence.ScoreTooLow);
        }

        if (margin < MinimumMargin)
        {
            return Build(TargetStatus.Ambiguous, TargetEvidence.AmbiguousMatch);
        }

        evidence.Add(TargetEvidence.UniquePeak);

        var candidateCenter = new WindowLocalPixels(
            candidate.X + (candidate.Width / 2),
            candidate.Y + (candidate.Height / 2));

        if (candidate.X < 0 || candidate.Y < 0
            || (long)candidate.X + candidate.Width > imageWidth
            || (long)candidate.Y + candidate.Height > imageHeight)
        {
            return Build(TargetStatus.Unverified, TargetEvidence.RegionOutOfFrame);
        }

        evidence.Add(TargetEvidence.InsideFrame);

        if (candidateCenter.X < expectation.MinMarginPixels
            || candidateCenter.Y < expectation.MinMarginPixels
            || candidateCenter.X >= imageWidth - expectation.MinMarginPixels
            || candidateCenter.Y >= imageHeight - expectation.MinMarginPixels)
        {
            return Build(TargetStatus.Unverified, TargetEvidence.InsufficientEdgeMargin);
        }

        foreach (var exclusion in expectation.Exclusions)
        {
            if (candidate.Intersects(exclusion))
            {
                return Build(TargetStatus.Unverified, TargetEvidence.BlockedByExclusion);
            }
        }

        evidence.Add(TargetEvidence.ClearOfExclusions);

        var dx = (double)candidateCenter.X - projectedCenter.X;
        var dy = (double)candidateCenter.Y - projectedCenter.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        if (distance > expectation.MaxDistanceFromProjectedCellPixels)
        {
            return Build(TargetStatus.Ambiguous, TargetEvidence.TooFarFromExpectedCell);
        }

        evidence.Add(TargetEvidence.NearExpectedCell);

        foreach (var otherCell in expectation.OtherElements)
        {
            WindowLocalPixels otherCenter;

            try
            {
                otherCenter = calibration.Mapper.Map(MapGeometry.PointOf(otherCell.Value));
            }
            catch (OverflowException)
            {
                return Build(TargetStatus.Ambiguous, TargetEvidence.AmbiguousMatch);
            }

            if (candidate.Contains(otherCenter))
            {
                return Build(TargetStatus.Ambiguous, TargetEvidence.AmbiguousMatch);
            }
        }

        var proposedClickPoint = candidateCenter;
        return Build(TargetStatus.Verified, null, proposedClickPoint);

        TargetRegion Build(TargetStatus status, TargetEvidence? reason, WindowLocalPixels? proposedClickPoint = null)
        {
            if (reason.HasValue)
            {
                evidence.Add(reason.Value);
            }

            var confidence = new TargetConfidence(
                score,
                runnerUpScore,
                margin,
                MinimumScore,
                MinimumMargin,
                evidence.ToImmutable());

            return new TargetRegion(
                expectation.MapId,
                expectation.ElementId,
                expectation.Cell,
                expectation.GfxId,
                expectation.ExpectedSkillId,
                projectedCenter,
                boundingBox,
                proposedClickPoint,
                confidence,
                status,
                reason,
                frame.CaptureId,
                calibration.CalibrationId);
        }
    }

    private static bool HasSpatialVariance(Mat template)
    {
        using var mean = new Mat();
        using var standardDeviation = new Mat();
        Cv2.MeanStdDev(template, mean, standardDeviation);

        var channels = template.Channels();

        for (var channel = 0; channel < channels; channel++)
        {
            if (standardDeviation.At<double>(0, channel) > TemplateVarianceEpsilon)
            {
                return true;
            }
        }

        return false;
    }

    private static double FindIndependentRunnerUp(Mat response, Point bestLocation, int templateWidth, int templateHeight)
    {
        using var suppressed = response.Clone();

        var responseRows = suppressed.Rows;
        var responseColumns = suppressed.Cols;
        var left = Math.Max(0, bestLocation.X - templateWidth + 1);
        var top = Math.Max(0, bestLocation.Y - templateHeight + 1);
        var right = Math.Min(responseColumns, bestLocation.X + templateWidth);
        var bottom = Math.Min(responseRows, bestLocation.Y + templateHeight);
        var suppressionWidth = right - left;
        var suppressionHeight = bottom - top;

        if (suppressionWidth <= 0 || suppressionHeight <= 0)
        {
            return -1.0;
        }

        using var suppressionRegion = new Mat(suppressed, new Rect(left, top, suppressionWidth, suppressionHeight));
        suppressionRegion.SetTo(new Scalar(-1));
        Cv2.MinMaxLoc(suppressed, out _, out var runnerUp, out _, out _);

        return double.IsFinite(runnerUp) ? runnerUp : -1.0;
    }

    private static bool TryCreateSearchRegion(
        WindowLocalPixels center,
        int radius,
        int imageWidth,
        int imageHeight,
        out Rect region)
    {
        var requestedLeft = (long)center.X - radius;
        var requestedTop = (long)center.Y - radius;
        var requestedRight = (long)center.X + radius + 1;
        var requestedBottom = (long)center.Y + radius + 1;
        var left = Math.Max(0L, requestedLeft);
        var top = Math.Max(0L, requestedTop);
        var right = Math.Min(imageWidth, requestedRight);
        var bottom = Math.Min(imageHeight, requestedBottom);

        region = default;

        if (left != requestedLeft || top != requestedTop || right != requestedRight || bottom != requestedBottom
            || right <= left || bottom <= top)
        {
            return false;
        }

        region = new Rect((int)left, (int)top, (int)(right - left), (int)(bottom - top));
        return true;
    }
}
