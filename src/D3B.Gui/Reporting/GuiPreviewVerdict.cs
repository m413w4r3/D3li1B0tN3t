namespace D3B.Gui.Reporting;

/// <summary>Describes whether an offline preview produced a usable calibration or target result.</summary>
public enum GuiPreviewVerdict
{
    /// <summary>The calibration passed its geometric and quantitative checks.</summary>
    Calibrated,

    /// <summary>The calibration did not pass its checks.</summary>
    NotCalibrated,

    /// <summary>The requested target passed visual localization checks.</summary>
    TargetVerified,

    /// <summary>The requested target did not pass visual localization checks.</summary>
    TargetUnverified,

    /// <summary>The preview could not be completed because its input or output failed.</summary>
    Error
}
