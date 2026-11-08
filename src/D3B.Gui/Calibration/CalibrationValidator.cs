using System.Collections.Immutable;
using System.Globalization;
using OpenCvSharp;

namespace D3B.Gui.Calibration;

/// <summary>Fits affine projections and rejects profiles that fail geometry, metrics, or freshness checks.</summary>
public static class CalibrationValidator
{
    /// <summary>The default lifetime of a calibration profile.</summary>
    public const int CalibrationMaxAgeSeconds = 30;

    /// <summary>The initial RANSAC reprojection threshold, in pixels.</summary>
    public const double RansacReprojectionThresholdPixels = 3.0;

    /// <summary>The initial maximum median fit error, in pixels.</summary>
    public const double MaximumMedianFitErrorPixels = 4.0;

    /// <summary>The initial maximum validation residual, in pixels.</summary>
    public const double MaximumValidationErrorPixels = 8.0;

    /// <summary>The hard per-validation-point residual limit, in pixels.</summary>
    public const double MaximumIndividualValidationErrorPixels = 12.0;

    /// <summary>The minimum RANSAC inlier fraction.</summary>
    public const double MinimumInlierRatio = 0.80;

    /// <summary>The minimum fitting-anchor bounding-box area as a fraction of the image.</summary>
    public const double MinimumFitBoundingBoxAreaFraction = 0.10;

    /// <summary>The minimum fitting-anchor bounding-box width and height as image fractions.</summary>
    public const double MinimumFitBoundingBoxDimensionFraction = 0.20;

    /// <summary>The minimum number of image quadrants occupied by fitting anchors.</summary>
    public const int MinimumFitQuadrantCount = 3;

    private const ulong RansacSeed = 0x504E3243414CUL;
    private const ulong RansacMaxIterations = 5000;
    private const ulong RansacRefineIterations = 10;
    private const double RansacConfidence = 0.99;
    private const double ScaleRelativeTolerance = 1e-6;
    private const double GeometryRelativeTolerance = 1e-10;
    private static readonly object _ransacSync = new();

    /// <summary>Validates a version 1 annotation document and fits its independent affine model.</summary>
    /// <param name="document">The manually annotated or explicitly synthetic anchor document.</param>
    /// <param name="request">Expected build, map, measured capture geometry, and clock.</param>
    /// <returns>A profile only when every geometric and metric check passes.</returns>
    public static CalibrationResult Validate(CalibrationAnchorDocument document, CalibrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);

        var problems = ImmutableArray.CreateBuilder<CalibrationProblem>();
        var fitAnchors = document.Anchors.Where(anchor => anchor.Role == CalibrationAnchorRole.Fit).ToArray();
        var validationAnchors = document.Anchors.Where(anchor => anchor.Role == CalibrationAnchorRole.Validation).ToArray();
        var isSynthetic = document.Synthetic || request.IsSynthetic
            || document.Anchors.Any(anchor => anchor.Source == CalibrationAnchorSource.SyntheticFixture);
        var metrics = EmptyMetrics(fitAnchors.Length, validationAnchors.Length);

        if (document.MapId != request.MapId)
            AddProblem(problems, CalibrationProblemCode.MapChanged, "The annotation map does not match the expected map.");

        if (!string.Equals(document.ClientBuild, request.ClientBuild, StringComparison.Ordinal))
            AddProblem(problems, CalibrationProblemCode.ClientBuildChanged, "The annotation client build does not match the expected client build.");

        if (document.ImageWidth != request.CaptureWidth || document.ImageHeight != request.CaptureHeight)
            AddProblem(problems, CalibrationProblemCode.ResolutionChanged, "The annotation dimensions do not match the current captured image.");

        if (!double.IsFinite(request.DesktopScale) || request.DesktopScale <= 0)
            AddProblem(problems, CalibrationProblemCode.NonFiniteInput, "Desktop scale must be finite and positive.");

