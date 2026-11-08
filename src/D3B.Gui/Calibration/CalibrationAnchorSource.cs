namespace D3B.Gui.Calibration;

/// <summary>Identifies how an anchor was obtained.</summary>
public enum CalibrationAnchorSource
{
    /// <summary>The screen point was annotated by a person; it is not an automatically verified observation.</summary>
    ManualAnnotation,

    /// <summary>The anchor belongs to a deterministic synthetic fixture.</summary>
    SyntheticFixture
}
