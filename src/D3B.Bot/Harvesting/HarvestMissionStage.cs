namespace D3B.Bot.Harvesting;

internal enum HarvestMissionStage
{
    CheckSession,
    CheckPods,
    CheckJob,
    PlanRoute,
    TravelToMap,
    DiscoverTarget,
    PlanApproach,
    WalkToUseCell,
    RevalidateAll,
    UseElement,
    VerifyHarvest,
    CheckPodsAfter,
    Stop,
}
