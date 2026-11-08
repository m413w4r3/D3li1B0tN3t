using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using D3B.Gui.Calibration;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;

namespace D3B.Gui.Reporting;

/// <summary>A versioned, path-redacted report for one offline GUI preview.</summary>
public sealed record GuiPreviewReport
{
    /// <summary>The current serialized report format.</summary>
    public const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();
    private static readonly GuiPreviewReportJsonContext _jsonContext = new(_jsonOptions);

    /// <summary>Gets the serialized report format version.</summary>
    public int FormatVersion { get; }

    /// <summary>Gets the UTC time when the report was generated.</summary>
    public DateTimeOffset GeneratedAt { get; }

    /// <summary>Gets whether the preview is based on synthetic fixture data.</summary>
    public bool IsSynthetic { get; }

    /// <summary>Gets the explicit fixture label, when the preview is synthetic.</summary>
    public string? Label { get; }

    /// <summary>Gets the input image file name without its directory.</summary>
    public string? ImageFileName { get; }

    /// <summary>Gets the anchor file name without its directory.</summary>
    public string? AnchorsFileName { get; }

    /// <summary>Gets the requested map identifier.</summary>
    public long MapId { get; }

    /// <summary>Gets the requested interactive element identifier, when localization was requested.</summary>
    public int? ElementId { get; }

    /// <summary>Gets the input image width in pixels.</summary>
    public int ImageWidth { get; }

    /// <summary>Gets the input image height in pixels.</summary>
    public int ImageHeight { get; }

    /// <summary>Gets the calibration result status.</summary>
    public CalibrationStatus? CalibrationStatus { get; }

    /// <summary>Gets the stable identity of the accepted calibration profile.</summary>
    public string? CalibrationId { get; }

    /// <summary>Gets the median fitting residual in pixels.</summary>
    public double? MedianFitErrorPixels { get; }

    /// <summary>Gets the maximum fitting residual in pixels.</summary>
    public double? MaxFitErrorPixels { get; }

    /// <summary>Gets the maximum independent validation residual in pixels.</summary>
    public double? MaxValidationErrorPixels { get; }

    /// <summary>Gets the fraction of fit anchors accepted by RANSAC.</summary>
    public double? InlierRatio { get; }

    /// <summary>Gets the number of fitting anchors.</summary>
    public int? FitAnchorCount { get; }

    /// <summary>Gets the number of independent validation anchors.</summary>
    public int? ValidationAnchorCount { get; }

    /// <summary>Gets the calibration problems found during validation.</summary>
    public ImmutableArray<CalibrationProblem> Problems { get; }

    /// <summary>Gets the visual target status, when target localization was requested.</summary>
    public TargetStatus? TargetStatus { get; }

    /// <summary>Gets matching scores, thresholds, and evidence for the target candidate.</summary>
    public TargetConfidence? TargetConfidence { get; }

    /// <summary>Gets the calibrated target cell center, when one was projected.</summary>
    public WindowLocalPixels? ProjectedCellCenter { get; }

    /// <summary>Gets the candidate target bounding box, when a match was measured.</summary>
    public WindowLocalRect? VisualBoundingBox { get; }

    /// <summary>Gets the proposed click point, present only for a verified visual target.</summary>
    public WindowLocalPixels? ProposedClickPoint { get; }

    /// <summary>Gets the final preview verdict.</summary>
    public GuiPreviewVerdict Verdict { get; }

    /// <summary>Gets the limits that apply to this offline result.</summary>
    public ImmutableArray<string> Limitations { get; }