        if (request.CaptureWidth <= 0 || request.CaptureHeight <= 0 || request.MaxAge < TimeSpan.Zero)
            AddProblem(problems, CalibrationProblemCode.InvalidInput, "Capture dimensions and the maximum age must be valid.");

        if (!ClientAreaFits(request.ClientAreaGeometry, request.CaptureWidth, request.CaptureHeight))
            AddProblem(problems, CalibrationProblemCode.SceneContradiction, "The measured client area lies outside the captured window.");

        if (fitAnchors.Length < 8)
            AddProblem(problems, CalibrationProblemCode.TooFewFitAnchors, "At least eight fitting anchors are required.");

        if (validationAnchors.Length < 3)
            AddProblem(problems, CalibrationProblemCode.TooFewValidationAnchors, "At least three independent validation anchors are required.");

        if (validationAnchors.Select(anchor => anchor.Cell.Value).Distinct().Count() != validationAnchors.Length
            || validationAnchors.Any(anchor => fitAnchors.Any(fit => fit.Cell == anchor.Cell)))
        {
            AddProblem(problems, CalibrationProblemCode.DependentValidationAnchor, "Validation cells must be unique and independent from fitting cells.");
        }

        if (fitAnchors.Length < 3)
        {
            return MakeResult(ClassifyInitialProblems(problems), null, metrics, problems, isSynthetic);
        }

        if (!HasNonCollinearPoints(fitAnchors.Select(anchor => ((double)anchor.LogicalPoint.X, (double)anchor.LogicalPoint.Y)))
            || !HasNonCollinearPoints(fitAnchors.Select(anchor => ((double)anchor.MeasuredPixelPoint.X, (double)anchor.MeasuredPixelPoint.Y))))
        {
            AddProblem(problems, CalibrationProblemCode.CollinearAnchors, "Fitting anchors must span two dimensions in logical and measured coordinates.");
            return MakeResult(CalibrationStatus.Degenerate, null, metrics, problems, isSynthetic);
        }

        if (!AreAnchorsDistributed(fitAnchors, document.ImageWidth, document.ImageHeight))
        {
            AddProblem(problems, CalibrationProblemCode.ConcentratedAnchors, "Fitting anchors must cover at least 10% of the image, span 20% of both dimensions, and occupy three quadrants.");
        }

        var fitPoints = fitAnchors.Select(anchor => new Point2f(anchor.LogicalPoint.X, anchor.LogicalPoint.Y)).ToArray();
        var measuredFitPoints = fitAnchors.Select(anchor => new Point2f(anchor.MeasuredPixelPoint.X, anchor.MeasuredPixelPoint.Y)).ToArray();
        Mat? estimatedMatrix;
        using var inlierMat = new Mat();

        try
        {
            lock (_ransacSync)
            {
                Cv2.SetTheRNG(RansacSeed);
                var from = InputArray.Create(fitPoints);
                var to = InputArray.Create(measuredFitPoints);
                var inliers = OutputArray.Create(inlierMat);
                estimatedMatrix = Cv2.EstimateAffine2D(
                    from,
                    to,
                    inliers,
                    RobustEstimationAlgorithms.RANSAC,
                    RansacReprojectionThresholdPixels,
                    RansacMaxIterations,
                    RansacConfidence,
                    RansacRefineIterations);
            }
        }
        catch (OpenCVException exception)
        {
            AddProblem(problems, CalibrationProblemCode.SingularMatrix, $"OpenCV could not estimate the affine transform: {exception.Message}");
            return MakeResult(CalibrationStatus.Degenerate, null, metrics, problems, isSynthetic);
        }

        using var matrix = estimatedMatrix;
        if (matrix is null || matrix.Empty())
        {
            AddProblem(problems, CalibrationProblemCode.SingularMatrix, "RANSAC did not produce an affine matrix.");
            return MakeResult(CalibrationStatus.Degenerate, null, metrics, problems, isSynthetic);
        }

