using System.Collections.Immutable;
using System.Text;
using D3B.Engine.Maps;
using D3B.Gui.Calibration;
using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Calibration;

public sealed class CalibrationValidatorTests
{
    [Fact]
    public void AnExactAffineTransformRecoversAllSixParameters()
    {
        var result = SyntheticCalibrationFixtures.Calibrate();

        Assert.True(result.IsCalibrated);
        Assert.Equal(CalibrationStatus.Calibrated, result.Status);
        Assert.NotNull(result.Profile);
        AssertClose(15, result.Profile.Mapper.A, 1e-5);
        AssertClose(8, result.Profile.Mapper.B, 1e-5);
        AssertClose(70, result.Profile.Mapper.Tx, 1e-5);
        AssertClose(8, result.Profile.Mapper.C, 1e-5);
        AssertClose(-3, result.Profile.Mapper.D, 1e-5);
        AssertClose(80, result.Profile.Mapper.Ty, 1e-5);
        AssertClose(0, result.Metrics!.MedianFitErrorPixels!.Value, 1e-4);
        AssertClose(0, result.Metrics.MaxValidationErrorPixels!.Value, 1e-4);
    }

    [Fact]
    public void PreviewCalibrationRetainsAnUnverifiedWindowIdentity()
    {
        var identity = new GameWindowIdentity(
            451,
            null,
            "/synthetic/dofus",
            "synthetic title",
            GameWindowIdentityStatus.UnverifiedProcessIdUnavailable);
        var result = SyntheticCalibrationFixtures.Calibrate(request: SyntheticCalibrationFixtures.CreateRequest(windowIdentity: identity));

        Assert.True(result.IsCalibrated);
        Assert.NotNull(result.Profile);
        Assert.Equal(GameWindowIdentityStatus.UnverifiedProcessIdUnavailable, result.Profile.WindowIdentity.Status);

        var environment = SyntheticCalibrationFixtures.CreateEnvironment(result.Profile);
        var validated = CalibrationValidator.ValidateProfile(result.Profile, environment, result.Profile.CreatedAt, TimeSpan.FromSeconds(30));
        Assert.True(validated.IsCalibrated);
        Assert.Equal(GameWindowIdentityStatus.UnverifiedProcessIdUnavailable, validated.Profile!.WindowIdentity.Status);
    }

    [Fact]
    public void DeterministicPixelNoiseStaysWithinTheInitialErrorThresholds()
    {
        var result = SyntheticCalibrationFixtures.Calibrate(SyntheticCalibrationFixtures.CreateDocument(noisy: true));

        Assert.True(result.IsCalibrated);
        Assert.True(result.Metrics!.MedianFitErrorPixels <= CalibrationValidator.MaximumMedianFitErrorPixels);
        Assert.True(result.Metrics.MaxValidationErrorPixels <= CalibrationValidator.MaximumValidationErrorPixels);
        Assert.All(result.Metrics.ValidationErrorsPixels, error => Assert.True(error <= CalibrationValidator.MaximumIndividualValidationErrorPixels));
    }

    [Fact]
    public void RansacRejectsTwoOfTwelveOutliersAndKeepsTheAffineFit()
    {
        var result = SyntheticCalibrationFixtures.Calibrate(SyntheticCalibrationFixtures.CreateDocument(outliers: true));

        Assert.Equal(CalibrationStatus.Calibrated, result.Status);
        Assert.NotNull(result.Profile);
        AssertClose(15, result.Profile.Mapper.A, 1e-3);
        AssertClose(8, result.Profile.Mapper.B, 1e-3);
        AssertClose(70, result.Profile.Mapper.Tx, 1e-2);
        AssertClose(8, result.Profile.Mapper.C, 1e-3);
        AssertClose(-3, result.Profile.Mapper.D, 1e-3);
        AssertClose(80, result.Profile.Mapper.Ty, 1e-2);
        Assert.True(result.Metrics!.InlierRatio >= 0.80);
        Assert.Equal(2, result.Metrics.RejectedAnchorIndexes.Length);
        Assert.Contains(2, result.Metrics.RejectedAnchorIndexes);
        Assert.Contains(10, result.Metrics.RejectedAnchorIndexes);
    }

