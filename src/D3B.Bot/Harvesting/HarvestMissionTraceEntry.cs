using System.Collections.Immutable;

namespace D3B.Bot.Harvesting;

internal sealed record HarvestMissionTraceEntry(
    HarvestMissionStage Stage,
    HarvestMissionVerdict Verdict,
    string ReasonCode,
    ImmutableDictionary<string, string?> Values)
{
    public string StageCode
    {
        get
        {
            return HarvestMissionStageCode.Of(Stage);
        }
    }
}
