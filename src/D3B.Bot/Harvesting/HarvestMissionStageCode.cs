namespace D3B.Bot.Harvesting;

internal static class HarvestMissionStageCode
{
    public static string Of(HarvestMissionStage stage)
    {
        return stage switch
        {
            HarvestMissionStage.CheckSession => "CHECK_SESSION",
            HarvestMissionStage.CheckPods => "CHECK_PODS",
            HarvestMissionStage.CheckJob => "CHECK_JOB",
            HarvestMissionStage.PlanRoute => "PLAN_ROUTE",
            HarvestMissionStage.TravelToMap => "TRAVEL_TO_MAP",
            HarvestMissionStage.DiscoverTarget => "DISCOVER_TARGET",
            HarvestMissionStage.PlanApproach => "PLAN_APPROACH",
            HarvestMissionStage.WalkToUseCell => "WALK_TO_USE_CELL",
            HarvestMissionStage.RevalidateAll => "REVALIDATE_ALL",
            HarvestMissionStage.UseElement => "USE_ELEMENT",
            HarvestMissionStage.VerifyHarvest => "VERIFY_HARVEST",
            HarvestMissionStage.CheckPodsAfter => "CHECK_PODS_AFTER",
            HarvestMissionStage.Stop => "STOP",
            _ => throw new ArgumentOutOfRangeException(nameof(stage)),
        };
    }
}
