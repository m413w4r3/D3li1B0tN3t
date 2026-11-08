using System.Collections.Immutable;

namespace D3B.Gui.Calibration;

/// <summary>Quantitative fit and independent validation measurements.</summary>
public sealed record CalibrationMetrics
{
    /// <summary>Gets the number of fitting anchors.</summary>
    public int FitAnchorCount { get; }

    /// <summary>Gets the number of independent validation anchors.</summary>
    public int ValidationAnchorCount { get; }

    /// <summary>Gets the median fitting residual, or null when no fit residual is available.</summary>
    public double? MedianFitErrorPixels { get; }

    /// <summary>Gets the maximum fitting residual, or null when no fit residual is available.</summary>
    public double? MaxFitErrorPixels { get; }

    /// <summary>Gets the maximum independent validation residual, or null when unavailable.</summary>
    public double? MaxValidationErrorPixels { get; }

    /// <summary>Gets the fraction of fitting anchors accepted by RANSAC.</summary>
    public double InlierRatio { get; }

    /// <summary>Gets fitting residuals in fitting-anchor order.</summary>
    public ImmutableArray<double> FitResidualsPixels { get; }

    /// <summary>Gets independent validation residuals in validation-anchor order.</summary>
    public ImmutableArray<double> ValidationErrorsPixels { get; }

    /// <summary>Gets indexes in the document's complete anchor array rejected by RANSAC.</summary>
    public ImmutableArray<int> RejectedAnchorIndexes { get; }

    /// <summary>Creates a metrics value.</summary>
    public CalibrationMetrics(
        int fitAnchorCount,
        int validationAnchorCount,
        double? medianFitErrorPixels,
        double? maxFitErrorPixels,
        double? maxValidationErrorPixels,
        double inlierRatio,
        ImmutableArray<double> fitResidualsPixels,
        ImmutableArray<double> validationErrorsPixels,
        ImmutableArray<int> rejectedAnchorIndexes)
    {
        FitAnchorCount = fitAnchorCount;
        ValidationAnchorCount = validationAnchorCount;
        MedianFitErrorPixels = medianFitErrorPixels;
        MaxFitErrorPixels = maxFitErrorPixels;
        MaxValidationErrorPixels = maxValidationErrorPixels;
        InlierRatio = inlierRatio;
        FitResidualsPixels = fitResidualsPixels.IsDefault ? ImmutableArray<double>.Empty : fitResidualsPixels;
        ValidationErrorsPixels = validationErrorsPixels.IsDefault ? ImmutableArray<double>.Empty : validationErrorsPixels;
        RejectedAnchorIndexes = rejectedAnchorIndexes.IsDefault ? ImmutableArray<int>.Empty : rejectedAnchorIndexes;
    }
}