        var matrixRows = matrix.Rows;
        var matrixColumns = matrix.Cols;
        if (matrixRows != 2 || matrixColumns != 3 || matrix.Type() != MatType.CV_64FC1)
        {
            AddProblem(problems, CalibrationProblemCode.SingularMatrix, "RANSAC returned a matrix with an unexpected shape or type.");
            return MakeResult(CalibrationStatus.Degenerate, null, metrics, problems, isSynthetic);
        }

        if (!AffineScreenMapper.TryCreate(matrix, out var mapper) || mapper is null)
        {
            var coefficients = ReadMatrixCoefficients(matrix);
            if (coefficients.Any(coefficient => !double.IsFinite(coefficient)))
                AddProblem(problems, CalibrationProblemCode.NonFiniteInput, "RANSAC returned a non-finite affine coefficient.");
            else
                AddProblem(problems, CalibrationProblemCode.SingularMatrix, "RANSAC returned a singular affine transform.");

            return MakeResult(ClassifyInitialProblems(problems), null, metrics, problems, isSynthetic);
        }

        var inlierRows = inlierMat.Rows;
        var inlierColumns = inlierMat.Cols;
        if (inlierMat.Type() != MatType.CV_8UC1 || inlierRows != fitAnchors.Length || inlierColumns != 1)
        {
            AddProblem(problems, CalibrationProblemCode.SingularMatrix, "RANSAC returned an invalid inlier mask.");
            return MakeResult(CalibrationStatus.Degenerate, null, metrics, problems, isSynthetic);
        }

        var inlierIndexes = ImmutableArray.CreateBuilder<int>();
        var rejectedIndexes = ImmutableArray.CreateBuilder<int>();
        var allDocumentIndexes = document.Anchors
            .Select((anchor, index) => (anchor, index))
            .Where(item => item.anchor.Role == CalibrationAnchorRole.Fit)
            .Select(item => item.index)
            .ToArray();

        for (var index = 0; index < inlierRows; index++)
        {
            if (inlierMat.At<byte>(index) != 0)
                inlierIndexes.Add(index);
            else
                rejectedIndexes.Add(allDocumentIndexes[index]);
        }

        var inlierRatio = (double)inlierIndexes.Count / fitAnchors.Length;
        var fitResiduals = new double[fitAnchors.Length];
        var validationErrors = new double[validationAnchors.Length];
        var hasOutOfBounds = false;
        var hasNonFiniteProjection = false;
        var fitIndex = 0;
        foreach (var anchor in fitAnchors)
        {
            fitResiduals[fitIndex] = CalculateResidual(mapper, anchor, document.ImageWidth, document.ImageHeight, ref hasOutOfBounds, ref hasNonFiniteProjection);
            fitIndex++;
        }

        var validationIndex = 0;
        foreach (var anchor in validationAnchors)
        {
            validationErrors[validationIndex] = CalculateResidual(mapper, anchor, document.ImageWidth, document.ImageHeight, ref hasOutOfBounds, ref hasNonFiniteProjection);
            validationIndex++;
        }

        metrics = CreateMetrics(fitAnchors.Length, validationAnchors.Length, fitResiduals, validationErrors, inlierRatio, rejectedIndexes.ToImmutable());

        if (hasNonFiniteProjection)
            AddProblem(problems, CalibrationProblemCode.NonFiniteInput, "At least one affine projection or residual is not finite.");

        if (hasOutOfBounds)
            AddProblem(problems, CalibrationProblemCode.OutOfBounds, "At least one measured or projected anchor lies outside the captured image.");

        AddMetricProblems(metrics, problems);

        if (problems.Count != 0)
            return MakeResult(ClassifyInitialProblems(problems), null, metrics, problems, isSynthetic);

