using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using D3B.Data;
using D3B.Data.Jobs;
using D3B.Data.Packs;
using D3B.Data.Tests;
using D3B.Gui.Safety;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestReadinessTests
{
    private static CharacterState State()
    {
        return new CharacterState { Key = TestPlay.Key, Name = "Character" };
    }

    private static ElementUseSnapshot Snapshot(
        CharacterState? state = null,
        bool connected = true,
        bool sessionAttached = true,
        bool translationBlocked = false,
        bool paused = false,
        bool acting = false)
    {
        return new ElementUseSnapshot(state ?? State(), null, 12, 34, connected, sessionAttached, translationBlocked, paused, acting, GameInputMode.ProtocolInjection);
    }

    private static ElementCandidate Candidate(
        int elementId,
        int itemId = TestPlay.Ash,
        ElementCandidateIdentity identity = ElementCandidateIdentity.Verified,
        bool? skillEnabled = true,
        int? elementState = 0,
        int? skillId = TestPlay.TreeSkillId,
        int? skillInstanceId = 10,
        int? cell = 176,
        ImmutableArray<int>? possibleItemIds = null)
    {
        return new ElementCandidate
        {
            ElementId = elementId,
            MapId = TestPlay.MapId,
            ServerKnown = true,
            PackKnown = true,
            InteractiveId = 34,
            ServerCell = cell,
            GfxId = TestPlay.TreeGfxId,
            ElementState = elementState,
            SkillId = skillId,
            SkillInstanceId = skillInstanceId,
            SkillEnabled = skillEnabled,
            GatheredItemId = identity is ElementCandidateIdentity.Verified ? itemId : null,
            PossibleItemIds = possibleItemIds ?? [itemId],
            Identity = identity,
            IdentitySource = "server+catalog",
        };
    }

    [Fact]
    public void UnreportedDefaultWeightIsNotKnownPods()
    {
        var pods = HarvestPods.From(State());

        Assert.False(pods.Known);
        Assert.Null(pods.Weight);
        Assert.Null(pods.FreePods);
        Assert.Equal("WEIGHT_NOT_OBSERVED", pods.Blocker);
        Assert.Equal("Unknown", pods.Provenance);
    }

    [Fact]
    public void ObservedZeroCapacityBlocksBeforeThresholdMath()
    {
        var state = State() with
        {
            Weight = 0,
            MaxWeight = 0,
            WeightProvenance = InventoryWeightProvenance.ServerEvent,
        };

        var pods = HarvestPods.From(state);

        Assert.False(pods.Known);
        Assert.Equal(0, pods.Weight);
        Assert.Equal(0, pods.MaxWeight);
        Assert.Null(pods.FreePods);
        Assert.Null(pods.LoadPercent);
        Assert.Equal("CAPACITY_UNKNOWN_OR_ZERO", pods.Blocker);
    }

    [Fact]
    public void PodsUseUnroundedLoadAndConfiguredThreshold()
    {
        var below = CharacterEvents.Apply(State(), RelayDirection.FromServer, new InventoryWeightEvent { Weight = 8_999, MaxWeight = 10_000 });
        var atNinety = CharacterEvents.Apply(State(), RelayDirection.FromServer, new InventoryWeightEvent { Weight = 9_000, MaxWeight = 10_000 });
        var atCustom = CharacterEvents.Apply(State(), RelayDirection.FromServer, new InventoryWeightEvent { Weight = 8_000, MaxWeight = 10_000 });

        var belowPods = HarvestPods.From(below);
        Assert.Equal(1_001, belowPods.FreePods);
        Assert.Equal(89.99, belowPods.LoadPercent);
        Assert.Null(belowPods.Blocker);
        Assert.Equal("PODS_THRESHOLD_REACHED", HarvestPods.From(atNinety).Blocker);
        Assert.Equal("PODS_THRESHOLD_REACHED", HarvestPods.From(atCustom, thresholdPercent: 80).Blocker);
    }

    [Fact]
    public void WeightEventsSetProvenanceAndAdvanceRevision()
    {
        var first = CharacterEvents.Apply(State(), RelayDirection.FromServer, new InventoryWeightEvent { Weight = 1, MaxWeight = 10 });
        var second = CharacterEvents.Apply(first, RelayDirection.FromServer, new InventoryWeightEvent { Weight = 2, MaxWeight = 10 });

        Assert.Equal(InventoryWeightProvenance.ServerEvent, first.WeightProvenance);
        Assert.Equal(1, first.WeightRevision);
        Assert.Equal(2, second.WeightRevision);
        Assert.Equal(2, HarvestPods.From(second).WeightRevision);
    }

    [Fact]
    public async Task JobChecksRequireAReportedLevelAndTheSkillMinimum()
    {
        await using var play = new TestPlay();
        var state = State();

        Assert.Equal("JOB_UNKNOWN", HarvestJob.Check(state, play.Tables, TestPlay.TreeSkillId, true, "server+catalog")!.Status);

        var described = CharacterEvents.Apply(state, RelayDirection.FromServer, new JobDescriptionEvent
        {
            Jobs = { new JobDescription { JobId = TestPlay.Lumberjack, Skills = { new JobSkill { SkillId = TestPlay.TreeSkillId } } } },
        });
        Assert.False(described.Jobs[TestPlay.Lumberjack].LevelKnown);
        Assert.Equal("JOB_LEVEL_UNKNOWN", HarvestJob.Check(described, play.Tables, TestPlay.TreeSkillId, true, "server+catalog")!.Status);

        var belowLevel = CharacterEvents.Apply(described, RelayDirection.FromServer, new JobExperiencesEvent
        {
            Jobs = { new JobExperience { JobId = TestPlay.Lumberjack, Level = 0 } },
        });
        Assert.True(belowLevel.Jobs[TestPlay.Lumberjack].LevelKnown);
        Assert.Equal("LEVEL_TOO_LOW", HarvestJob.Check(belowLevel, play.Tables, TestPlay.TreeSkillId, true, "server+catalog")!.Status);

        var sufficient = CharacterEvents.Apply(belowLevel, RelayDirection.FromServer, new JobExperiencesEvent
        {
            Jobs = { new JobExperience { JobId = TestPlay.Lumberjack, Level = 1 } },
        });
        var enabled = HarvestJob.Check(sufficient, play.Tables, TestPlay.TreeSkillId, true, "server+catalog")!;
        var disabled = HarvestJob.Check(sufficient, play.Tables, TestPlay.TreeSkillId, false, "server+catalog")!;

        Assert.Equal("OK", enabled.Status);
        Assert.Equal("server+catalog", enabled.Provenance);
        Assert.Equal("SKILL_DISABLED", disabled.Status);
    }

    [Fact]
    public void CatalogChecksDistinguishUnknownAmbiguousAndUniqueItems()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("jobs.pack");
        var info = new PackInfo { ClientVersion = "test", Fingerprint = "0123456789abcdef", ExtractorVersion = "test" };
        using var stream = new MemoryStream();
        using (var writer = new PackWriter(stream, info, 3))
        {
            writer.Add(JobCatalog.EntryName, new JobCatalog(
                new[] { new Job(10, 0, Job.NoIcon), new Job(11, 0, Job.NoIcon) },
                new[] { new Skill(1, 0, 10, 50, 1, 1, false), new Skill(2, 0, 11, 50, 1, 1, false), new Skill(3, 0, 10, 51, 2, 1, false) }));
            writer.Complete();
        }

        File.WriteAllBytes(path, stream.ToArray());
        using var pack = GamePack.Open(path);
        var tables = new GameTables(pack);
        var state = State() with
        {
            Jobs = ImmutableSortedDictionary<int, CharacterJob>.Empty.Add(10, new CharacterJob(10, 4, 0, 0, 0, []) { LevelKnown = true }),
        };

        Assert.Equal("CATALOG_UNKNOWN", HarvestJob.CheckFromCatalog(state, tables, 99)!.Status);
        Assert.Equal("CATALOG_AMBIGUOUS", HarvestJob.CheckFromCatalog(state, tables, 50)!.Status);

        var unique = HarvestJob.CheckFromCatalog(state, tables, 51)!;
        Assert.Equal("OK", unique.Status);
        Assert.Equal("catalog", unique.Provenance);
        Assert.False(unique.SkillEnabled);
        Assert.False(unique.SkillEnabledKnown);
    }

    [Fact]
    public void ResourceSelectionRequiresOneVerifiedEnabledAvailableExactCandidate()
    {
        var identified = HarvestResourceTarget.Select(new ElementCandidateReport { Candidates = [Candidate(1)] }, TestPlay.Ash);
        var multiple = HarvestResourceTarget.Select(new ElementCandidateReport { Candidates = [Candidate(1), Candidate(2)] }, TestPlay.Ash);
        var ambiguous = HarvestResourceTarget.Select(new ElementCandidateReport
        {
            Candidates = [Candidate(3, identity: ElementCandidateIdentity.Ambiguous, possibleItemIds: [TestPlay.Ash])],
        }, TestPlay.Ash);
        var disabled = HarvestResourceTarget.Select(new ElementCandidateReport { Candidates = [Candidate(4, skillEnabled: false)] }, TestPlay.Ash);
        var depleted = HarvestResourceTarget.Select(new ElementCandidateReport { Candidates = [Candidate(5, elementState: 1)] }, TestPlay.Ash);
        var wrongItem = HarvestResourceTarget.Select(new ElementCandidateReport { Candidates = [Candidate(6, itemId: TestPlay.Chestnut)] }, TestPlay.Ash);

        Assert.Equal(HarvestResourceTargetVerdict.Identified, identified.Verdict);
        Assert.Equal(1, Assert.Single(identified.EligibleTargets).ElementId);
        Assert.Equal(HarvestResourceTargetVerdict.Multiple, multiple.Verdict);
        Assert.Equal(2, multiple.EligibleTargets.Length);
        Assert.Equal(HarvestResourceTargetVerdict.Ambiguous, ambiguous.Verdict);
        Assert.Equal("AMBIGUOUS_EVIDENCE", Assert.Single(ambiguous.RejectedCandidates).Reason);
        Assert.Equal(HarvestResourceTargetVerdict.NotFound, disabled.Verdict);
        Assert.Equal("SKILL_DISABLED_OR_UNKNOWN", Assert.Single(disabled.RejectedCandidates).Reason);
        Assert.Equal(HarvestResourceTargetVerdict.NotFound, depleted.Verdict);
        Assert.Equal("ELEMENT_UNAVAILABLE", Assert.Single(depleted.RejectedCandidates).Reason);
        Assert.Equal(HarvestResourceTargetVerdict.NotFound, wrongItem.Verdict);
        Assert.Empty(wrongItem.EligibleTargets);
    }

    [Fact]
    public async Task ReadinessListsEverySessionBlockerBeforePodsJobAndTarget()
    {
        await using var play = new TestPlay();
        var snapshot = Snapshot(connected: false, sessionAttached: false, translationBlocked: true, paused: true, acting: true);

        var readiness = HarvestReadiness.CheckBeforeTravel(snapshot, play.Tables, TestPlay.Ash);

        Assert.Equal("BLOCKED", readiness.Verdict);
        Assert.Equal(
            ["CONNECTED_REQUIRED", "SESSION_REQUIRED", "TRANSLATION_BLOCKED", "BOT_PAUSED", "BOT_ACTING", "WEIGHT_NOT_OBSERVED", "JOB_UNKNOWN"],
            readiness.Missing);
        Assert.Null(readiness.Target);
        Assert.DoesNotContain("TARGET_NOT_FOUND", readiness.Missing);
    }

    [Fact]
    public async Task ReadinessSnapshotSendsNothingAndStartsNoActivity()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);

        connection.Server(new InventoryWeightEvent { Weight = 100, MaxWeight = 1_000 });
        connection.Server(new JobDescriptionEvent
        {
            Jobs = { new JobDescription { JobId = TestPlay.Lumberjack, Skills = { new JobSkill { SkillId = TestPlay.TreeSkillId } } } },
        });
        connection.Server(new JobExperiencesEvent
        {
            Jobs = { new JobExperience { JobId = TestPlay.Lumberjack, Level = 1 } },
        });
        await bot.ProcessedAsync();

        var sentBefore = connection.Sent.Count;
        var readiness = HarvestReadiness.CheckAfterArrival(SupervisedElementUse.Capture(bot), play.Tables, TestPlay.Ash);

        Assert.Equal("READY", readiness.Verdict);
        Assert.Equal(HarvestResourceTargetVerdict.Identified, readiness.Target!.Verdict);
        Assert.Null(bot.Activity);
        Assert.Equal(sentBefore, connection.Sent.Count);
        Assert.DoesNotContain(connection.Sent.Skip(sentBefore), request => request is InteractiveUseRequest or MapMovementRequest or MapChangeRequest);
    }
}
