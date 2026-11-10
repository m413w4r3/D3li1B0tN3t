namespace D3B.Bot.Harvesting;

internal sealed record HarvestMissionTransitionEvidence(
    long FromMapId,
    long? RequestedMapId,
    long? ReachedMapId,
    string Kind,
    string Outcome);
