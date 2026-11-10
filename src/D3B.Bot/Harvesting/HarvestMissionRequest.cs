namespace D3B.Bot.Harvesting;

internal sealed record HarvestMissionRequest
{
    public long DestinationMapId { get; init; }

    public int WantedItemId { get; init; }

    public int ThresholdPercent { get; init; }

    public int MaxTransitions { get; init; }

    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(5);

    public HarvestMissionRequest(
        long destinationMapId,
        int wantedItemId,
        int thresholdPercent = 90,
        int maxTransitions = 8,
        TimeSpan? maxDuration = null)
    {
        DestinationMapId = destinationMapId;
        WantedItemId = wantedItemId;
        ThresholdPercent = thresholdPercent;
        MaxTransitions = maxTransitions;
        MaxDuration = maxDuration ?? TimeSpan.FromMinutes(5);
    }
}
