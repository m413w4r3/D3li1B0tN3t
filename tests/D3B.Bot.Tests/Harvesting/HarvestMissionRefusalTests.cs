using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Network;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestMissionRefusalTests
{
    private static HarvestMissionRequest Request(long destinationMapId = TestPlay.MapId, int wantedItemId = TestPlay.Ash)
    {
        return new HarvestMissionRequest(destinationMapId, wantedItemId);
    }

    private static HarvestMissionRunner Runner(TestPlay play)
    {
        return new HarvestMissionRunner(new SupervisedElementUseSession(play.Bots, play.Tables), play.Tables);
    }

    private static async Task SetReadinessAsync(TestConnection connection, D3B.Bot.Runtime.BotActor bot, int weight = 100, int maxWeight = 1_000, int? jobLevel = 1)
    {
        connection.Server(new InventoryWeightEvent { Weight = weight, MaxWeight = maxWeight });

        if (jobLevel is { } level)
        {
            connection.Server(new JobExperiencesEvent
            {
                Jobs = { new JobExperience { JobId = TestPlay.Lumberjack, Level = level } },
            });
        }

        await bot.ProcessedAsync();
    }

    private static async Task<HarvestMissionResult> AssertStoppedAsync(
        TestPlay play,
        TestConnection connection,
        D3B.Bot.Runtime.BotActor bot,
        Task<HarvestMissionResult> run,
        HarvestMissionStage stage,
        HarvestMissionVerdict verdict,
        string reason,
        int sentCount,
        int? expectedUseRequestCount = null)
    {
        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, run);

        Assert.Equal(stage, result.Stage);
        Assert.Equal(verdict, result.Verdict);
        Assert.Equal(reason, result.ReasonCode);
        Assert.Equal(sentCount, connection.Sent.Count);
        var useRequestCount = expectedUseRequestCount ?? (stage is HarvestMissionStage.UseElement or HarvestMissionStage.VerifyHarvest or HarvestMissionStage.CheckPodsAfter or HarvestMissionStage.Stop ? 1 : 0);
        Assert.Equal(useRequestCount, connection.Sent.OfType<InteractiveUseRequest>().Count());
        Assert.Empty(connection.Sent.OfType<InteractiveUseRequest>().Skip(1));
        Assert.Null(bot.Activity);

        return result;
    }

    [Fact]
    public async Task ClosedTranslationLatchStopsAtSessionCheck()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        connection.ServerRaw(TestGame.UnknownCarriedEvent());
        await bot.ProcessedAsync();

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken), HarvestMissionStage.CheckSession, HarvestMissionVerdict.Blocked, "TRANSLATION_LATCH_CLOSED", 0);
    }

    [Fact]
    public async Task UnknownWeightStopsAtPodsCheck()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken), HarvestMissionStage.CheckPods, HarvestMissionVerdict.Blocked, "WEIGHT_NOT_OBSERVED", 0);
    }

    [Fact]
    public async Task StaleWeightAfterInventoryChangeStopsAtPodsCheck()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        connection.Server(new InventoryWeightEvent { Weight = 100, MaxWeight = 1_000 });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 1 }, Position = 63 },
        });
        await bot.ProcessedAsync();

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken), HarvestMissionStage.CheckPods, HarvestMissionVerdict.Blocked, "WEIGHT_STALE", 0);
    }

    [Fact]
    public async Task ZeroCapacityStopsAtPodsCheck()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot, maxWeight: 0);

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken), HarvestMissionStage.CheckPods, HarvestMissionVerdict.Blocked, "CAPACITY_UNKNOWN_OR_ZERO", 0);
    }

    [Fact]
    public async Task ThresholdPodsStopAtPodsCheck()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot, weight: 900);

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken), HarvestMissionStage.CheckPods, HarvestMissionVerdict.Blocked, "PODS_THRESHOLD_REACHED", 0);
    }

    [Fact]
    public async Task MissingJobStopsAtJobCheck()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot, jobLevel: null);

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken), HarvestMissionStage.CheckJob, HarvestMissionVerdict.Blocked, "JOB_UNKNOWN", 0);
    }

    [Fact]
    public async Task UnknownJobLevelStopsAtJobCheck()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        connection.Server(new InventoryWeightEvent { Weight = 100, MaxWeight = 1_000 });
        connection.Server(new JobDescriptionEvent
        {
            Jobs = { new JobDescription { JobId = TestPlay.Lumberjack, Skills = { new JobSkill { SkillId = TestPlay.TreeSkillId } } } },
        });
        await bot.ProcessedAsync();

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken), HarvestMissionStage.CheckJob, HarvestMissionVerdict.Blocked, "JOB_LEVEL_UNKNOWN", 0);
    }

    [Fact]
    public async Task LowJobLevelStopsAtJobCheck()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot, jobLevel: 0);

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken), HarvestMissionStage.CheckJob, HarvestMissionVerdict.Blocked, "LEVEL_TOO_LOW", 0);
    }

    [Fact]
    public async Task UnknownWantedItemStopsAtJobCheck()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot);

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, Request(wantedItemId: 999_999), TestContext.Current.CancellationToken), HarvestMissionStage.CheckJob, HarvestMissionVerdict.Blocked, "CATALOG_UNKNOWN", 0);
    }

    [Fact]
    public async Task UnreachableDestinationStopsAtRoutePlanning()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot);

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, Request(42), TestContext.Current.CancellationToken), HarvestMissionStage.PlanRoute, HarvestMissionVerdict.Blocked, "NO_PATH", 0);
    }

    [Fact]
    public async Task TransitionLimitStopsAtRoutePlanning()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.EastMapId);
        await SetReadinessAsync(connection, bot);

        var request = new HarvestMissionRequest(TestPlay.NorthMapId, TestPlay.Ash, maxTransitions: 1);
        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, request, TestContext.Current.CancellationToken), HarvestMissionStage.PlanRoute, HarvestMissionVerdict.Blocked, "TRANSITION_LIMIT", 0);
    }

    [Fact]
    public async Task MissingResourceStopsAtDiscovery()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        await SetReadinessAsync(connection, bot);
        var run = Runner(play).RunAsync(bot, Request(destinationMapId: TestPlay.NorthMapId), TestContext.Current.CancellationToken);
        _ = await TestWalks.WalkedAsync(play, connection, 1, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await TestWalks.SentAsync(play, connection, 2, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30)));
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 300);

        await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.DiscoverTarget, HarvestMissionVerdict.Blocked, "TARGET_NOT_FOUND", 2);
    }

    [Fact]
    public async Task PodsThresholdDuringTravelStopsAtArrival()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        await SetReadinessAsync(connection, bot);
        var run = Runner(play).RunAsync(bot, Request(TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        _ = await TestWalks.WalkedAsync(play, connection, 1, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await TestWalks.SentAsync(play, connection, 2, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30)));
        connection.Server(new InventoryWeightEvent { Weight = 900, MaxWeight = 1_000 });
        await bot.ProcessedAsync();
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 300);

        await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.TravelToMap, HarvestMissionVerdict.Blocked, "PODS_THRESHOLD_REACHED", 2);
    }

    [Fact]
    public async Task JobLevelDuringTravelStopsAtArrival()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        await SetReadinessAsync(connection, bot);
        var run = Runner(play).RunAsync(bot, Request(TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        _ = await TestWalks.WalkedAsync(play, connection, 1, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await TestWalks.SentAsync(play, connection, 2, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30)));
        connection.Server(new JobExperiencesEvent
        {
            Jobs = { new JobExperience { JobId = TestPlay.Lumberjack, Level = 0 } },
        });
        await bot.ProcessedAsync();
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 300);

        await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.TravelToMap, HarvestMissionVerdict.Blocked, "LEVEL_TOO_LOW", 2);
    }

    [Fact]
    public async Task InvalidThresholdStopsBeforeAnyRequest()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, new HarvestMissionRequest(TestPlay.MapId, TestPlay.Ash, thresholdPercent: 101), TestContext.Current.CancellationToken), HarvestMissionStage.CheckSession, HarvestMissionVerdict.Blocked, "INVALID_REQUEST", 0);
    }

    [Fact]
    public async Task ClosedConnectionStopsAtSessionCheck()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        connection.Close();
        await bot.ProcessedAsync();

        await AssertStoppedAsync(play, connection, bot, Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken), HarvestMissionStage.CheckSession, HarvestMissionVerdict.Blocked, "SESSION_CLOSED", 0);
    }

    [Fact]
    public async Task ReusingRunnerStopsAsAlreadyRun()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        var runner = Runner(play);
        _ = await runner.RunAsync(bot, Request(), TestContext.Current.CancellationToken);

        await AssertStoppedAsync(play, connection, bot, runner.RunAsync(bot, Request(), TestContext.Current.CancellationToken), HarvestMissionStage.CheckSession, HarvestMissionVerdict.Blocked, "ALREADY_RUN", 0);
    }

    private static async Task<(TestConnection Connection, D3B.Bot.Runtime.BotActor Bot, TestPlay Play, Task<HarvestMissionResult> Run)> BeginUseAsync(TestPlay play)
    {
        var (connection, bot) = await play.EnterAsync(300);
        await SetReadinessAsync(connection, bot);
        var run = Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken);
        _ = await TestWalks.WalkedAsync(play, connection, 1, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        Assert.IsType<InteractiveUseRequest>(await TestWalks.SentAsync(play, connection, 2, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30)));
        await FrontierOpenAsync(play);

        return (connection, bot, play, run);
    }

    private static async Task FrontierOpenAsync(TestPlay play)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

        while (!play.Log.GetSnapshot().Any(record => record.Message.Contains("acceptance frontier open", StringComparison.Ordinal)))
        {
            Assert.True(DateTime.UtcNow < deadline, "The element-use acceptance frontier did not open.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task NoUseAnswerStopsAsUnconfirmed()
    {
        await using var play = new TestPlay();
        var (connection, bot, _, run) = await BeginUseAsync(play);

        var result = await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.UseElement, HarvestMissionVerdict.Blocked, "UNCONFIRMED", 2);
        Assert.Equal("NO_ANSWER", result.StageTrace.Last().Values.GetValueOrDefault("useReason"));
    }

    [Fact]
    public async Task UntrustedPositionAfterUseDoesNotPass()
    {
        await using var play = new TestPlay();
        var (connection, bot, _, run) = await BeginUseAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 1 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 1 }, Position = 63 },
        });
        connection.Server(new InventoryWeightEvent { Weight = 105, MaxWeight = 1_000 });
        connection.Server(new MapMovementRefusedEvent());
        await bot.ProcessedAsync();

        var result = await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.UseElement, HarvestMissionVerdict.Blocked, "UNCONFIRMED", 2, expectedUseRequestCount: 1);
        Assert.Equal("POSITION_NOT_TRUSTED_AFTER_SEND", result.StageTrace.Last().Values.GetValueOrDefault("useReason"));
    }

    [Fact]
    public async Task ServerUseRefusalStopsAtUseStage()
    {
        await using var play = new TestPlay();
        var (connection, bot, _, run) = await BeginUseAsync(play);
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.TreeElementId, SkillInstanceId = TestPlay.TreeInstanceId });
        await bot.ProcessedAsync();

        await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.UseElement, HarvestMissionVerdict.Refused, "SERVER_REFUSED", 2);
    }

    [Fact]
    public async Task ZeroDurationUseStopsAsUnconfirmed()
    {
        await using var play = new TestPlay();
        var (connection, bot, _, run) = await BeginUseAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 0 });
        await bot.ProcessedAsync();

        await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.UseElement, HarvestMissionVerdict.Blocked, "UNCONFIRMED", 2);
    }

    [Fact]
    public async Task MissingUseEndStopsAsUnconfirmed()
    {
        await using var play = new TestPlay();
        var (connection, bot, _, run) = await BeginUseAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 1 });
        await bot.ProcessedAsync();

        await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.UseElement, HarvestMissionVerdict.Blocked, "UNCONFIRMED", 2);
    }

    [Fact]
    public async Task UseEndWithoutStartDoesNotConfirmHarvest()
    {
        await using var play = new TestPlay();
        var (connection, bot, _, run) = await BeginUseAsync(play);
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        await bot.ProcessedAsync();

        var result = await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.UseElement, HarvestMissionVerdict.Blocked, "UNCONFIRMED", 2);
        Assert.Equal("NO_ANSWER", result.StageTrace.Last().Values.GetValueOrDefault("useReason"));
    }

    [Fact]
    public async Task UseStartedByAnotherActorDoesNotConfirmHarvest()
    {
        await using var play = new TestPlay();
        var (connection, bot, _, run) = await BeginUseAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId + 1, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 1 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        await bot.ProcessedAsync();

        var result = await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.UseElement, HarvestMissionVerdict.Blocked, "UNCONFIRMED", 2);
        Assert.Equal("NO_ANSWER", result.StageTrace.Last().Values.GetValueOrDefault("useReason"));
    }

    [Fact]
    public async Task MissingItemGainStopsAsUnconfirmed()
    {
        await using var play = new TestPlay();
        var (connection, bot, _, run) = await BeginUseAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 1 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        await bot.ProcessedAsync();

        await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.UseElement, HarvestMissionVerdict.Blocked, "UNCONFIRMED", 2);
    }

    [Fact]
    public async Task WalkRefusalStopsBeforeUse()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        await SetReadinessAsync(connection, bot);
        var run = Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken);
        _ = await TestWalks.SentAsync(play, connection, 1, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        var movement = Assert.IsType<MapMovementRequest>(connection.Sent[0]);
        var point = D3B.Engine.Maps.MapGeometry.PointOf(movement.KeyMovements[0] & 0xFFF);
        connection.Server(new MapMovementRefusedEvent { X = point.X, Y = point.Y });
        await bot.ProcessedAsync();

        await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.WalkToUseCell, HarvestMissionVerdict.Blocked, "WALK_REFUSED", 1);
    }

    [Fact]
    public async Task WalkNotConfirmedStopsBeforeUse()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        await SetReadinessAsync(connection, bot);
        var run = Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken);
        var movement = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 1, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30)));
        var start = movement.KeyMovements[0] & 0xFFF;
        var end = movement.KeyMovements[^1] & 0xFFF;
        Assert.True(D3B.Engine.Maps.CellPathfinder.TryFind(play.Maps.Get(movement.MapId).Cells.Span, start, end, out var path));
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        connection.Client(new MapMovementConfirmRequest());

        await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.WalkToUseCell, HarvestMissionVerdict.Blocked, "WALK_NOT_CONFIRMED", 1);
    }

    [Fact]
    public async Task TranslationLatchClosedAfterWalkStopsBeforeUse()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        await SetReadinessAsync(connection, bot);
        var run = Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken);
        var movement = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 1, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30)));
        var start = movement.KeyMovements[0] & 0xFFF;
        var end = movement.KeyMovements[^1] & 0xFFF;
        Assert.True(D3B.Engine.Maps.CellPathfinder.TryFind(play.Maps.Get(movement.MapId).Cells.Span, start, end, out var path));
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        connection.ServerRaw(TestGame.UnknownCarriedEvent());
        await bot.ProcessedAsync();
        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());
        await bot.ProcessedAsync();

        await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.RevalidateAll, HarvestMissionVerdict.Blocked, "TRANSLATION_LATCH_CLOSED", 1, expectedUseRequestCount: 0);
    }

    [Fact]
    public async Task MissingWeightRefreshAfterHarvestStopsAtPostCheck()
    {
        await using var play = new TestPlay();
        var (connection, bot, _, run) = await BeginUseAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 1 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 1 }, Position = 63 },
        });
        await bot.ProcessedAsync();

        await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.CheckPodsAfter, HarvestMissionVerdict.Blocked, "WEIGHT_NOT_REFRESHED", 2);
    }

    [Fact]
    public async Task RefreshedPodsAtThresholdStopCleanlyAfterHarvest()
    {
        await using var play = new TestPlay();
        var (connection, bot, _, run) = await BeginUseAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 1 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 1 }, Position = 63 },
        });
        connection.Server(new InventoryWeightEvent { Weight = 900, MaxWeight = 1_000 });
        await bot.ProcessedAsync();

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, run);

        Assert.Equal(HarvestMissionStage.Stop, result.Stage);
        Assert.Equal(HarvestMissionVerdict.Pass, result.Verdict);
        Assert.Equal("PODS_THRESHOLD_REACHED", result.ReasonCode);
        Assert.Equal(2, connection.Sent.Count);
        Assert.Single(connection.Sent.OfType<InteractiveUseRequest>());
        Assert.Null(bot.Activity);
    }

    [Fact]
    public async Task RemovedExpectedItemGainCannotPassVerification()
    {
        await using var play = new TestPlay();
        var (connection, bot, _, run) = await BeginUseAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 1 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 1 }, Position = 63 },
        });
        await bot.ProcessedAsync();
        await Task.Delay(25, TestContext.Current.CancellationToken);
        connection.Server(new ObjectsDeletedEvent { Uids = { 12 } });
        await bot.ProcessedAsync();

        var result = await AssertStoppedAsync(play, connection, bot, run, HarvestMissionStage.UseElement, HarvestMissionVerdict.Blocked, "UNCONFIRMED", 2);
        Assert.Equal("ITEM_GAIN_NOT_PRESENT_AT_VERDICT", result.StageTrace.Last().Values.GetValueOrDefault("useReason"));
    }
}
