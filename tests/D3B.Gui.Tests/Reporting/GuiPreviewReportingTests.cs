using System.Text.Json;
using D3B.Gui.Calibration;
using D3B.Gui.Reporting;
using D3B.Gui.Vision;
using OpenCvSharp;

namespace D3B.Gui.Tests.Reporting;

[Trait("Fixture", "SYNTHETIC")]
public sealed class GuiPreviewReportingTests
{
    private static readonly string[] _expectedOutputFileNames = ["preview.png", "report.json"];

    [Fact]
    public void ExactSyntheticCalibrationSerializesMetricsAndRedactsPaths()
    {
        var document = SyntheticPreviewFixtures.CreateDocument();
        var calibration = SyntheticPreviewFixtures.Calibrate(document);
        var report = GuiPreviewReport.Create(
            SyntheticPreviewFixtures.MapId,
            "/private/session/image.png",
            "/private/session/anchors.json",
            SyntheticPreviewFixtures.ImageWidth,
            SyntheticPreviewFixtures.ImageHeight,
            document,
            calibration,
            generatedAt: DateTimeOffset.UnixEpoch);

        using var json = JsonDocument.Parse(report.ToJson());
        var root = json.RootElement;

        Assert.Equal(CalibrationStatus.Calibrated, calibration.Status);
        Assert.Equal(GuiPreviewVerdict.Calibrated, report.Verdict);
        Assert.True(report.IsSynthetic);
        Assert.Equal("SYNTHETIC", report.Label);
        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        Assert.Equal("calibrated", root.GetProperty("verdict").GetString());
        Assert.Equal("SYNTHETIC", root.GetProperty("label").GetString());
        Assert.Equal("image.png", root.GetProperty("imageFileName").GetString());
        Assert.Equal("anchors.json", root.GetProperty("anchorsFileName").GetString());
        Assert.Equal(SyntheticPreviewFixtures.ImageWidth, root.GetProperty("imageWidth").GetInt32());
        Assert.Equal(SyntheticPreviewFixtures.ImageHeight, root.GetProperty("imageHeight").GetInt32());
        Assert.Equal(12, root.GetProperty("fitAnchorCount").GetInt32());
        Assert.Equal(3, root.GetProperty("validationAnchorCount").GetInt32());
        Assert.Equal(1.0, root.GetProperty("inlierRatio").GetDouble(), 6);
        Assert.True(root.GetProperty("medianFitErrorPixels").GetDouble() < 0.001);
        Assert.True(root.GetProperty("maxFitErrorPixels").GetDouble() < 0.001);
        Assert.True(root.GetProperty("maxValidationErrorPixels").GetDouble() < 0.001);
        Assert.Contains("A synthetic calibration is not validation on Unity.", report.Limitations);
        Assert.Contains("No real resource detection is performed; localization uses only the supplied local template.", report.Limitations);
        Assert.Contains("No capture, input, click, or network request is performed.", report.Limitations);
        Assert.DoesNotContain("/private/session", report.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void NoisyOutlierCalibrationReportsMetricsAndProblems()
    {
        var document = SyntheticPreviewFixtures.CreateDocument(noisy: true, outlierCount: 4);
        var calibration = SyntheticPreviewFixtures.Calibrate(document);
        var report = GuiPreviewReport.Create(
            SyntheticPreviewFixtures.MapId,
            "image.png",
            "anchors.json",
            SyntheticPreviewFixtures.ImageWidth,
            SyntheticPreviewFixtures.ImageHeight,
            document,
            calibration);

        Assert.Equal(GuiPreviewVerdict.NotCalibrated, report.Verdict);
        Assert.NotNull(report.InlierRatio);
        Assert.True(report.InlierRatio < 0.80);
        Assert.NotNull(report.MaxFitErrorPixels);
        Assert.NotNull(report.MaxValidationErrorPixels);
        Assert.Contains(calibration.Problems, problem => problem.Code == CalibrationProblemCode.InlierRatioTooLow);
        Assert.Contains(report.Problems, problem => problem.Code == CalibrationProblemCode.InlierRatioTooLow);
    }

    [Fact]
    public void InvalidCalibrationDoesNotClaimTargetVerification()
    {
        var document = SyntheticPreviewFixtures.CreateDocument(fitAnchorCount: 7);
        var calibration = SyntheticPreviewFixtures.Calibrate(document);
        var report = GuiPreviewReport.Create(
            SyntheticPreviewFixtures.MapId,
            "image.png",
            "anchors.json",
            SyntheticPreviewFixtures.ImageWidth,
            SyntheticPreviewFixtures.ImageHeight,
            document,
            calibration,
            elementId: SyntheticPreviewFixtures.ElementId);

        Assert.Equal(GuiPreviewVerdict.NotCalibrated, report.Verdict);
        Assert.Equal(SyntheticPreviewFixtures.ElementId, report.ElementId);
        Assert.Null(report.TargetStatus);
        Assert.Null(report.TargetConfidence);
        Assert.Null(report.ProposedClickPoint);
    }

    [Fact]
    public void VerifiedSyntheticTargetIncludesBoundsAndProposedPoint()
    {
        var document = SyntheticPreviewFixtures.CreateDocument();
        var calibration = SyntheticPreviewFixtures.Calibrate(document);
        using var template = SyntheticPreviewFixtures.CreateTemplate();
        var bounds = SyntheticPreviewFixtures.TargetBounds(calibration.Profile!);
        using var image = SyntheticPreviewFixtures.CreateImage(template, bounds);
        using var frame = SyntheticPreviewFixtures.CreateFrame(image, calibration.Profile!);
        var target = new TemplateTargetLocator().Locate(
            frame,
            calibration.Profile!,
            SyntheticPreviewFixtures.CreateExpectation(template));
        var report = GuiPreviewReport.Create(
            SyntheticPreviewFixtures.MapId,
            "image.png",
            "anchors.json",
            SyntheticPreviewFixtures.ImageWidth,
            SyntheticPreviewFixtures.ImageHeight,
            document,
            calibration,
            target,
            SyntheticPreviewFixtures.ElementId);

        Assert.Equal(TargetStatus.Verified, target.Status);
        Assert.Equal(GuiPreviewVerdict.TargetVerified, report.Verdict);
        Assert.Equal(bounds, report.VisualBoundingBox);
        Assert.Equal(target.ProposedClickPoint, report.ProposedClickPoint);
        Assert.NotNull(report.TargetConfidence);
        Assert.True(report.TargetConfidence.Score >= TemplateTargetLocator.MinimumScore);
        using var json = JsonDocument.Parse(report.ToJson());
        Assert.Equal("verified", json.RootElement.GetProperty("targetStatus").GetString());
        Assert.Equal(bounds.X, json.RootElement.GetProperty("visualBoundingBox").GetProperty("x").GetInt32());
        Assert.Equal(target.ProposedClickPoint!.Value.X, json.RootElement.GetProperty("proposedClickPoint").GetProperty("x").GetInt32());
    }

    [Fact]
    public void MissingSyntheticTargetReturnsUnverifiedWithoutClickPoint()
    {
        var document = SyntheticPreviewFixtures.CreateDocument();
        var calibration = SyntheticPreviewFixtures.Calibrate(document);
        using var template = SyntheticPreviewFixtures.CreateTemplate();
        using var image = SyntheticPreviewFixtures.CreateImage();
        using var frame = SyntheticPreviewFixtures.CreateFrame(image, calibration.Profile!);
        var target = new TemplateTargetLocator().Locate(
            frame,
            calibration.Profile!,
            SyntheticPreviewFixtures.CreateExpectation(template));
        var report = GuiPreviewReport.Create(
            SyntheticPreviewFixtures.MapId,
            "image.png",
            "anchors.json",
            SyntheticPreviewFixtures.ImageWidth,
            SyntheticPreviewFixtures.ImageHeight,
            document,
            calibration,
            target,
            SyntheticPreviewFixtures.ElementId);

        Assert.Equal(TargetStatus.Unverified, target.Status);
        Assert.Equal(GuiPreviewVerdict.TargetUnverified, report.Verdict);
        Assert.Null(report.ProposedClickPoint);
    }

    [Fact]
    public void OverlayKeepsDimensionsModifiesPixelsAndMarksSyntheticFixture()
    {
        var document = SyntheticPreviewFixtures.CreateDocument();
        var calibration = SyntheticPreviewFixtures.Calibrate(document);
        using var image = SyntheticPreviewFixtures.CreateImage();
        using var overlay = PreviewOverlayRenderer.Render(image, document, calibration);

        Assert.Equal(image.Cols, overlay.Cols);
        Assert.Equal(image.Rows, overlay.Rows);
        Assert.True(HasPixelDifference(image, overlay, 8, 8, 135, 25));
        Assert.True(HasPixelDifference(image, overlay, 0, 0, image.Cols, image.Rows));
    }

    [Fact]
    public void OutputGuardAcceptsHomeRejectsRepositoryAndRelativePaths()
    {
        var temporaryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "p2n-output-guard"));
        var home = Path.Combine(temporaryRoot, "home");
        var repository = Path.Combine(temporaryRoot, "repository");

        Assert.True(PreviewOutputGuard.TryAuthorize(
            Path.Combine(home, ".local", "share", "D3B", "preview"),
            home,
            repository,
            false,
            out var accepted,
            out _));
        Assert.NotNull(accepted);
        Assert.False(PreviewOutputGuard.TryAuthorize(
            Path.Combine(repository, "artifacts"),
            home,
            repository,
            true,
            out _,
            out _));
        Assert.False(PreviewOutputGuard.TryAuthorize(home, home, repository, true, out _, out _));
        Assert.False(PreviewOutputGuard.TryAuthorize(Path.GetTempPath(), home, repository, true, out _, out _));
        Assert.False(PreviewOutputGuard.TryAuthorize("relative-output", home, repository, true, out _, out _));
        Assert.True(PreviewOutputGuard.TryAuthorize(
            Path.Combine(temporaryRoot, "explicit-output"),
            home,
            repository,
            true,
            out _,
            out _));
        Assert.False(PreviewOutputGuard.TryAuthorize(
            Path.Combine(temporaryRoot, "implicit-output"),
            home,
            repository,
            false,
            out _,
            out _));
    }

    [Fact]
    public void OutputWriterUsesPrivatePermissionsAndAtomicallyReplacesWithoutTemporaryFiles()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), $"p2n-preview-{Guid.NewGuid():N}");
        try
        {
            PreviewOutputWriter.WriteText(directory, "report.json", "first");
            var destination = PreviewOutputWriter.WriteText(directory, "report.json", "second");
            var imageBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            var imagePath = PreviewOutputWriter.WriteBytes(directory, "preview.png", imageBytes);

            Assert.Equal("second", File.ReadAllText(destination));
            Assert.Equal(imageBytes, File.ReadAllBytes(imagePath));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(directory));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(destination));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(imagePath));
            Assert.Equal(_expectedOutputFileNames, Directory.GetFiles(directory).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static bool HasPixelDifference(Mat original, Mat overlay, int left, int top, int right, int bottom)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                if (!original.At<Vec3b>(y, x).Equals(overlay.At<Vec3b>(y, x)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
