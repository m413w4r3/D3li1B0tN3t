using System.Collections.Immutable;

namespace D3B.Gui.Vision;

/// <summary>Matching measurements, configured thresholds, and evidence for a target candidate.</summary>
public sealed record TargetConfidence
{
    /// <summary>Gets the best normalized correlation score.</summary>
    public double Score { get; }

    /// <summary>Gets the best independent runner-up score, or -1 when no independent location exists.</summary>
    public double RunnerUpScore { get; }

    /// <summary>Gets the difference between the best and runner-up scores.</summary>
    public double Margin { get; }

    /// <summary>Gets the minimum accepted normalized correlation score.</summary>
    public double RequiredScore { get; }

    /// <summary>Gets the minimum accepted difference between independent peaks.</summary>
    public double RequiredMargin { get; }

    /// <summary>Gets the immutable checks and failure causes observed for the candidate.</summary>
    public ImmutableArray<TargetEvidence> Evidence { get; }

    /// <summary>Creates confidence evidence for one localization attempt.</summary>
    public TargetConfidence(
        double score,
        double runnerUpScore,
        double margin,
        double requiredScore,
        double requiredMargin,
        ImmutableArray<TargetEvidence> evidence)
    {
        Score = score;
        RunnerUpScore = runnerUpScore;
        Margin = margin;
        RequiredScore = requiredScore;
        RequiredMargin = requiredMargin;
        Evidence = evidence.IsDefault ? ImmutableArray<TargetEvidence>.Empty : evidence;
    }
}