    [Fact]
    public void RansacRejectsAProfileBelowTheMinimumInlierRatio()
    {
        var original = SyntheticCalibrationFixtures.CreateDocument();
        var anchors = original.Anchors.ToBuilder();
        for (var index = 0; index < 4; index++)
        {
            var anchor = anchors[index];
            anchors[index] = new CalibrationAnchor(
                anchor.Cell,
                anchor.LogicalPoint,
                new WindowLocalPixels(anchor.MeasuredPixelPoint.X + 100, anchor.MeasuredPixelPoint.Y + 90),
                anchor.Role,
                anchor.Source);
        }

        var document = new CalibrationAnchorDocument(
            original.ClientBuild,
            original.MapId,
            original.ImageWidth,
            original.ImageHeight,
            anchors.ToImmutable(),
            original.Synthetic);
        var result = SyntheticCalibrationFixtures.Calibrate(document);

        AssertProblem(result, CalibrationProblemCode.InlierRatioTooLow);
        Assert.True(result.Metrics!.InlierRatio < CalibrationValidator.MinimumInlierRatio);
    }

    [Fact]
    public void CollinearFitAnchorsAreRejectedWithCollinearAnchors()
    {
        var document = SyntheticCalibrationFixtures.CreateDocument(fitCells: Enumerable.Range(0, 12));
        var result = SyntheticCalibrationFixtures.Calibrate(document);

        AssertProblem(result, CalibrationProblemCode.CollinearAnchors);
        Assert.Equal(CalibrationStatus.Degenerate, result.Status);
    }

    [Fact]
    public void FewerThanEightFitAnchorsAreRejected()
    {
        var document = SyntheticCalibrationFixtures.CreateDocument(fitCells: [0, 13, 153, 280, 293, 420, 433]);
        var result = SyntheticCalibrationFixtures.Calibrate(document);

        AssertProblem(result, CalibrationProblemCode.TooFewFitAnchors);
        Assert.Equal(CalibrationStatus.InsufficientAnchors, result.Status);
        Assert.Equal(7, result.Metrics!.FitAnchorCount);
    }

    [Fact]
    public void ConcentratedFitAnchorsAreRejectedWithConcentratedAnchors()
    {
        int[] concentratedCells = [170, 171, 184, 185, 198, 199, 212, 213, 226, 227, 240, 241];
        var result = SyntheticCalibrationFixtures.Calibrate(SyntheticCalibrationFixtures.CreateDocument(fitCells: concentratedCells));

        AssertProblem(result, CalibrationProblemCode.ConcentratedAnchors);
        Assert.Equal(CalibrationStatus.Degenerate, result.Status);
    }

    [Fact]
    public void FewerThanThreeIndependentValidationAnchorsAreRejected()
    {
        var document = SyntheticCalibrationFixtures.CreateDocument(validationCells: [167, 286]);
        var result = SyntheticCalibrationFixtures.Calibrate(document);

        AssertProblem(result, CalibrationProblemCode.TooFewValidationAnchors);
        Assert.Equal(CalibrationStatus.InsufficientAnchors, result.Status);
        Assert.Equal(2, result.Metrics!.ValidationAnchorCount);
    }

    [Fact]
    public void ValidationAnchorsDuplicatingFitCellsAreRejectedAsDependent()
    {
        var document = SyntheticCalibrationFixtures.CreateDocument(validationCells: [167, 286, 13]);
        var result = SyntheticCalibrationFixtures.Calibrate(document);

        AssertProblem(result, CalibrationProblemCode.DependentValidationAnchor);
        Assert.Equal(CalibrationStatus.MetricsFailed, result.Status);
    }

