using System.Collections.Immutable;
using D3B.Bot.Interactives;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Engine.World;

namespace D3B.Bot.Harvesting;

internal sealed record HarvestMissionPlan
{
    public string Verdict { get; init; } = "PLAN_BLOCKED";

    public string ReasonCode { get; init; } = "NO_PATH";

    public ImmutableArray<string> Missing { get; init; } = [];

    public ElementUseSnapshot Snapshot { get; init; } = null!;

    public long? StartMapId { get; init; }

    public long DestinationMapId { get; init; }

    public ImmutableArray<string> PlannedTransitionTypes { get; init; } = [];

    public int PlannedTransitionCount
    {
        get
        {
            return PlannedTransitionTypes.Length;
        }
    }

    public HarvestPods Pods { get; init; } = new();

    public HarvestJob? Job { get; init; }

    public HarvestResourceTarget? Target { get; init; }

    public HarvestApproach? Approach { get; init; }

    public long SessionEpoch { get; init; }

    public long StateRevision { get; init; }

    public static HarvestMissionPlan Plan(
        BotActor bot,
        GameTables tables,
        GraphicSkills? graphics,
        Func<int, string?>? itemName,
        HarvestMissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(request);
        HarvestMissionRunner.ValidateRequest(request);

        var snapshot = SupervisedElementUse.Capture(bot);
        var pods = HarvestPods.From(snapshot.State, request.ThresholdPercent);
        var job = JobFromSnapshot(snapshot, tables, request.WantedItemId, graphics, itemName);
        var missing = ImmutableArray.CreateBuilder<string>();
        string? blocker = null;

        if (!snapshot.Connected || !snapshot.SessionAttached)
        {
            AddBlocker(missing, ref blocker, "SESSION_CLOSED");
        }

        if (snapshot.TranslationBlocked)
        {
            AddBlocker(missing, ref blocker, "TRANSLATION_LATCH_CLOSED");
        }

        if (snapshot.Paused)
        {
            AddBlocker(missing, ref blocker, "BOT_PAUSED");
        }

        if (snapshot.Acting)
        {
            AddBlocker(missing, ref blocker, "BOT_ACTING");
        }

        if (pods.Blocker is { } podsBlocker)
        {
            AddBlocker(missing, ref blocker, podsBlocker);
        }

        if (job is { Status: not "OK" })
        {
            AddBlocker(missing, ref blocker, job.Status);
        }

        ImmutableArray<string> plannedTypes = [];
        var routeFound = TryPlanRoute(bot, snapshot, request, out var path);

        if (!routeFound)
        {
            AddBlocker(missing, ref blocker, "NO_PATH");
        }
        else
        {
            plannedTypes = TransitionTypes(path!);

            if (plannedTypes.Length > request.MaxTransitions)
            {
                AddBlocker(missing, ref blocker, "TRANSITION_LIMIT");
            }
        }

        HarvestResourceTarget? target = null;
        HarvestApproach? approach = null;

        if (snapshot.State.MapId == request.DestinationMapId)
        {
            var report = ElementCandidates.Build(snapshot, tables, graphics, itemName);
            var selection = HarvestResourceTarget.Select(report, request.WantedItemId);
            var reachable = ReachableTargets(snapshot, selection);

            if (reachable.Length > 0)
            {
                target = reachable[0].Target;
                approach = reachable[0].Approach;

                if (selection.Verdict is HarvestResourceTargetVerdict.Multiple)
                {
                    missing.Add("MULTIPLE_TARGETS_RESOLVED_BY_APPROACH");
                }
            }
            else
            {
                var targetBlocker = selection.Verdict switch
                {
                    HarvestResourceTargetVerdict.Ambiguous => "TARGET_AMBIGUOUS",
                    HarvestResourceTargetVerdict.Multiple => "NO_REACHABLE_TARGET",
                    _ => "TARGET_NOT_FOUND",
                };

                AddBlocker(missing, ref blocker, targetBlocker);
            }
        }
        else
        {
            missing.Add("TARGET_NOT_VISIBLE_UNTIL_ARRIVAL");
        }

        var reason = blocker ?? "PLAN_READY";

        return new HarvestMissionPlan
        {
            Verdict = blocker is null ? "PLAN_READY" : "PLAN_BLOCKED",
            ReasonCode = reason,
            Missing = missing.ToImmutable(),
            Snapshot = snapshot,
            StartMapId = snapshot.State.MapId,
            DestinationMapId = request.DestinationMapId,
            PlannedTransitionTypes = plannedTypes,
            Pods = pods,
            Job = job,
            Target = target,
            Approach = approach,
            SessionEpoch = snapshot.SessionEpoch,
            StateRevision = snapshot.Revision,
        };
    }

