namespace D3B.Gui.Calibration;

/// <summary>Describes whether an anchor participates in fitting or independent validation.</summary>
public enum CalibrationAnchorRole
{
    /// <summary>The anchor may contribute to the affine fit.</summary>
    Fit,

    /// <summary>The anchor measures fit quality and is excluded from fitting.</summary>
    Validation
}
