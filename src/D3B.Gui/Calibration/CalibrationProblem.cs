namespace D3B.Gui.Calibration;

/// <summary>A single calibration failure or invalidation detail.</summary>
public sealed record CalibrationProblem
{
    /// <summary>Gets the stable problem code.</summary>
    public CalibrationProblemCode Code { get; }

    /// <summary>Gets a human-readable explanation.</summary>
    public string Detail { get; }

    /// <summary>Gets the associated residual, when one exists.</summary>
    public double? ErrorPixels { get; }

    /// <summary>Creates a problem description.</summary>
    public CalibrationProblem(CalibrationProblemCode code, string detail, double? errorPixels = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        Code = code;
        Detail = detail;
        ErrorPixels = errorPixels;
    }
}