    [Fact]
    public void ValidationAnchorsDoNotParticipateInTheFit()
    {
        var mapper = SyntheticCalibrationFixtures.CreateMapper();
        var document = SyntheticCalibrationFixtures.CreateDocument(mapper: mapper, validationCells: [167, 286, 532]);
        var anchors = document.Anchors.ToBuilder();
        for (var index = 12; index < anchors.Count; index++)
        {
            var anchor = anchors[index];
            anchors[index] = new CalibrationAnchor(
                anchor.Cell,
                anchor.LogicalPoint,
                new WindowLocalPixels(anchor.MeasuredPixelPoint.X + 25, anchor.MeasuredPixelPoint.Y + 20),
                anchor.Role,
                anchor.Source);
        }

        var changedValidation = new CalibrationAnchorDocument(
            document.ClientBuild,
            document.MapId,
            document.ImageWidth,
            document.ImageHeight,
            anchors.ToImmutable(),
            document.Synthetic);
        var result = SyntheticCalibrationFixtures.Calibrate(changedValidation);

        AssertProblem(result, CalibrationProblemCode.ValidationMaxTooHigh);
        AssertProblem(result, CalibrationProblemCode.ValidationPointTooFar);
        Assert.NotNull(result.Metrics);
        Assert.True(result.Metrics.MaxValidationErrorPixels > 20);
        Assert.True(result.Metrics.ValidationErrorsPixels.Max() > CalibrationValidator.MaximumIndividualValidationErrorPixels);
        AssertClose(0, result.Metrics.MedianFitErrorPixels!.Value, 1e-4);
    }

    [Fact]
    public void SingularMapperCoefficientsAreRejected()
    {
        var created = AffineScreenMapper.TryCreate([15, 8, 70, 7.5, 4, 80], out _);

        Assert.False(created);

        using var singularMatrix = new OpenCvSharp.Mat(2, 3, OpenCvSharp.MatType.CV_64FC1);
        singularMatrix.Set(0, 0, 15.0);
        singularMatrix.Set(0, 1, 8.0);
        singularMatrix.Set(0, 2, 70.0);
        singularMatrix.Set(1, 0, 7.5);
        singularMatrix.Set(1, 1, 4.0);
        singularMatrix.Set(1, 2, 80.0);
        Assert.False(AffineScreenMapper.TryCreate(singularMatrix, out _));
    }

    [Fact]
    public void SingularPointGeometryIsRejectedWithCollinearAnchors()
    {
        var mapper = SyntheticCalibrationFixtures.CreateMapper();
        var result = SyntheticCalibrationFixtures.Calibrate(SyntheticCalibrationFixtures.CreateDocument(mapper: mapper, fitCells: Enumerable.Range(0, 12)));

        AssertProblem(result, CalibrationProblemCode.CollinearAnchors);
    }

    [Fact]
    public void ReprojectedPointsOutsideTheImageAreRejectedWithOutOfBounds()
    {
        Assert.True(AffineScreenMapper.TryCreate([30, 10, -200, 8, -8, 100], out var mapper));
        int[] fitCells = [13, 153, 280, 293, 420, 433, 547, 558, 559, 546, 286, 532];
        var anchors = ImmutableArray.CreateBuilder<CalibrationAnchor>();
        foreach (var cell in fitCells)
            anchors.Add(SyntheticCalibrationFixtures.CreateAnchor(cell, CalibrationAnchorRole.Fit, mapper!));

        anchors.Add(SyntheticCalibrationFixtures.CreateAnchor(0, CalibrationAnchorRole.Validation, mapper!, source: CalibrationAnchorSource.SyntheticFixture));
        anchors[^1] = new CalibrationAnchor(
            anchors[^1].Cell,
            anchors[^1].LogicalPoint,
            new WindowLocalPixels(100, 100),
            CalibrationAnchorRole.Validation,
            CalibrationAnchorSource.SyntheticFixture);
        anchors.Add(SyntheticCalibrationFixtures.CreateAnchor(167, CalibrationAnchorRole.Validation, mapper!));
        anchors.Add(SyntheticCalibrationFixtures.CreateAnchor(294, CalibrationAnchorRole.Validation, mapper!));
        var document = new CalibrationAnchorDocument(
            SyntheticCalibrationFixtures.ClientBuild,
            SyntheticCalibrationFixtures.MapId,
            SyntheticCalibrationFixtures.ImageWidth,
            SyntheticCalibrationFixtures.ImageHeight,
            anchors.ToImmutable(),
            true);
        var result = SyntheticCalibrationFixtures.Calibrate(document);

        AssertProblem(result, CalibrationProblemCode.OutOfBounds);
        Assert.Equal(CalibrationStatus.OutOfBounds, result.Status);
    }