    internal static HarvestJob? JobFromSnapshot(
        ElementUseSnapshot snapshot,
        GameTables tables,
        int wantedItemId,
        GraphicSkills? graphics,
        Func<int, string?>? itemName)
    {
        var report = ElementCandidates.Build(snapshot, tables, graphics, itemName);
        var candidates = report.Candidates
            .Where(candidate => candidate.Identity is ElementCandidateIdentity.Verified
                && candidate.GatheredItemId == wantedItemId
                && candidate.SkillId is not null)
            .ToArray();
        var skillIds = candidates.Select(candidate => candidate.SkillId!.Value).Distinct().ToArray();

        if (skillIds.Length is 1)
        {
            var enabled = candidates.Any(candidate => candidate.SkillEnabled is true);
            var enabledKnown = candidates.Any(candidate => candidate.SkillEnabled is not null);

            if (enabledKnown)
            {
                return HarvestJob.Check(snapshot.State, tables, skillIds[0], enabled, "server+catalog");
            }
        }

        return HarvestJob.CheckFromCatalog(snapshot.State, tables, wantedItemId);
    }

    internal static ImmutableArray<(HarvestResourceTarget Target, HarvestApproach Approach)> ReachableTargets(
        ElementUseSnapshot snapshot,
        HarvestResourceTargetSelection selection)
    {
        if (snapshot.Map is null)
        {
            return [];
        }

        return selection.EligibleTargets
            .Select(target => (Target: target, Approach: HarvestApproachPlanner.Plan(snapshot.State, snapshot.Map, target.ElementId, target.Cell)))
            .Where(item => item.Approach.Status is "CONFIRMED")
            .OrderBy(item => item.Approach.Distance)
            .ThenBy(item => item.Target.ElementId)
            .ToImmutableArray();
    }

    private static bool TryPlanRoute(BotActor bot, ElementUseSnapshot snapshot, HarvestMissionRequest request, out WorldPath? path)
    {
        path = null;
        var state = snapshot.State;

        if (state.MapId is not { } mapId || state.Cell is not { } cell || snapshot.Map is null
            || !bot.World.TryGetVertex(snapshot.Map, cell, out var from))
        {
            return false;
        }

        var rules = new WorldPathRules
        {
            Facts = WorldTravel.FactsOf(state, bot.Time.GetUtcNow()),
            KnownZaaps = state.KnownZaaps,
            Kamas = state.Kamas,
            NoTeleportFrom = bot.NoTeleportFrom,
            NoNpcWays = bot.NoNpcWays,
        };

        if (mapId == request.DestinationMapId)
        {
            path = new WorldPath(from, []);

            return true;
        }

        return bot.World.TryFind(from, new HashSet<long> { request.DestinationMapId }, rules, out path);
    }

    private static ImmutableArray<string> TransitionTypes(WorldPath path)
    {
        var builder = ImmutableArray.CreateBuilder<string>(path.Steps.Count);

        foreach (var step in path.Steps)
        {
            builder.Add(step.IsTeleport ? "Zaap" : step.IsNpcWay ? "Npc" : step.Transition.Type.ToString());
        }

        return builder.ToImmutable();
    }

    private static void AddBlocker(ImmutableArray<string>.Builder missing, ref string? blocker, string reason)
    {
        missing.Add(reason);
        blocker ??= reason;
    }
}