        var now = request.TimeProvider.GetUtcNow();
        var profile = new CalibrationProfile(
            document.ClientBuild,
            document.MapId,
            request.WindowIdentity,
            document.ImageWidth,
            document.ImageHeight,
            request.ClientAreaGeometry,
            request.DesktopScale,
            request.CalibrationVersion,
            mapper,
            document.Anchors,
            validationErrors.ToImmutableArray(),
            now,
            isSynthetic);

        return MakeResult(CalibrationStatus.Calibrated, profile, metrics, problems, isSynthetic);
    }

    /// <summary>Validates a profile against measured window, capture, map, version, and age state.</summary>
    /// <param name="profile">The profile to validate.</param>
    /// <param name="environment">The currently measured environment.</param>
    /// <param name="now">The current clock value.</param>
    /// <param name="maxAge">The maximum permitted profile age.</param>
    /// <returns>A current calibrated result or an invalidation result with metrics.</returns>
    public static CalibrationResult ValidateProfile(
        CalibrationProfile profile,
        CalibrationEnvironment environment,
        DateTimeOffset now,
        TimeSpan maxAge)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(environment);

        var problems = ImmutableArray.CreateBuilder<CalibrationProblem>();
        var fitAnchors = profile.Anchors.Where(anchor => anchor.Role == CalibrationAnchorRole.Fit).ToArray();
        var validationAnchors = profile.Anchors.Where(anchor => anchor.Role == CalibrationAnchorRole.Validation).ToArray();
        var metrics = EmptyMetrics(fitAnchors.Length, validationAnchors.Length);

        if (profile.WindowIdentity.Xid != environment.WindowIdentity.Xid
            || profile.WindowIdentity.ProcessId != environment.WindowIdentity.ProcessId
            || !string.Equals(profile.WindowIdentity.ExecutablePath, environment.WindowIdentity.ExecutablePath, StringComparison.Ordinal))
        {
            AddProblem(problems, CalibrationProblemCode.WindowReplaced, "The verified game window identity changed or is no longer verified.");
        }

        if (profile.CaptureWidth != environment.CaptureWidth || profile.CaptureHeight != environment.CaptureHeight)
            AddProblem(problems, CalibrationProblemCode.ResolutionChanged, "The captured image dimensions changed.");

        if (!double.IsFinite(environment.DesktopScale) || !double.IsFinite(profile.DesktopScale))
            AddProblem(problems, CalibrationProblemCode.NonFiniteInput, "The profile or environment desktop scale is not finite.");
        else if (!NearlyEqual(profile.DesktopScale, environment.DesktopScale, ScaleRelativeTolerance))
            AddProblem(problems, CalibrationProblemCode.ScaleChanged, "The measured desktop scale changed.");

        if (profile.ClientAreaGeometry != environment.ClientAreaGeometry)
            AddProblem(problems, CalibrationProblemCode.ClientAreaChanged, "The measured client-area position or dimensions changed.");

        if (profile.MapId != environment.MapId)
            AddProblem(problems, CalibrationProblemCode.MapChanged, "The active map changed.");

        if (profile.CalibrationVersion != environment.CalibrationVersion)
            AddProblem(problems, CalibrationProblemCode.CalibrationVersionChanged, "The calibration contract version changed.");

        if (maxAge < TimeSpan.Zero)
            AddProblem(problems, CalibrationProblemCode.InvalidInput, "The maximum profile age must not be negative.");
        else if (profile.CreatedAt > now)
            AddProblem(problems, CalibrationProblemCode.SceneContradiction, "The profile creation time is in the future.");
        else if (now - profile.CreatedAt > maxAge)
            AddProblem(problems, CalibrationProblemCode.Stale, "The calibration profile exceeded its permitted age.");

        if (environment.CaptureWidth <= 0 || environment.CaptureHeight <= 0
            || !ClientAreaFits(profile.ClientAreaGeometry, profile.CaptureWidth, profile.CaptureHeight))
        {
            AddProblem(problems, CalibrationProblemCode.SceneContradiction, "The stored client-area geometry contradicts the captured image.");
        }

        if (fitAnchors.Length < 8)
            AddProblem(problems, CalibrationProblemCode.TooFewFitAnchors, "The stored profile has fewer than eight fitting anchors.");

        if (validationAnchors.Length < 3)
            AddProblem(problems, CalibrationProblemCode.TooFewValidationAnchors, "The stored profile has fewer than three validation anchors.");

        if (validationAnchors.Select(anchor => anchor.Cell.Value).Distinct().Count() != validationAnchors.Length
            || validationAnchors.Any(anchor => fitAnchors.Any(fit => fit.Cell == anchor.Cell)))
        {
            AddProblem(problems, CalibrationProblemCode.DependentValidationAnchor, "Stored validation cells duplicate fitting or other validation cells.");
        }

        var fitResiduals = new double[fitAnchors.Length];
        var validationErrors = new double[validationAnchors.Length];
        var hasOutOfBounds = false;
        var hasNonFiniteProjection = false;
        var fitIndex = 0;
        foreach (var anchor in fitAnchors)
        {
            fitResiduals[fitIndex] = CalculateResidual(profile.Mapper, anchor, profile.CaptureWidth, profile.CaptureHeight, ref hasOutOfBounds, ref hasNonFiniteProjection);
            fitIndex++;
        }

        var validationIndex = 0;
        foreach (var anchor in validationAnchors)
        {
            validationErrors[validationIndex] = CalculateResidual(profile.Mapper, anchor, profile.CaptureWidth, profile.CaptureHeight, ref hasOutOfBounds, ref hasNonFiniteProjection);
            validationIndex++;
        }

        var rejectedIndexes = ImmutableArray.CreateBuilder<int>();
        var acceptedCount = 0;
        var profileFitIndex = 0;
        for (var profileAnchorIndex = 0; profileAnchorIndex < profile.Anchors.Length; profileAnchorIndex++)
        {
            var anchor = profile.Anchors[profileAnchorIndex];
            if (anchor.Role != CalibrationAnchorRole.Fit)
                continue;

            if (fitResiduals[profileFitIndex] <= RansacReprojectionThresholdPixels)
                acceptedCount++;
            else
                rejectedIndexes.Add(profileAnchorIndex);

            profileFitIndex++;
        }

        var inlierRatio = fitAnchors.Length == 0 ? 0 : (double)acceptedCount / fitAnchors.Length;
        metrics = CreateMetrics(fitAnchors.Length, validationAnchors.Length, fitResiduals, validationErrors, inlierRatio, rejectedIndexes.ToImmutable());

        if (hasNonFiniteProjection)
            AddProblem(problems, CalibrationProblemCode.NonFiniteInput, "At least one stored affine projection or residual is not finite.");

        if (hasOutOfBounds)
            AddProblem(problems, CalibrationProblemCode.OutOfBounds, "At least one stored annotation or projected anchor lies outside the image.");

        if (!profile.ValidationErrorsPixels.IsDefault
            && (profile.ValidationErrorsPixels.Length != validationErrors.Length
                || profile.ValidationErrorsPixels.Where((error, index) => index >= validationErrors.Length
                    || !NearlyEqual(error, validationErrors[index], 1e-6)).Any()))
        {
            AddProblem(problems, CalibrationProblemCode.SceneContradiction, "Stored validation residuals do not match the profile's anchors and mapper.");
        }

        if (fitAnchors.Length >= 8 && !AreAnchorsDistributed(fitAnchors, profile.CaptureWidth, profile.CaptureHeight))
            AddProblem(problems, CalibrationProblemCode.ConcentratedAnchors, "Stored fitting anchors no longer cover the required image area.");

        if (fitAnchors.Length >= 3
            && (!HasNonCollinearPoints(fitAnchors.Select(anchor => ((double)anchor.LogicalPoint.X, (double)anchor.LogicalPoint.Y)))
                || !HasNonCollinearPoints(fitAnchors.Select(anchor => ((double)anchor.MeasuredPixelPoint.X, (double)anchor.MeasuredPixelPoint.Y)))))
        {
            AddProblem(problems, CalibrationProblemCode.CollinearAnchors, "Stored fitting anchors no longer span two dimensions.");
        }

        AddMetricProblems(metrics, problems);

        if (problems.Count == 0)
            return MakeResult(CalibrationStatus.Calibrated, profile, metrics, problems, profile.IsSynthetic);

        var status = problems.All(problem => problem.Code == CalibrationProblemCode.Stale)
            ? CalibrationStatus.Stale
            : problems.Any(problem => problem.Code is CalibrationProblemCode.MedianErrorTooHigh
                or CalibrationProblemCode.ValidationMaxTooHigh
                or CalibrationProblemCode.ValidationPointTooFar
                or CalibrationProblemCode.InlierRatioTooLow)
                && problems.All(problem => problem.Code is CalibrationProblemCode.Stale
                    or CalibrationProblemCode.MedianErrorTooHigh
                    or CalibrationProblemCode.ValidationMaxTooHigh
                    or CalibrationProblemCode.ValidationPointTooFar
                    or CalibrationProblemCode.InlierRatioTooLow)
                    ? CalibrationStatus.MetricsFailed
                    : CalibrationStatus.Invalidated;

        return MakeResult(status, profile, metrics, problems, profile.IsSynthetic);
    }

    /// <summary>Validates a profile with the default 30-second lifetime.</summary>
    /// <param name="profile">The profile to validate.</param>
    /// <param name="environment">The currently measured environment.</param>
    /// <param name="now">The current clock value.</param>
    /// <returns>A current calibrated result or an invalidation result with metrics.</returns>
    public static CalibrationResult ValidateProfile(CalibrationProfile profile, CalibrationEnvironment environment, DateTimeOffset now)
    {
        return ValidateProfile(profile, environment, now, TimeSpan.FromSeconds(CalibrationMaxAgeSeconds));
    }

    private static CalibrationMetrics EmptyMetrics(int fitCount, int validationCount)
    {
        return new CalibrationMetrics(
            fitCount,
            validationCount,
            null,
            null,
            null,
            0,
            ImmutableArray<double>.Empty,
            ImmutableArray<double>.Empty,
            ImmutableArray<int>.Empty);
    }

    private static CalibrationMetrics CreateMetrics(
        int fitCount,
        int validationCount,
        double[] fitResiduals,
        double[] validationErrors,
        double inlierRatio,
        ImmutableArray<int> rejectedIndexes)
    {
        var fitValues = fitResiduals.ToImmutableArray();
        var validationValues = validationErrors.ToImmutableArray();
        double? medianFit = fitResiduals.Length == 0 ? null : Median(fitResiduals);
        double? maximumFit = fitResiduals.Length == 0 ? null : fitResiduals.Max();
        double? maximumValidation = validationErrors.Length == 0 ? null : validationErrors.Max();

        return new CalibrationMetrics(
            fitCount,
            validationCount,
            medianFit,
            maximumFit,
            maximumValidation,
            inlierRatio,
            fitValues,
            validationValues,
            rejectedIndexes);
    }

    private static double CalculateResidual(
        AffineScreenMapper mapper,
        CalibrationAnchor anchor,
        int imageWidth,
        int imageHeight,
        ref bool hasOutOfBounds,
        ref bool hasNonFiniteProjection)
    {
        var projected = mapper.Project(anchor.LogicalPoint);
        if (!double.IsFinite(projected.X) || !double.IsFinite(projected.Y))
        {
            hasNonFiniteProjection = true;
            return double.PositiveInfinity;
        }

        if (anchor.MeasuredPixelPoint.X < 0 || anchor.MeasuredPixelPoint.X >= imageWidth
            || anchor.MeasuredPixelPoint.Y < 0 || anchor.MeasuredPixelPoint.Y >= imageHeight
            || projected.X < 0 || projected.X >= imageWidth
            || projected.Y < 0 || projected.Y >= imageHeight)
        {
            hasOutOfBounds = true;
        }

        var deltaX = projected.X - anchor.MeasuredPixelPoint.X;
        var deltaY = projected.Y - anchor.MeasuredPixelPoint.Y;
        var residual = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (!double.IsFinite(residual))
        {
            hasNonFiniteProjection = true;
            return double.PositiveInfinity;
        }

        return residual;
    }

    private static bool AreAnchorsDistributed(CalibrationAnchor[] anchors, int imageWidth, int imageHeight)
    {
        if (anchors.Length == 0 || imageWidth <= 0 || imageHeight <= 0)
            return false;

        var minX = anchors.Min(anchor => anchor.MeasuredPixelPoint.X);
        var maxX = anchors.Max(anchor => anchor.MeasuredPixelPoint.X);
        var minY = anchors.Min(anchor => anchor.MeasuredPixelPoint.Y);
        var maxY = anchors.Max(anchor => anchor.MeasuredPixelPoint.Y);
        var width = maxX - minX;
        var height = maxY - minY;
        var areaFraction = ((double)width * height) / ((double)imageWidth * imageHeight);
        var quadrants = anchors
            .Select(anchor => (Right: anchor.MeasuredPixelPoint.X >= imageWidth / 2, Bottom: anchor.MeasuredPixelPoint.Y >= imageHeight / 2))
            .Distinct()
            .Count();

        return (double)width / imageWidth >= MinimumFitBoundingBoxDimensionFraction
            && (double)height / imageHeight >= MinimumFitBoundingBoxDimensionFraction
            && areaFraction >= MinimumFitBoundingBoxAreaFraction
            && quadrants >= MinimumFitQuadrantCount;
    }

    private static bool HasNonCollinearPoints(IEnumerable<(double X, double Y)> points)
    {
        var values = points.ToArray();
        if (values.Length < 3)
            return false;

        var meanX = values.Average(point => point.X);
        var meanY = values.Average(point => point.Y);
        var xx = 0.0;
        var yy = 0.0;
        var xy = 0.0;
        foreach (var point in values)
        {
            var deltaX = point.X - meanX;
            var deltaY = point.Y - meanY;
            xx += deltaX * deltaX;
            yy += deltaY * deltaY;
            xy += deltaX * deltaY;
        }

        var trace = xx + yy;
        var determinant = (xx * yy) - (xy * xy);
        return trace > 0 && determinant > GeometryRelativeTolerance * trace * trace;
    }

    private static bool ClientAreaFits(ClientAreaGeometry geometry, int captureWidth, int captureHeight)
    {
        return geometry.OriginInWindow.X >= 0
            && geometry.OriginInWindow.Y >= 0
            && geometry.Width > 0
            && geometry.Height > 0
            && (long)geometry.OriginInWindow.X + geometry.Width <= captureWidth
            && (long)geometry.OriginInWindow.Y + geometry.Height <= captureHeight;
    }

    private static bool NearlyEqual(double first, double second, double relativeTolerance)
    {
        if (!double.IsFinite(first) || !double.IsFinite(second))
            return false;

        var scale = Math.Max(1.0, Math.Max(Math.Abs(first), Math.Abs(second)));
        return Math.Abs(first - second) <= relativeTolerance * scale;
    }

    private static double Median(double[] values)
    {
        var ordered = values.Order().ToArray();
        var middle = ordered.Length / 2;
        if (ordered.Length % 2 != 0)
            return ordered[middle];

        return (ordered[middle - 1] + ordered[middle]) / 2.0;
    }

    private static double[] ReadMatrixCoefficients(Mat matrix)
    {
        return new[]
        {
            matrix.At<double>(0, 0),
            matrix.At<double>(0, 1),
            matrix.At<double>(0, 2),
            matrix.At<double>(1, 0),
            matrix.At<double>(1, 1),
            matrix.At<double>(1, 2),
        };
    }

    private static void AddMetricProblems(CalibrationMetrics metrics, ImmutableArray<CalibrationProblem>.Builder problems)
    {
        if (metrics.MedianFitErrorPixels is double median && median > MaximumMedianFitErrorPixels)
        {
            AddProblem(problems, CalibrationProblemCode.MedianErrorTooHigh,
                string.Create(CultureInfo.InvariantCulture, $"Median fit error {median:F3}px exceeds {MaximumMedianFitErrorPixels:F1}px."), median);
        }

        if (metrics.MaxValidationErrorPixels is double maximum && maximum > MaximumValidationErrorPixels)
        {
            AddProblem(problems, CalibrationProblemCode.ValidationMaxTooHigh,
                string.Create(CultureInfo.InvariantCulture, $"Maximum validation error {maximum:F3}px exceeds {MaximumValidationErrorPixels:F1}px."), maximum);
        }

        var largestValidationError = metrics.ValidationErrorsPixels.Length == 0 ? 0 : metrics.ValidationErrorsPixels.Max();
        if (largestValidationError > MaximumIndividualValidationErrorPixels)
        {
            AddProblem(problems, CalibrationProblemCode.ValidationPointTooFar,
                string.Create(CultureInfo.InvariantCulture, $"A validation point error {largestValidationError:F3}px exceeds {MaximumIndividualValidationErrorPixels:F1}px."), largestValidationError);
        }

        if (metrics.InlierRatio < MinimumInlierRatio)
        {
            AddProblem(problems, CalibrationProblemCode.InlierRatioTooLow,
                string.Create(CultureInfo.InvariantCulture, $"RANSAC inlier ratio {metrics.InlierRatio:P1} is below {MinimumInlierRatio:P0}."));
        }
    }

    private static void AddProblem(
        ImmutableArray<CalibrationProblem>.Builder problems,
        CalibrationProblemCode code,
        string detail,
        double? errorPixels = null)
    {
        problems.Add(new CalibrationProblem(code, detail, errorPixels));
    }

    private static CalibrationStatus ClassifyInitialProblems(ImmutableArray<CalibrationProblem>.Builder problems)
    {
        if (problems.Any(problem => problem.Code is CalibrationProblemCode.InvalidInput
            or CalibrationProblemCode.NonFiniteInput
            or CalibrationProblemCode.MapChanged
            or CalibrationProblemCode.ClientBuildChanged
            or CalibrationProblemCode.ResolutionChanged
            or CalibrationProblemCode.SceneContradiction))
        {
            return CalibrationStatus.InvalidInput;
        }

        if (problems.Any(problem => problem.Code is CalibrationProblemCode.TooFewFitAnchors or CalibrationProblemCode.TooFewValidationAnchors))
            return CalibrationStatus.InsufficientAnchors;

        if (problems.Any(problem => problem.Code is CalibrationProblemCode.CollinearAnchors
            or CalibrationProblemCode.ConcentratedAnchors
            or CalibrationProblemCode.SingularMatrix))
        {
            return CalibrationStatus.Degenerate;
        }

        if (problems.Any(problem => problem.Code == CalibrationProblemCode.OutOfBounds))
            return CalibrationStatus.OutOfBounds;

        return problems.Count == 0 ? CalibrationStatus.Calibrated : CalibrationStatus.MetricsFailed;
    }

    private static CalibrationResult MakeResult(
        CalibrationStatus status,
        CalibrationProfile? profile,
        CalibrationMetrics? metrics,
        ImmutableArray<CalibrationProblem>.Builder problems,
        bool isSynthetic)
    {
        return new CalibrationResult(status, profile, metrics, problems.ToImmutable(), isSynthetic);
    }
}
