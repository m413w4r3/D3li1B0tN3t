namespace D3B.Gui.Calibration;

/// <summary>Describes the outcome of calibration or profile validation.</summary>
public enum CalibrationStatus
{
    /// <summary>The profile passed all fit, validation, and freshness checks.</summary>
    Calibrated,

    /// <summary>No usable calibration profile was produced.</summary>
    NotCalibrated,

    /// <summary>The document or request contains invalid input.</summary>
    InvalidInput,

    /// <summary>The required independent anchor counts were not met.</summary>
    InsufficientAnchors,

    /// <summary>The anchor geometry or affine matrix is degenerate.</summary>
    Degenerate,

    /// <summary>At least one annotation or projected point lies outside the image.</summary>
    OutOfBounds,

    /// <summary>The fit or validation metrics exceed a required threshold.</summary>
    MetricsFailed,

    /// <summary>The otherwise matching profile has expired.</summary>
    Stale,

    /// <summary>The window or environment no longer matches the profile.</summary>
    Invalidated
}
