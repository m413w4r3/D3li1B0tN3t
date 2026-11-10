using D3B.Bot.Characters;

namespace D3B.Bot.Harvesting;

/// <summary>A passive, provenance-aware view of the character's remaining inventory capacity.</summary>
internal sealed record HarvestPods
{
    public int? Weight { get; init; }

    public int? MaxWeight { get; init; }

    public int? FreePods { get; init; }

    public double? LoadPercent { get; init; }

    public bool Known { get; init; }

    public string Provenance { get; init; } = "Unknown";

    public long WeightRevision { get; init; }

    public int ThresholdPercent { get; init; } = 90;

    public string? Blocker { get; init; }

    public static HarvestPods From(CharacterState state, int thresholdPercent = 90)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfLessThan(thresholdPercent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(thresholdPercent, 100);

        var observed = state.WeightProvenance is InventoryWeightProvenance.ServerEvent;
        var fresh = observed && state.WeightInventoryRevision == state.InventoryRevision;
        var weight = fresh ? state.Weight : (int?)null;
        var maxWeight = fresh ? state.MaxWeight : (int?)null;
        var known = fresh && state.MaxWeight > 0;
        int? freePods = known ? state.MaxWeight - state.Weight : null;
        double? loadPercent = known ? 100.0 * state.Weight / state.MaxWeight : null;
        var blocker = !observed
            ? "WEIGHT_NOT_OBSERVED"
            : !fresh
                ? "WEIGHT_STALE"
            : state.MaxWeight <= 0
                ? "CAPACITY_UNKNOWN_OR_ZERO"
                : loadPercent >= thresholdPercent
                    ? "PODS_THRESHOLD_REACHED"
                    : null;

        return new HarvestPods
        {
            Weight = weight,
            MaxWeight = maxWeight,
            FreePods = freePods,
            LoadPercent = loadPercent,
            Known = known,
            Provenance = state.WeightProvenance.ToString(),
            WeightRevision = state.WeightRevision,
            ThresholdPercent = thresholdPercent,
            Blocker = blocker,
        };
    }
}