    [Fact]
    public void NonFiniteScaleIsRejectedWithNonFiniteInput()
    {
        var result = SyntheticCalibrationFixtures.Calibrate(request: SyntheticCalibrationFixtures.CreateRequest(desktopScale: double.PositiveInfinity));

        AssertProblem(result, CalibrationProblemCode.NonFiniteInput);
        Assert.Equal(CalibrationStatus.InvalidInput, result.Status);
        Assert.NotNull(result.Metrics);
        Assert.Equal(12, result.Metrics.FitResidualsPixels.Length);
    }

    [Fact]
    public void NonFiniteMapperCoefficientsAreRejected()
    {
        Assert.False(AffineScreenMapper.TryCreate([double.NaN, 1, 1, 1, 1, 1], out _));
        Assert.False(AffineScreenMapper.TryCreate([1, 1, 1, 1, double.PositiveInfinity, 1], out _));
    }

    [Fact]
    public void AResolutionChangeInvalidatesTheProfile()
    {
        var profile = CalibratedProfile();
        var environment = SyntheticCalibrationFixtures.CreateEnvironment(profile) with { };
        var changed = new CalibrationEnvironment(
            environment.WindowIdentity,
            environment.CaptureWidth + 1,
            environment.CaptureHeight,
            environment.ClientAreaGeometry,
            environment.DesktopScale,
            environment.MapId,
            environment.CalibrationVersion);
        var result = CalibrationValidator.ValidateProfile(profile, changed, profile.CreatedAt, TimeSpan.FromSeconds(30));

        AssertProblem(result, CalibrationProblemCode.ResolutionChanged);
        Assert.Equal(CalibrationStatus.Invalidated, result.Status);
    }

    [Fact]
    public void AMapChangeInvalidatesTheProfile()
    {
        var profile = CalibratedProfile();
        var environment = SyntheticCalibrationFixtures.CreateEnvironment(profile);
        var changed = new CalibrationEnvironment(
            environment.WindowIdentity,
            environment.CaptureWidth,
            environment.CaptureHeight,
            environment.ClientAreaGeometry,
            environment.DesktopScale,
            environment.MapId + 1,
            environment.CalibrationVersion);
        var result = CalibrationValidator.ValidateProfile(profile, changed, profile.CreatedAt, TimeSpan.FromSeconds(30));

        AssertProblem(result, CalibrationProblemCode.MapChanged);
        Assert.Equal(CalibrationStatus.Invalidated, result.Status);
    }

