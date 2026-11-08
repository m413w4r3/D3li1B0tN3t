using D3B.Gui.Calibration;
using D3B.Gui.Windowing;

namespace D3B.Gui.Vision;

/// <summary>A candidate target region tied to one captured frame and exact calibration profile.</summary>
/// <remarks>
/// Every pixel coordinate is relative to the <see cref="CaptureId"/> frame and <see cref="CalibrationId"/> profile.
/// An unverified result never proposes a fallback click at the projected cell center.
/// </remarks>
public sealed record TargetRegion
{
    /// <summary>Gets the map identifier.</summary>
    public long MapId { get; }

    /// <summary>Gets the interactive element identifier.</summary>
    public int ElementId { get; }

    /// <summary>Gets the logical target cell.</summary>
    public LogicalCell Cell { get; }

    /// <summary>Gets the graphics identifier.</summary>
    public int GfxId { get; }

    /// <summary>Gets the expected skill identifier, when supplied.</summary>
    public int? ExpectedSkillId { get; }

    /// <summary>Gets the calibrated cell center in captured-window pixels.</summary>
    public WindowLocalPixels ProjectedCellCenter { get; }

    /// <summary>Gets the matched candidate bounds, when a finite match was measured.</summary>
    public WindowLocalRect? VisualBoundingBox { get; }

    /// <summary>Gets the candidate center only after every verification check passes.</summary>
    public WindowLocalPixels? ProposedClickPoint { get; }

    /// <summary>Gets scores, thresholds, and accumulated evidence.</summary>
    public TargetConfidence Confidence { get; }

    /// <summary>Gets the localization status.</summary>
    public TargetStatus Status { get; }

    /// <summary>Gets the primary reason for an unverified or ambiguous result.</summary>
    public TargetEvidence? Reason { get; }

    /// <summary>Gets the exact capture identifier used for localization.</summary>
    public Guid CaptureId { get; }

    /// <summary>Gets the exact calibration identifier used for localization.</summary>
    public string CalibrationId { get; }

    /// <summary>Creates a candidate result tied to one frame and calibration.</summary>
    public TargetRegion(
        long mapId,
        int elementId,
        LogicalCell cell,
        int gfxId,
        int? expectedSkillId,
        WindowLocalPixels projectedCellCenter,
        WindowLocalRect? visualBoundingBox,
        WindowLocalPixels? proposedClickPoint,
        TargetConfidence confidence,
        TargetStatus status,
        TargetEvidence? reason,
        Guid captureId,
        string calibrationId)
    {
        ArgumentNullException.ThrowIfNull(confidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(calibrationId);

        MapId = mapId;
        ElementId = elementId;
        Cell = cell;
        GfxId = gfxId;
        ExpectedSkillId = expectedSkillId;
        ProjectedCellCenter = projectedCellCenter;
        VisualBoundingBox = visualBoundingBox;
        ProposedClickPoint = proposedClickPoint;
        Confidence = confidence;
        Status = status;
        Reason = reason;
        CaptureId = captureId;
        CalibrationId = calibrationId;
    }
}