    private GuiPreviewReport(
        DateTimeOffset generatedAt,
        bool isSynthetic,
        string? imageFileName,
        string? anchorsFileName,
        long mapId,
        int? elementId,
        int imageWidth,
        int imageHeight,
        CalibrationResult calibration,
        TargetRegion? target,
        GuiPreviewVerdict verdict)
    {
        FormatVersion = CurrentFormatVersion;
        GeneratedAt = generatedAt.ToUniversalTime();
        IsSynthetic = isSynthetic;
        Label = isSynthetic ? "SYNTHETIC" : null;
        ImageFileName = FileNameOnly(imageFileName);
        AnchorsFileName = FileNameOnly(anchorsFileName);
        MapId = mapId;
        ElementId = elementId ?? target?.ElementId;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        CalibrationStatus = calibration.Status;
        CalibrationId = calibration.Profile?.CalibrationId;
        MedianFitErrorPixels = calibration.Metrics?.MedianFitErrorPixels;
        MaxFitErrorPixels = calibration.Metrics?.MaxFitErrorPixels;
        MaxValidationErrorPixels = calibration.Metrics?.MaxValidationErrorPixels;
        InlierRatio = calibration.Metrics?.InlierRatio;
        FitAnchorCount = calibration.Metrics?.FitAnchorCount;
        ValidationAnchorCount = calibration.Metrics?.ValidationAnchorCount;
        Problems = calibration.Problems.IsDefault ? ImmutableArray<CalibrationProblem>.Empty : calibration.Problems;
        TargetStatus = target?.Status;
        TargetConfidence = target?.Confidence;
        ProjectedCellCenter = target?.ProjectedCellCenter;
        VisualBoundingBox = target?.VisualBoundingBox;
        ProposedClickPoint = target is { Status: Vision.TargetStatus.Verified } verifiedTarget
            ? verifiedTarget.ProposedClickPoint
            : null;
        Verdict = verdict;

        var limitations = ImmutableArray.CreateBuilder<string>();
        if (isSynthetic)
        {
            limitations.Add("SYNTHETIC: this preview uses synthetic fixture data.");
        }

        limitations.Add("A synthetic calibration is not validation on Unity.");
        limitations.Add("No real resource detection is performed; localization uses only the supplied local template.");
        limitations.Add("No capture, input, click, or network request is performed.");
        Limitations = limitations.ToImmutable();
    }

    /// <summary>Creates a report from a completed calibration and optional target localization.</summary>
    /// <param name="mapId">The map identifier requested by the caller.</param>
    /// <param name="imageFileName">The image path; only its file name is retained.</param>
    /// <param name="anchorsFileName">The anchor path; only its file name is retained.</param>
    /// <param name="imageWidth">The decoded input image width.</param>
    /// <param name="imageHeight">The decoded input image height.</param>
    /// <param name="anchorDocument">The loaded anchor document.</param>
    /// <param name="calibration">The quantitative calibration outcome.</param>
    /// <param name="target">The optional target localization outcome.</param>
    /// <param name="elementId">The requested target element identifier, including when calibration fails.</param>
    /// <param name="generatedAt">The report time, defaulting to the current UTC time.</param>
    public static GuiPreviewReport Create(
        long mapId,
        string? imageFileName,
        string? anchorsFileName,
        int imageWidth,
        int imageHeight,
        CalibrationAnchorDocument anchorDocument,
        CalibrationResult calibration,
        TargetRegion? target = null,
        int? elementId = null,
        DateTimeOffset? generatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(anchorDocument);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);

        var acceptedTarget = calibration.IsCalibrated ? target : null;
        var isSynthetic = anchorDocument.Synthetic || calibration.IsSynthetic
            || anchorDocument.Anchors.Any(anchor => anchor.Source == CalibrationAnchorSource.SyntheticFixture);
        var targetRequested = elementId.HasValue || target is not null;
        var verdict = calibration.IsCalibrated
            ? !targetRequested
                ? GuiPreviewVerdict.Calibrated
                : acceptedTarget?.Status == Vision.TargetStatus.Verified
                    ? GuiPreviewVerdict.TargetVerified
                    : GuiPreviewVerdict.TargetUnverified
            : GuiPreviewVerdict.NotCalibrated;

        return new GuiPreviewReport(
            generatedAt ?? DateTimeOffset.UtcNow,
            isSynthetic,
            imageFileName,
            anchorsFileName,
            mapId,
            elementId ?? acceptedTarget?.ElementId,
            imageWidth,
            imageHeight,
            calibration,
            acceptedTarget,
            verdict);
    }

    private static string? FileNameOnly(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.GetFileName(path);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter<GuiPreviewVerdict>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<CalibrationStatus>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<CalibrationProblemCode>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<TargetStatus>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<TargetEvidence>(JsonNamingPolicy.CamelCase));
        return options;
    }

    /// <summary>Serializes this report as indented camel-case JSON with string enum values.</summary>
    /// <returns>The report JSON.</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, _jsonContext.GuiPreviewReport);
    }
}