    [Fact]
    public void AReplacedWindowInvalidatesTheProfile()
    {
        var profile = CalibratedProfile();
        var environment = SyntheticCalibrationFixtures.CreateEnvironment(profile);
        var changedIdentity = SyntheticCalibrationFixtures.CreateWindowIdentity(environment.WindowIdentity.Xid + 1, environment.WindowIdentity.ProcessId!.Value);
        var changed = new CalibrationEnvironment(
            changedIdentity,
            environment.CaptureWidth,
            environment.CaptureHeight,
            environment.ClientAreaGeometry,
            environment.DesktopScale,
            environment.MapId,
            environment.CalibrationVersion);
        var result = CalibrationValidator.ValidateProfile(profile, changed, profile.CreatedAt, TimeSpan.FromSeconds(30));

        AssertProblem(result, CalibrationProblemCode.WindowReplaced);
        Assert.Equal(CalibrationStatus.Invalidated, result.Status);
    }

    [Fact]
    public void AChangedWindowProcessIdInvalidatesTheProfile()
    {
        var profile = CalibratedProfile();
        var environment = SyntheticCalibrationFixtures.CreateEnvironment(profile);
        var changedIdentity = SyntheticCalibrationFixtures.CreateWindowIdentity(environment.WindowIdentity.Xid, environment.WindowIdentity.ProcessId!.Value + 1);
        var changed = new CalibrationEnvironment(
            changedIdentity,
            environment.CaptureWidth,
            environment.CaptureHeight,
            environment.ClientAreaGeometry,
            environment.DesktopScale,
            environment.MapId,
            environment.CalibrationVersion);
        var result = CalibrationValidator.ValidateProfile(profile, changed, profile.CreatedAt, TimeSpan.FromSeconds(30));

        AssertProblem(result, CalibrationProblemCode.WindowReplaced);
        Assert.Equal(CalibrationStatus.Invalidated, result.Status);
    }

    [Fact]
    public void AClientAreaMoveInvalidatesTheProfile()
    {
        var profile = CalibratedProfile();
        var environment = SyntheticCalibrationFixtures.CreateEnvironment(profile);
        var changed = new CalibrationEnvironment(
            environment.WindowIdentity,
            environment.CaptureWidth,
            environment.CaptureHeight,
            SyntheticCalibrationFixtures.CreateClientAreaGeometry(originX: 11),
            environment.DesktopScale,
            environment.MapId,
            environment.CalibrationVersion);
        var result = CalibrationValidator.ValidateProfile(profile, changed, profile.CreatedAt, TimeSpan.FromSeconds(30));

        AssertProblem(result, CalibrationProblemCode.ClientAreaChanged);
        Assert.Equal(CalibrationStatus.Invalidated, result.Status);
    }

    [Fact]
    public void ADesktopScaleChangeInvalidatesTheProfile()
    {
        var profile = CalibratedProfile();
        var environment = SyntheticCalibrationFixtures.CreateEnvironment(profile);
        var changed = new CalibrationEnvironment(
            environment.WindowIdentity,
            environment.CaptureWidth,
            environment.CaptureHeight,
            environment.ClientAreaGeometry,
            environment.DesktopScale * 1.25,
            environment.MapId,
            environment.CalibrationVersion);
        var result = CalibrationValidator.ValidateProfile(profile, changed, profile.CreatedAt, TimeSpan.FromSeconds(30));

        AssertProblem(result, CalibrationProblemCode.ScaleChanged);
        Assert.Equal(CalibrationStatus.Invalidated, result.Status);
    }

    [Fact]
    public void ACalibrationVersionChangeInvalidatesTheProfile()
    {
        var profile = CalibratedProfile();
        var environment = SyntheticCalibrationFixtures.CreateEnvironment(profile);
        var changed = new CalibrationEnvironment(
            environment.WindowIdentity,
            environment.CaptureWidth,
            environment.CaptureHeight,
            environment.ClientAreaGeometry,
            environment.DesktopScale,
            environment.MapId,
            environment.CalibrationVersion + 1);
        var result = CalibrationValidator.ValidateProfile(profile, changed, profile.CreatedAt, TimeSpan.FromSeconds(30));

        AssertProblem(result, CalibrationProblemCode.CalibrationVersionChanged);
        Assert.Equal(CalibrationStatus.Invalidated, result.Status);
    }

