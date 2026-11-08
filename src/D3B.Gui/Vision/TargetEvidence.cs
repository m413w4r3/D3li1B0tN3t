namespace D3B.Gui.Vision;

/// <summary>A named check or failure recorded during target localization.</summary>
public enum TargetEvidence
{
    /// <summary>The expected cell was projected using the supplied calibration.</summary>
    ProjectedByCalibration,

    /// <summary>Template matching returned a finite candidate score.</summary>
    TemplateMatched,

    /// <summary>The best candidate is separated from an independent peak by the required margin.</summary>
    UniquePeak,

    /// <summary>The candidate bounds and proposed point satisfy the frame and edge-margin checks.</summary>
    InsideFrame,

    /// <summary>The candidate bounds and proposed point do not intersect any configured exclusion.</summary>
    ClearOfExclusions,

    /// <summary>The candidate center is within the configured distance of the expected visual center.</summary>
    NearExpectedCell,

    /// <summary>The template is empty or has no spatial variance.</summary>
    TemplateDegenerate,

    /// <summary>The projected cell, search region, or safe point lies outside the captured image.</summary>
    RegionOutOfFrame,

    /// <summary>The measured offset could not be represented as a window-local pixel point.</summary>
    InvalidOffset,

    /// <summary>The target-specific proposed click point lies outside the captured image.</summary>
    ClickPointOutOfFrame,

    /// <summary>The candidate center does not retain the configured distance from the frame edge.</summary>
    InsufficientEdgeMargin,

    /// <summary>The capture did not produce a usable image.</summary>
    FrameUnusable,

    /// <summary>The frame and calibration describe different windows, dimensions, or maps.</summary>
    CalibrationMismatch,

    /// <summary>No finite matching candidate could be measured.</summary>
    NoMatch,

    /// <summary>The best candidate score is below the required score.</summary>
    ScoreTooLow,

    /// <summary>Another independent candidate is too close in score or another known element overlaps.</summary>
    AmbiguousMatch,

    /// <summary>The candidate intersects a configured UI or occlusion exclusion.</summary>
    BlockedByExclusion,

    /// <summary>A configured exclusion does not fit the captured frame.</summary>
    InvalidExclusion,

    /// <summary>The candidate is farther from the expected visual center than the configured bound.</summary>
    TooFarFromExpectedCell,

    /// <summary>The template type is incompatible with the captured image type.</summary>
    TemplateIncompatible,

    /// <summary>The template is larger than the bounded search region.</summary>
    TemplateTooLarge
}
