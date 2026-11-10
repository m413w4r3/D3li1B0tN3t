using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Network;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestMissionTests
{
    private static HarvestMissionRequest Request(long destinationMapId = TestPlay.MapId)
    {
        return new HarvestMissionRequest(destinationMapId, TestPlay.Ash);
    }

    private static HarvestMissionRunner Runner(TestPlay play)
    {
        return new HarvestMissionRunner(new SupervisedElementUseSession(play.Bots, play.Tables), play.Tables);
    }

    private static async Task SetReadinessAsync(TestConnection connection, D3B.Bot.Runtime.BotActor bot, int weight = 100, int maxWeight = 1_000, int jobLevel = 1)
    {
        connection.Server(new InventoryWeightEvent { Weight = weight, MaxWeight = maxWeight });
        connection.Server(new JobExperiencesEvent
        {
            Jobs = { new JobExperience { JobId = TestPlay.Lumberjack, Level = jobLevel } },
        });
        await bot.ProcessedAsync();
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
    public async Task HappyPathTravelsApproachesUsesOnceAndVerifiesHarvest()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot);
        var runner = Runner(play);
        var run = runner.RunAsync(bot, Request(), TestContext.Current.CancellationToken);

        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 1, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30)));
        await TestWalks.WalkedAsync(play, connection, 1, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(play, connection, 2, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30)));
        TestWalks.Arrive(connection, TestPlay.MapId, 300);

        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 3, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30)));
        var useCell = await TestWalks.WalkedAsync(play, connection, 3, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        IMessage useMessage;

        try
        {
            useMessage = await TestWalks.SentAsync(play, connection, 4, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        }
        catch (Xunit.Sdk.XunitException exception)
        {
            var diagnostic = await TestWalks.AdvanceUntilDoneAsync(play.Time, run);
            var stageTrace = string.Join(Environment.NewLine, diagnostic.StageTrace.TakeLast(4).Select(entry =>
                $"{entry.StageCode}: {entry.Verdict}/{entry.ReasonCode}; values={string.Join(", ", entry.Values.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"))}"));

            throw new Xunit.Sdk.XunitException(
                $"Waiting for the use request failed: {exception.Message}{Environment.NewLine}" +
                $"Mission result: {diagnostic.Verdict}/{diagnostic.Stage}/{diagnostic.ReasonCode}{Environment.NewLine}" +
                $"Recent stage trace:{Environment.NewLine}{stageTrace}");
        }

        var useRequest = Assert.IsType<InteractiveUseRequest>(useMessage);
        Assert.Equal(TestPlay.TreeElementId, useRequest.ElementId);
        Assert.Equal(TestPlay.TreeInstanceId, useRequest.SkillInstanceId);
        await FrontierOpenAsync(play);

        connection.Server(new InteractiveUsedEvent
        {
            ActorId = TestPlay.CharacterId,
            ElementId = TestPlay.TreeElementId,
            SkillId = TestPlay.TreeSkillId,
            Duration = 30,
        });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject
            {
                Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 },
                Position = 63,
            },
        });
        connection.Server(new InventoryWeightEvent { Weight = 105, MaxWeight = 1_000 });
        await bot.ProcessedAsync();

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, run);
        var sent = connection.Sent.ToArray();

        Assert.Equal(HarvestMissionVerdict.Pass, result.Verdict);
        Assert.Equal(HarvestMissionStage.Stop, result.Stage);
        Assert.Equal("COMPLETE", result.ReasonCode);
        Assert.Equal(TestPlay.NorthMapId, result.StartMapId);
        Assert.Equal(TestPlay.MapId, result.DestinationMapId);
        Assert.Equal(1, result.PlannedTransitionCount);
        Assert.Equal(["Scroll"], result.PlannedTransitionTypes);
        Assert.Collection(result.TravelTransitions,
            transition =>
            {
                Assert.Equal(TestPlay.NorthMapId, transition.FromMapId);
                Assert.Equal(TestPlay.MapId, transition.ReachedMapId);
                Assert.Equal("Scroll", transition.Kind);
                Assert.Equal("Changed", transition.Outcome);
            });
        Assert.Equal("Verified", result.TargetIdentity);
        Assert.Equal(TestPlay.TreeElementId, result.TargetElementId);
        Assert.Equal(TestPlay.TreeCell, result.TargetCell);
        Assert.Equal(TestPlay.TreeSkillId, result.TargetSkillId);
        Assert.Equal(TestPlay.TreeInstanceId, result.TargetSkillInstanceId);
        Assert.Equal(TestPlay.Ash, result.ExpectedItemId);
        Assert.Equal(useCell, result.UseCell);
        Assert.Contains(useCell, result.ApproachPath);
        Assert.True(result.UseRequestSent);
        Assert.True(result.UseStartedObserved);
        Assert.True(result.UseEndedObserved);
        Assert.Equal(0, result.InventoryBefore);
        Assert.Equal(3, result.InventoryAfter);
        Assert.Equal(3, result.InventoryGain);
        Assert.Equal(100, result.WeightBefore);
        Assert.Equal(105, result.WeightAfter);
        Assert.Equal(1, result.WeightRevisionBefore);
        Assert.Equal(2, result.WeightRevisionAfter);
        Assert.Equal(
            ["CHECK_SESSION", "CHECK_PODS", "CHECK_JOB", "PLAN_ROUTE", "TRAVEL_TO_MAP", "DISCOVER_TARGET", "PLAN_APPROACH", "WALK_TO_USE_CELL", "REVALIDATE_ALL", "USE_ELEMENT", "VERIFY_HARVEST", "CHECK_PODS_AFTER", "STOP"],
            result.StageTrace.Select(entry => entry.StageCode));
        Assert.Equal(4, sent.Length);
        Assert.IsType<MapMovementRequest>(sent[0]);
        Assert.IsType<MapChangeRequest>(sent[1]);
        Assert.IsType<MapMovementRequest>(sent[2]);
        Assert.IsType<InteractiveUseRequest>(sent[3]);
        Assert.Single(sent.OfType<InteractiveUseRequest>());

        var second = await runner.RunAsync(bot, Request(), TestContext.Current.CancellationToken);

        Assert.Equal(HarvestMissionVerdict.Blocked, second.Verdict);
        Assert.Equal("ALREADY_RUN", second.ReasonCode);
        Assert.Equal(sent.Length, connection.Sent.Count);
    }

    [Fact]
    public async Task UnknownPodsBlockBeforeAnyRequest()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);

        var result = await Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken);

        Assert.Equal(HarvestMissionVerdict.Blocked, result.Verdict);
        Assert.Equal(HarvestMissionStage.CheckPods, result.Stage);
        Assert.Equal("WEIGHT_NOT_OBSERVED", result.ReasonCode);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AtThresholdPodsBlockBeforeAnyRequest()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot, weight: 900);

        var result = await Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken);

        Assert.Equal(HarvestMissionVerdict.Blocked, result.Verdict);
        Assert.Equal(HarvestMissionStage.CheckPods, result.Stage);
        Assert.Equal("PODS_THRESHOLD_REACHED", result.ReasonCode);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task UnknownJobBlocksBeforeTravel()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        connection.Server(new InventoryWeightEvent { Weight = 100, MaxWeight = 1_000 });
        await bot.ProcessedAsync();

        var result = await Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken);

        Assert.Equal(HarvestMissionVerdict.Blocked, result.Verdict);
        Assert.Equal(HarvestMissionStage.CheckJob, result.Stage);
        Assert.Equal("JOB_UNKNOWN", result.ReasonCode);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task JobLevelBelowTheSkillMinimumBlocksBeforeTravel()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot, jobLevel: 0);

        var result = await Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken);

        Assert.Equal(HarvestMissionVerdict.Blocked, result.Verdict);
        Assert.Equal(HarvestMissionStage.CheckJob, result.Stage);
        Assert.Equal("LEVEL_TOO_LOW", result.ReasonCode);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ClosedTranslationLatchBlocksAtSessionCheckWithoutRequests()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        connection.ServerRaw(TestGame.UnknownCarriedEvent());
        await bot.ProcessedAsync();

        var result = await Runner(play).RunAsync(bot, Request(), TestContext.Current.CancellationToken);

        Assert.Equal(HarvestMissionVerdict.Blocked, result.Verdict);
        Assert.Equal(HarvestMissionStage.CheckSession, result.Stage);
        Assert.Equal("TRANSLATION_LATCH_CLOSED", result.ReasonCode);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ARunnerCanOnlyBeRunOnce()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        var runner = Runner(play);

        var first = await runner.RunAsync(bot, Request(), TestContext.Current.CancellationToken);
        var sentBefore = connection.Sent.Count;
        var second = await runner.RunAsync(bot, Request(), TestContext.Current.CancellationToken);

        Assert.Equal("WEIGHT_NOT_OBSERVED", first.ReasonCode);
        Assert.Equal("ALREADY_RUN", second.ReasonCode);
        Assert.Equal(HarvestMissionStage.CheckSession, second.Stage);
        Assert.Equal(sentBefore, connection.Sent.Count);
    }

    [Fact]
    public async Task PlanIsPassiveAndFindsAReachableDestination()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot);
        var sentBefore = connection.Sent.Count;

        var plan = HarvestMissionPlan.Plan(bot, play.Tables, graphics: null, itemName: null, Request());

        Assert.Equal("PLAN_READY", plan.Verdict);
        Assert.Equal("PLAN_READY", plan.ReasonCode);
        Assert.Equal(TestPlay.NorthMapId, plan.StartMapId);
        Assert.Equal(1, plan.PlannedTransitionCount);
        Assert.Equal(["Scroll"], plan.PlannedTransitionTypes);
        Assert.Contains("TARGET_NOT_VISIBLE_UNTIL_ARRIVAL", plan.Missing);
        Assert.Null(plan.Target);
        Assert.Null(plan.Approach);
        Assert.Null(bot.Activity);
        Assert.Equal(sentBefore, connection.Sent.Count);
    }

    [Fact]
    public async Task PlanBlocksWhenTheDestinationHasNoRoute()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot);

        var plan = HarvestMissionPlan.Plan(bot, play.Tables, graphics: null, itemName: null, Request(42));

        Assert.Equal("PLAN_BLOCKED", plan.Verdict);
        Assert.Equal("NO_PATH", plan.ReasonCode);
        Assert.Empty(plan.PlannedTransitionTypes);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task PlanBlocksUnknownPodsAndClosedTranslationLatch()
    {
        await using var unknownPlay = new TestPlay();
        var (unknownConnection, unknownBot) = await unknownPlay.EnterAsync(300, TestPlay.NorthMapId);
        var unknown = HarvestMissionPlan.Plan(unknownBot, unknownPlay.Tables, graphics: null, itemName: null, Request());

        Assert.Equal("PLAN_BLOCKED", unknown.Verdict);
        Assert.Equal("WEIGHT_NOT_OBSERVED", unknown.ReasonCode);
        Assert.Empty(unknownConnection.Sent);

        await using var closedPlay = new TestPlay();
        var (closedConnection, closedBot) = await closedPlay.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(closedConnection, closedBot);
        closedConnection.ServerRaw(TestGame.UnknownCarriedEvent());
        await closedBot.ProcessedAsync();

        var closed = HarvestMissionPlan.Plan(closedBot, closedPlay.Tables, graphics: null, itemName: null, Request());

        Assert.Equal("PLAN_BLOCKED", closed.Verdict);
        Assert.Equal("TRANSLATION_LATCH_CLOSED", closed.ReasonCode);
        Assert.Empty(closedConnection.Sent);
    }

    [Fact]
    public async Task PlanIncludesVisibleVerifiedTargetAndApproachOnDestination()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        await SetReadinessAsync(connection, bot);
        var sentBefore = connection.Sent.Count;

        var plan = HarvestMissionPlan.Plan(bot, play.Tables, graphics: null, itemName: null, Request());

        Assert.Equal("PLAN_READY", plan.Verdict);
        Assert.Equal(0, plan.PlannedTransitionCount);
        Assert.Equal(TestPlay.TreeElementId, plan.Target!.ElementId);
        Assert.Equal("Verified", plan.Target.Identity.ToString());
        Assert.Equal(TestPlay.TreeCell, plan.Approach!.ElementCell);
        Assert.Equal("CONFIRMED", plan.Approach.Status);
        Assert.DoesNotContain("TARGET_NOT_VISIBLE_UNTIL_ARRIVAL", plan.Missing);
        Assert.Equal(sentBefore, connection.Sent.Count);
    }
}
