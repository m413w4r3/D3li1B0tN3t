using System.Collections.Immutable;
using D3B.Bot.Interactives;
using D3B.Data;

namespace D3B.Bot.Harvesting;

/// <summary>A passive snapshot of session, capacity, job, and exact resource readiness.</summary>
internal sealed record HarvestReadiness
{
    public HarvestPods Pods { get; init; } = new();

    public HarvestJob? Job { get; init; }

    public HarvestResourceTargetSelection? Target { get; init; }

    public string Verdict { get; init; } = "BLOCKED";

    public ImmutableArray<string> Missing { get; init; } = [];

    public long SessionEpoch { get; init; }

    public long StateRevision { get; init; }

    public bool TranslationBlocked { get; init; }

    public bool Paused { get; init; }

    public bool Acting { get; init; }

    public bool Connected { get; init; }

    public bool SessionAttached { get; init; }

    /// <summary>Checks readiness after arrival; multiple eligible targets remain blocked until the caller resolves one explicitly.</summary>
    public static HarvestReadiness CheckAfterArrival(ElementUseSnapshot snapshot, GameTables tables, int wantedItemId, int thresholdPercent = 90)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(tables);

        var report = ElementCandidates.Build(snapshot, tables, graphics: null, itemName: null);
        var target = HarvestResourceTarget.Select(report, wantedItemId);
        var job = JobForArrival(snapshot, tables, target, wantedItemId);

        return Build(snapshot, tables, job, target, thresholdPercent, requireTarget: true);
    }

    /// <summary>Checks passive pre-travel readiness from the item catalog, without asserting a current map target or enabled skill.</summary>
    public static HarvestReadiness CheckBeforeTravel(ElementUseSnapshot snapshot, GameTables tables, int wantedItemId, int thresholdPercent = 90)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(tables);

        var job = HarvestJob.CheckFromCatalog(snapshot.State, tables, wantedItemId);

        return Build(snapshot, tables, job, target: null, thresholdPercent, requireTarget: false);
    }

    private static HarvestJob? JobForArrival(ElementUseSnapshot snapshot, GameTables tables, HarvestResourceTargetSelection target, int wantedItemId)
    {
        if (target.EligibleTargets.IsDefaultOrEmpty)
            return HarvestJob.CheckFromCatalog(snapshot.State, tables, wantedItemId);

        var skillIds = target.EligibleTargets.Select(item => item.SkillId).Distinct().ToArray();
        if (skillIds.Length != 1)
            return HarvestJob.CheckFromCatalog(snapshot.State, tables, wantedItemId);

        return HarvestJob.Check(snapshot.State, tables, skillIds[0], skillEnabled: true, provenance: "server+catalog");
    }

    private static HarvestReadiness Build(
        ElementUseSnapshot snapshot,
        GameTables tables,
        HarvestJob? job,
        HarvestResourceTargetSelection? target,
        int thresholdPercent,
        bool requireTarget)
    {
        ArgumentNullException.ThrowIfNull(tables);

        var pods = HarvestPods.From(snapshot.State, thresholdPercent);
        var missing = ImmutableArray.CreateBuilder<string>();

        if (!snapshot.Connected)
            missing.Add("CONNECTED_REQUIRED");

        if (!snapshot.SessionAttached)
            missing.Add("SESSION_REQUIRED");

        if (snapshot.TranslationBlocked)
            missing.Add("TRANSLATION_BLOCKED");

        if (snapshot.Paused)
            missing.Add("BOT_PAUSED");

        if (snapshot.Acting)
            missing.Add("BOT_ACTING");

        if (pods.Blocker is { } podsBlocker)
            missing.Add(podsBlocker);

        if (job is { Status: not "OK" } && job.Status is { } jobStatus)
            missing.Add(jobStatus);

        if (requireTarget && target is not null && target.Verdict is not HarvestResourceTargetVerdict.Identified)
        {
            missing.Add(target.Verdict switch
            {
                HarvestResourceTargetVerdict.Multiple => "TARGET_MULTIPLE",
                HarvestResourceTargetVerdict.Ambiguous => "TARGET_AMBIGUOUS",
                _ => "TARGET_NOT_FOUND",
            });
        }

        return new HarvestReadiness
        {
            Pods = pods,
            Job = job,
            Target = target,
            Verdict = missing.Count == 0 ? "READY" : "BLOCKED",
            Missing = missing.ToImmutable(),
            SessionEpoch = snapshot.SessionEpoch,
            StateRevision = snapshot.Revision,
            TranslationBlocked = snapshot.TranslationBlocked,
            Paused = snapshot.Paused,
            Acting = snapshot.Acting,
            Connected = snapshot.Connected,
            SessionAttached = snapshot.SessionAttached,
        };
    }
}