    [Fact]
    public void ClientAreaGeometryOutsideTheCaptureIsRejectedAsContradictory()
    {
        var calibrated = CalibratedProfile();
        var profile = new CalibrationProfile(
            calibrated.ClientBuild,
            calibrated.MapId,
            calibrated.WindowIdentity,
            calibrated.CaptureWidth,
            calibrated.CaptureHeight,
            SyntheticCalibrationFixtures.CreateClientAreaGeometry(originX: 20, width: 900),
            calibrated.DesktopScale,
            calibrated.CalibrationVersion,
            calibrated.Mapper,
            calibrated.Anchors,
            calibrated.ValidationErrorsPixels,
            calibrated.CreatedAt,
            calibrated.IsSynthetic);
        var environment = SyntheticCalibrationFixtures.CreateEnvironment(profile);
        var result = CalibrationValidator.ValidateProfile(profile, environment, profile.CreatedAt, TimeSpan.FromSeconds(30));

        AssertProblem(result, CalibrationProblemCode.SceneContradiction);
        Assert.Equal(CalibrationStatus.Invalidated, result.Status);
    }

    [Fact]
    public void AProfileOlderThanCalibrationMaxAgeSecondsIsStale()
    {
        var createdAt = new DateTimeOffset(2026, 11, 8, 14, 0, 0, TimeSpan.Zero);
        var provider = SyntheticCalibrationFixtures.CreateTimeProvider(createdAt);
        var resultAtCreation = SyntheticCalibrationFixtures.Calibrate(request: SyntheticCalibrationFixtures.CreateRequest(timeProvider: provider));
        Assert.NotNull(resultAtCreation.Profile);
        var profile = resultAtCreation.Profile;
        Assert.Equal(createdAt, profile.CreatedAt);
        var environment = SyntheticCalibrationFixtures.CreateEnvironment(profile);
        var now = profile.CreatedAt.AddSeconds(CalibrationValidator.CalibrationMaxAgeSeconds + 1);
        var staleResult = CalibrationValidator.ValidateProfile(profile, environment, now, TimeSpan.FromSeconds(CalibrationValidator.CalibrationMaxAgeSeconds));

        AssertProblem(staleResult, CalibrationProblemCode.Stale);
        Assert.Equal(CalibrationStatus.Stale, staleResult.Status);
    }

    [Fact]
    public void VersionOneJsonRoundTripsTheMissionFormatAndSyntheticLabel()
    {
        var document = SyntheticCalibrationFixtures.CreateDocument(synthetic: true);
        var json = document.ToJson();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var loaded = CalibrationAnchorDocument.Load(stream);
        var result = SyntheticCalibrationFixtures.Calibrate(loaded);

        Assert.Equal(1, loaded.FormatVersion);
        Assert.Contains("\"formatVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"cellId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"pixelX\"", json, StringComparison.Ordinal);
        Assert.Contains("\"pixelY\"", json, StringComparison.Ordinal);
        Assert.Contains("\"role\": \"fit\"", json, StringComparison.Ordinal);
        Assert.Contains("\"role\": \"validation\"", json, StringComparison.Ordinal);
        Assert.Equal(document.Anchors.Length, loaded.Anchors.Length);
        Assert.All(loaded.Anchors, anchor => Assert.Equal(CalibrationAnchorSource.ManualAnnotation, anchor.Source));
        Assert.True(result.IsSynthetic);
        Assert.True(result.Profile!.IsSynthetic);
    }

    [Fact]
    public void UnknownJsonVersionIsRejected()
    {
        const string json = "{\"formatVersion\":2,\"clientBuild\":\"synthetic\",\"mapId\":1,\"imageWidth\":800,\"imageHeight\":600,\"anchors\":[]}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        Assert.Throws<InvalidDataException>(() => CalibrationAnchorDocument.Load(stream));
    }

