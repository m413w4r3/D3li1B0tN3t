using System.CommandLine;
using System.Globalization;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Reporting;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Scripts.GuiPreview;

/// <summary>Runs the offline image, anchor, calibration, and optional template preview.</summary>
internal static class GuiPreviewCommand
{
    public static RootCommand Create()
    {
        var image = new Option<FileInfo>("--image")
        {
            Required = true,
            Description = "A local PNG image to annotate.",
        };
        var anchors = new Option<FileInfo>("--anchors")
        {
            Required = true,
            Description = "A version 1 calibration anchor JSON document.",
        };
        var map = new Option<long>("--map")
        {
            Required = true,
            Description = "The expected map identifier.",
        };
        var element = new Option<int?>("--element")
        {
            Description = "Optionally localize a target element.",
        };
        var template = new Option<FileInfo?>("--template")
        {
            Description = "A local PNG template required with --element.",
        };
        var cell = new Option<int?>("--cell")
        {
            Description = "The target cell required with --element.",
        };
        var gfx = new Option<int?>("--gfx")
        {
            Description = "Optional graphics identifier for the target report.",
        };
        var output = new Option<string?>("--out")
        {
            Description = "An explicit absolute output directory outside the repository.",
        };

        var command = new RootCommand("Creates an offline calibrated GUI preview and JSON report. No capture or input is performed.")
        {
            image,
            anchors,
            map,
            element,
            template,
            cell,
            gfx,
            output,
        };

        command.SetAction(result => Run(
            result.GetValue(image)!,
            result.GetValue(anchors)!,
            result.GetValue(map),
            result.GetValue(element),
            result.GetValue(template),
            result.GetValue(cell),
            result.GetValue(gfx),
            result.GetValue(output)));

        return command;
    }

    private static int Run(
        FileInfo imageFile,
        FileInfo anchorsFile,
        long mapId,
        int? elementId,
        FileInfo? templateFile,
        int? cellId,
        int? gfxId,
        string? outputDirectory)
    {
        if (elementId.HasValue && (templateFile is null || !cellId.HasValue))
        {
            return Fail("--element requires both --template and --cell.");
        }

        if (!elementId.HasValue && (templateFile is not null || cellId.HasValue || gfxId.HasValue))
        {
            return Fail("--template, --cell, and --gfx require --element.");
        }

        try
        {
            var repositoryRoot = FindRepositoryRoot(Environment.CurrentDirectory);
            var homeDirectory = GetHomeDirectory();
            var isExplicitOutput = outputDirectory is not null;
            var selectedOutput = outputDirectory ?? GetDefaultOutputDirectory();

            if (!PreviewOutputGuard.TryAuthorize(
                selectedOutput,
                homeDirectory,
                repositoryRoot,
                isExplicitOutput,
                out var normalizedOutput,
                out var rejectionReason))
            {
                return Fail(rejectionReason ?? "The output directory is not allowed.");
            }

            using var inputImage = Cv2.ImRead(imageFile.FullName, ImreadModes.Color);
            if (inputImage.Empty())
            {
                return Fail($"The image could not be decoded: {imageFile.Name}.");
            }

            var anchorDocument = CalibrationAnchorDocument.Load(anchorsFile.FullName);
            var now = DateTimeOffset.UtcNow;
            var identity = new GameWindowIdentity(0, null, null, null, GameWindowIdentityStatus.NotFound);
            var calibrationRequest = new CalibrationRequest(
                mapId,
                anchorDocument.ClientBuild,
                inputImage.Cols,
                inputImage.Rows,
                1.0,
                new ClientAreaGeometry(new DesktopPixels(0, 0), new ClientAreaPixels(0, 0), inputImage.Cols, inputImage.Rows),
                identity,
                timeProvider: TimeProvider.System,
                isSynthetic: anchorDocument.Synthetic);
            var calibration = CalibrationValidator.Validate(anchorDocument, calibrationRequest);
            TargetRegion? target = null;

            if (elementId.HasValue && calibration.IsCalibrated)
            {
                using var templateImage = Cv2.ImRead(templateFile!.FullName, ImreadModes.Color);
                if (templateImage.Empty())
                {
                    return Fail($"The template could not be decoded: {templateFile.Name}.");
                }

                var snapshot = new GameWindowSnapshot(
                    identity,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    true,
                    true,
                    true,
                    new DesktopPixels(0, 0),
                    inputImage.Cols,
                    inputImage.Rows,
                    0,
                    inputImage.Cols,
                    inputImage.Rows,
                    null,
                    now);
                using var frame = new CapturedFrame(
                    Guid.NewGuid(),
                    identity,
                    now,
                    now,
                    snapshot,
                    snapshot,
                    inputImage.Cols,
                    inputImage.Rows,
                    inputImage.Clone(),
                    CaptureStatus.Captured,
                    null,
                    true);
                var expectation = new TargetExpectation(
                    mapId,
                    elementId.Value,
                    new LogicalCell(cellId!.Value),
                    gfxId ?? 0,
                    null,
                    templateImage);
                target = new TemplateTargetLocator().Locate(frame, calibration.Profile!, expectation);
            }

            using var annotated = PreviewOverlayRenderer.Render(inputImage, anchorDocument, calibration, target);
            if (!Cv2.ImEncode(".png", annotated, out var pngBytes))
            {
                return Fail("OpenCV could not encode the annotated preview as PNG.");
            }

            var report = GuiPreviewReport.Create(
                mapId,
                imageFile.Name,
                anchorsFile.Name,
                inputImage.Cols,
                inputImage.Rows,
                anchorDocument,
                calibration,
                target,
                elementId,
                now);
            var outputPath = normalizedOutput!;
            PreviewOutputWriter.WriteBytes(outputPath, "preview.png", pngBytes);
            PreviewOutputWriter.WriteText(outputPath, "report.json", report.ToJson());

            Console.Out.WriteLine($"Verdict: {report.Verdict}");
            Console.Out.WriteLine("Image: preview.png");
            Console.Out.WriteLine("Report: report.json");
            Console.Out.WriteLine($"Output directory: {outputPath}");

            return report.Verdict is GuiPreviewVerdict.Calibrated or GuiPreviewVerdict.TargetVerified ? 0 : 2;
        }
        catch (Exception exception)
        {
            return Fail(exception.Message);
        }
    }

    private static string FindRepositoryRoot(string startingDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startingDirectory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "D3B.slnx"))
                || Directory.Exists(Path.Combine(current.FullName, ".git"))
                || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the current directory.");
    }

    private static string GetHomeDirectory()
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (string.IsNullOrWhiteSpace(home) || !Path.IsPathFullyQualified(home))
        {
            throw new InvalidOperationException("The user's absolute home directory could not be determined.");
        }

        return Path.GetFullPath(home);
    }

    private static string GetDefaultOutputDirectory()
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmssfffffff'Z'", CultureInfo.InvariantCulture);
        var homeDirectory = GetHomeDirectory();
        return Path.Combine(homeDirectory, ".local", "share", "D3B", "p2n-preview", timestamp);
    }

    private static int Fail(string message)
    {
        Console.Out.WriteLine("Verdict: Error");
        Console.Error.WriteLine($"Error: {message}");
        return 1;
    }
}
