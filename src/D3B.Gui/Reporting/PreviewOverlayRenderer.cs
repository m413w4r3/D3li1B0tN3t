using System.Globalization;
using D3B.Engine.Maps;
using D3B.Gui.Calibration;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Reporting;

/// <summary>Renders calibration and target evidence on a new copy of a local image.</summary>
public static class PreviewOverlayRenderer
{
    private static readonly Scalar _gridColor = new(72, 72, 72);
    private static readonly Scalar _fitColor = new(0, 220, 0);
    private static readonly Scalar _validationColor = new(255, 110, 0);
    private static readonly Scalar _rejectedColor = new(0, 0, 255);
    private static readonly Scalar _targetColor = new(0, 255, 255);
    private static readonly Scalar _unverifiedTargetColor = new(0, 140, 255);
    private static readonly Scalar _textColor = new(245, 245, 245);

    /// <summary>Draws projected cells, calibration anchors, residuals, and optional target evidence.</summary>
    /// <param name="image">The caller-owned input image.</param>
    /// <param name="anchorDocument">The manual or synthetic anchor source.</param>
    /// <param name="calibration">The quantitative calibration outcome.</param>
    /// <param name="target">The optional target localization result.</param>
    /// <returns>A new annotated image. The caller owns and must dispose it.</returns>
    public static Mat Render(
        Mat image,
        CalibrationAnchorDocument anchorDocument,
        CalibrationResult calibration,
        TargetRegion? target = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(anchorDocument);
        ArgumentNullException.ThrowIfNull(calibration);

        if (image.Empty())
        {
            throw new ArgumentException("The preview image must not be empty.", nameof(image));
        }

        var output = image.Clone();
        var draw = InputOutputArray.Create(output);

        if (calibration.IsCalibrated && calibration.Profile is not null)
        {
            DrawProjectedGrid(draw, output, calibration.Profile.Mapper);
        }

        DrawAnchors(draw, output, anchorDocument, calibration);
        DrawTarget(draw, target);
        DrawIdentity(draw, output, anchorDocument, calibration, target);

        return output;
    }

    private static void DrawProjectedGrid(InputOutputArray draw, Mat image, AffineScreenMapper mapper)
    {
        var cellCount = MapGeometry.Rows * MapGeometry.Columns;
        for (var cellId = 0; cellId < cellCount; cellId++)
        {
            if (!TryProject(mapper, cellId, out var from) || !IsNearImage(from, image))
            {
                continue;
            }

            for (var direction = 0; direction < 4; direction++)
            {
                var neighbour = MapGeometry.Neighbour(cellId, direction);
                if (neighbour < 0 || !TryProject(mapper, neighbour, out var to) || !IsNearImage(to, image))
                {
                    continue;
                }

                Cv2.Line(draw, from, to, _gridColor, 1, LineTypes.AntiAlias);
            }
        }
    }

    private static void DrawAnchors(InputOutputArray draw, Mat image, CalibrationAnchorDocument document, CalibrationResult calibration)
    {
        var metrics = calibration.Metrics;
        var rejectedIndexes = metrics is null
            ? new HashSet<int>()
            : metrics.RejectedAnchorIndexes.ToHashSet();
        var fitOrdinal = 0;
        var validationOrdinal = 0;

        for (var anchorIndex = 0; anchorIndex < document.Anchors.Length; anchorIndex++)
        {
            var anchor = document.Anchors[anchorIndex];
            Scalar color;
            string label;

            if (anchor.Role == CalibrationAnchorRole.Fit)
            {
                var rejected = rejectedIndexes.Contains(anchorIndex);
                color = rejected ? _rejectedColor : _fitColor;
                var residual = metrics is not null && fitOrdinal < metrics.FitResidualsPixels.Length
                    ? metrics.FitResidualsPixels[fitOrdinal]
                    : (double?)null;
                label = residual.HasValue
                    ? string.Create(CultureInfo.InvariantCulture, $"{(rejected ? "REJECTED" : "FIT")} {residual.Value:F1}px")
                    : rejected ? "REJECTED" : "FIT";
                fitOrdinal++;
            }
            else
            {
                color = _validationColor;
                var residual = metrics is not null && validationOrdinal < metrics.ValidationErrorsPixels.Length
                    ? metrics.ValidationErrorsPixels[validationOrdinal]
                    : (double?)null;
                label = residual.HasValue
                    ? string.Create(CultureInfo.InvariantCulture, $"VALIDATION {residual.Value:F1}px")
                    : "VALIDATION";
                validationOrdinal++;
            }

            var point = new Point(anchor.MeasuredPixelPoint.X, anchor.MeasuredPixelPoint.Y);
            Cv2.Circle(draw, point, 5, color, 2, LineTypes.AntiAlias);
            var textPoint = new Point(point.X + 6, Math.Max(12, point.Y - 6));
            DrawText(draw, label, textPoint, color);
        }
    }

    private static void DrawTarget(InputOutputArray draw, TargetRegion? target)
    {
        if (target?.VisualBoundingBox is not WindowLocalRect bounds)
        {
            return;
        }

        var color = target.Status == TargetStatus.Verified ? _targetColor : _unverifiedTargetColor;
        var rectangle = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        Cv2.Rectangle(draw, rectangle, color, 2, LineTypes.AntiAlias);

        if (target.Status == TargetStatus.Verified && target.ProposedClickPoint is WindowLocalPixels clickPoint)
        {
            Cv2.Circle(draw, new Point(clickPoint.X, clickPoint.Y), 7, _targetColor, 2, LineTypes.AntiAlias);
        }
    }

    private static void DrawIdentity(
        InputOutputArray draw,
        Mat image,
        CalibrationAnchorDocument document,
        CalibrationResult calibration,
        TargetRegion? target)
    {
        var synthetic = document.Synthetic || calibration.IsSynthetic
            || document.Anchors.Any(anchor => anchor.Source == CalibrationAnchorSource.SyntheticFixture);
        var line = 22;
        if (synthetic)
        {
            DrawText(draw, "SYNTHETIC", new Point(12, line), _targetColor);
            line += 23;
        }

        var profile = calibration.Profile;
        var status = calibration.Status.ToString();
        var identity = profile is null
            ? $"Calibration {status} map {document.MapId} build {document.ClientBuild}"
            : $"Calibration {status} map {profile.MapId} build {profile.ClientBuild} id {profile.CalibrationId[..12]}";
        DrawText(draw, identity, new Point(12, Math.Min(line, image.Rows - 4)), _textColor);

        if (target is not null)
        {
            var targetLine = Math.Min(line + 23, image.Rows - 4);
            DrawText(draw, $"Target {target.Status} element {target.ElementId} cell {target.Cell.Value}", new Point(12, targetLine), _textColor);
        }
    }

    private static void DrawText(InputOutputArray draw, string text, Point position, Scalar color)
    {
        Cv2.PutText(draw, text, position, HersheyFonts.HersheySimplex, 0.45, color, 1, LineTypes.AntiAlias);
    }

    private static bool TryProject(AffineScreenMapper mapper, int cellId, out Point point)
    {
        try
        {
            var pixels = mapper.Map(MapGeometry.PointOf(cellId));
            point = new Point(pixels.X, pixels.Y);
            return true;
        }
        catch (OverflowException)
        {
            point = default;
            return false;
        }
    }

    private static bool IsNearImage(Point point, Mat image)
    {
        return point.X >= -image.Cols && point.Y >= -image.Rows
            && point.X <= image.Cols * 2 && point.Y <= image.Rows * 2;
    }
}