    [Fact]
    public void UnknownJsonAnchorRoleIsRejected()
    {
        const string json = "{\"formatVersion\":1,\"clientBuild\":\"synthetic\",\"mapId\":1,\"imageWidth\":800,\"imageHeight\":600,\"anchors\":[{\"cellId\":1,\"pixelX\":1,\"pixelY\":1,\"role\":\"guess\"}]}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        Assert.Throws<InvalidDataException>(() => CalibrationAnchorDocument.Load(stream));
    }

    [Fact]
    public void MissingOptionalSyntheticJsonFieldDefaultsToFalse()
    {
        const string json = "{\"formatVersion\":1,\"clientBuild\":\"synthetic\",\"mapId\":1,\"imageWidth\":800,\"imageHeight\":600,\"anchors\":[]}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var document = CalibrationAnchorDocument.Load(stream);

        Assert.False(document.Synthetic);
    }

    [Fact]
    public void CalibrationIdentityIsStableForTheSameProfileAndChangesWithItsWindow()
    {
        var createdAt = new DateTimeOffset(2026, 11, 8, 14, 0, 0, TimeSpan.Zero);
        var provider = SyntheticCalibrationFixtures.CreateTimeProvider(createdAt);
        var first = SyntheticCalibrationFixtures.Calibrate(request: SyntheticCalibrationFixtures.CreateRequest(timeProvider: provider)).Profile!;
        var second = SyntheticCalibrationFixtures.Calibrate(request: SyntheticCalibrationFixtures.CreateRequest(timeProvider: provider)).Profile!;
        var changedWindowRequest = SyntheticCalibrationFixtures.CreateRequest(
            timeProvider: provider,
            windowIdentity: SyntheticCalibrationFixtures.CreateWindowIdentity(xid: 452));
        var changedWindow = SyntheticCalibrationFixtures.Calibrate(request: changedWindowRequest).Profile!;

        Assert.Equal(first.CalibrationId, second.CalibrationId);
        Assert.NotEqual(first.CalibrationId, changedWindow.CalibrationId);
    }

    [Fact]
    public void JsonAnchorOutsideTheImageIsRejected()
    {
        const string json = "{\"formatVersion\":1,\"clientBuild\":\"synthetic\",\"mapId\":1,\"imageWidth\":800,\"imageHeight\":600,\"anchors\":[{\"cellId\":1,\"pixelX\":800,\"pixelY\":1,\"role\":\"fit\"}]}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        Assert.Throws<InvalidDataException>(() => CalibrationAnchorDocument.Load(stream));
    }

    [Fact]
    public void NonPositiveJsonDimensionsAreRejected()
    {
        const string json = "{\"formatVersion\":1,\"clientBuild\":\"synthetic\",\"mapId\":1,\"imageWidth\":0,\"imageHeight\":600,\"anchors\":[]}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        Assert.Throws<InvalidDataException>(() => CalibrationAnchorDocument.Load(stream));
    }

    [Fact]
    public void AnAnchorRejectsLogicalCoordinatesThatDisagreeWithMapGeometry()
    {
        var cell = new LogicalCell(10);

        Assert.Throws<ArgumentException>(() => new CalibrationAnchor(
            cell,
            new MapPoint(100, 100),
            new WindowLocalPixels(20, 20),
            CalibrationAnchorRole.Fit,
            CalibrationAnchorSource.SyntheticFixture));
    }

    private static CalibrationProfile CalibratedProfile()
    {
        var result = SyntheticCalibrationFixtures.Calibrate();
        Assert.NotNull(result.Profile);
        return result.Profile;
    }

    private static void AssertProblem(CalibrationResult result, CalibrationProblemCode expected)
    {
        Assert.Contains(result.Problems, problem => problem.Code == expected);
    }

    private static void AssertClose(double expected, double actual, double tolerance)
    {
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"Expected {expected} ± {tolerance}, received {actual}.");
    }
}
