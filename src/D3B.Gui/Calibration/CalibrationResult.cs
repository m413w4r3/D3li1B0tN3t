using System.Collections.Immutable;

namespace D3B.Gui.Calibration;

/// <summary>A calibration outcome, quantitative metrics, and any rejection causes.</summary>
public sealed record CalibrationResult
{
    /// <summary>Gets the result status.</summary>
    public CalibrationStatus Status { get; }

    /// <summary>Gets the usable profile, when every calibration check passed.</summary>
    public CalibrationProfile? Profile { get; }

    /// <summary>Gets the metrics available for this attempt.</summary>
    public CalibrationMetrics? Metrics { get; }

    /// <summary>Gets the rejection or invalidation causes.</summary>
    public ImmutableArray<CalibrationProblem> Problems { get; }

    /// <summary>Gets whether this result came from synthetic data.</summary>
    public bool IsSynthetic { get; }

    /// <summary>Gets whether a current usable profile is present.</summary>
    public bool IsCalibrated => Status == CalibrationStatus.Calibrated && Profile is not null;

    /// <summary>Creates a calibration outcome.</summary>
    public CalibrationResult(
        CalibrationStatus status,
        CalibrationProfile? profile,
        CalibrationMetrics? metrics,
        ImmutableArray<CalibrationProblem> problems,
        bool isSynthetic)
    {
        Status = status;
        Profile = profile;
        Metrics = metrics;
        Problems = problems.IsDefault ? ImmutableArray<CalibrationProblem>.Empty : problems;
        IsSynthetic = isSynthetic;
    }
}
