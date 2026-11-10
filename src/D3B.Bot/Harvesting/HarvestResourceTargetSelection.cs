using System.Collections.Immutable;

namespace D3B.Bot.Harvesting;

/// <summary>The eligible targets, rejected candidates, and verdict of an exact resource selection.</summary>
internal sealed record HarvestResourceTargetSelection
{
    public HarvestResourceTargetVerdict Verdict { get; init; }

    public ImmutableArray<HarvestResourceTarget> EligibleTargets { get; init; } = [];

    public ImmutableArray<HarvestResourceTargetRejection> RejectedCandidates { get; init; } = [];
}
