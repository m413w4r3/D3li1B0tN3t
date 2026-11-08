namespace D3B.Gui.Calibration;

/// <summary>Stable machine-readable causes for calibration rejection or invalidation.</summary>
public enum CalibrationProblemCode
{
    /// <summary>Fewer than eight fitting anchors were supplied.</summary>
    TooFewFitAnchors,

    /// <summary>Fewer than three independent validation anchors were supplied.</summary>
    TooFewValidationAnchors,

    /// <summary>A validation anchor duplicates a fitting or another validation cell.</summary>
    DependentValidationAnchor,

    /// <summary>Fitting anchors do not span a two-dimensional plane.</summary>
    CollinearAnchors,

    /// <summary>Fitting anchors cover too little of the captured image.</summary>
    ConcentratedAnchors,

    /// <summary>An input or computed coordinate contains NaN or infinity.</summary>
    NonFiniteInput,

    /// <summary>The fitted affine matrix is singular or numerically unusable.</summary>
    SingularMatrix,

    /// <summary>An annotation or projected point is outside the captured image.</summary>
    OutOfBounds,

    /// <summary>The median fitting residual exceeds its threshold.</summary>
    MedianErrorTooHigh,

    /// <summary>The maximum independent validation residual exceeds its threshold.</summary>
    ValidationMaxTooHigh,

    /// <summary>An independent validation residual exceeds the hard per-point limit.</summary>
    ValidationPointTooFar,

    /// <summary>Too few fitting anchors were accepted by RANSAC.</summary>
    InlierRatioTooLow,

    /// <summary>The verified window identifier or process identifier changed.</summary>
    WindowReplaced,

    /// <summary>The captured image dimensions changed.</summary>
    ResolutionChanged,

    /// <summary>The measured desktop scale changed.</summary>
    ScaleChanged,

    /// <summary>The measured client-area position or dimensions changed.</summary>
    ClientAreaChanged,

    /// <summary>The active map changed.</summary>
    MapChanged,

    /// <summary>The calibration contract version changed.</summary>
    CalibrationVersionChanged,

    /// <summary>The profile exceeded its permitted age.</summary>
    Stale,

    /// <summary>The measured scene or stored profile geometry is contradictory.</summary>
    SceneContradiction,

    /// <summary>The client build does not match the annotation document or profile.</summary>
    ClientBuildChanged,

    /// <summary>A request or age bound is invalid.</summary>
    InvalidInput
}
