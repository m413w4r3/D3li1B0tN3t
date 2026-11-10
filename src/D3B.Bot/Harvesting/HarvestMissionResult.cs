using System.Collections.Immutable;

namespace D3B.Bot.Harvesting;

internal sealed record HarvestMissionResult
{
    public HarvestMissionVerdict Verdict { get; init; }

    public HarvestMissionStage Stage { get; init; }

    public string ReasonCode { get; init; } = "UNKNOWN";

    public ImmutableArray<HarvestMissionTraceEntry> StageTrace { get; init; } = [];

    public long? StartMapId { get; init; }

    public long DestinationMapId { get; init; }

    public int PlannedTransitionCount { get; init; }

    public ImmutableArray<string> PlannedTransitionTypes { get; init; } = [];

    public ImmutableArray<HarvestMissionTransitionEvidence> TravelTransitions { get; init; } = [];

    public string? TargetIdentity { get; init; }

    public int? TargetCell { get; init; }

    public int? TargetElementId { get; init; }

    public int? TargetInteractiveId { get; init; }

    public int? TargetGfxId { get; init; }

    public int? TargetSkillId { get; init; }

    public int? TargetSkillInstanceId { get; init; }

    public int? ExpectedItemId { get; init; }

    public int? UseCell { get; init; }

    public ImmutableArray<int> ApproachPath { get; init; } = [];

    public bool UseRequestSent { get; init; }

    public bool UseStartedObserved { get; init; }

    public bool UseEndedObserved { get; init; }

    public int? InventoryBefore { get; init; }

    public int? InventoryAfter { get; init; }

    public int? InventoryGain { get; init; }

    public int? WeightBefore { get; init; }

    public int? WeightAfter { get; init; }

    public long WeightRevisionBefore { get; init; }

    public long WeightRevisionAfter { get; init; }

    public int ThresholdPercent { get; init; }

    public long SessionEpoch { get; init; }

    public long StateRevision { get; init; }

    public string StopReason { get; init; } = "UNKNOWN";
}
