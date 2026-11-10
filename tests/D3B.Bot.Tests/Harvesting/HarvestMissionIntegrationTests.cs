using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestMissionIntegrationTests(ITestOutputHelper output)
{
    private static HarvestMissionRunner Runner(TestPlay play)
    {
        return new HarvestMissionRunner(new SupervisedElementUseSession(play.Bots, play.Tables), play.Tables);
    }

    private static async Task SetReadinessAsync(TestConnection connection, D3B.Bot.Runtime.BotActor bot)
    {
        connection.Server(new JobDescriptionEvent
        {
            Jobs = { new JobDescription { JobId = TestPlay.Lumberjack, Skills = { new JobSkill { SkillId = TestPlay.TreeSkillId } } } },
        });
        connection.Server(new JobExperiencesEvent
        {
            Jobs = { new JobExperience { JobId = TestPlay.Lumberjack, Level = 1 } },
        });
        connection.Server(new InventoryWeightEvent { Weight = 100, MaxWeight = 1_000 });
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

    private static async Task<IMessage> WalkAndChangeAsync(TestPlay play, TestConnection connection, long destination, int cell, int requestCount)
    {
        await TestWalks.WalkedAsync(play, connection, requestCount, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        var change = await TestWalks.SentAsync(play, connection, requestCount + 1, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        Assert.Equal(new MapChangeRequest { MapId = (int)destination }, change);
        TestWalks.Arrive(connection, destination, cell);
        return change;
    }

    [Fact]
    public async Task MissionJournalShowsTheFullHarvestChain()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot);
        var run = Runner(play).RunAsync(bot, new HarvestMissionRequest(TestPlay.MapId, TestPlay.Ash), TestContext.Current.CancellationToken);

        await WalkAndChangeAsync(play, connection, TestPlay.MapId, 300, 1);
        var approachWalk = await TestWalks.SentAsync(play, connection, 3, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        Assert.IsType<MapMovementRequest>(approachWalk);
        var useCell = await TestWalks.WalkedAsync(play, connection, 3, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30));
        var useRequest = Assert.IsType<InteractiveUseRequest>(await TestWalks.SentAsync(play, connection, 4, advance: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(30)));
        Assert.Equal((TestPlay.TreeElementId, TestPlay.TreeInstanceId), (useRequest.ElementId, useRequest.SkillInstanceId));
        await FrontierOpenAsync(play);

        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent { Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 } });
        connection.Server(new InventoryWeightEvent { Weight = 105, MaxWeight = 1_000 });
        await bot.ProcessedAsync();
        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, run);

        Assert.Equal(HarvestMissionVerdict.Pass, result.Verdict);
        Assert.Equal(HarvestMissionStage.Stop, result.Stage);
        Assert.Equal("COMPLETE", result.ReasonCode);
        Assert.Equal(1, result.PlannedTransitionCount);
        Assert.Equal([(TestPlay.NorthMapId, (long?)TestPlay.MapId, (long?)TestPlay.MapId, "Changed")],
            result.TravelTransitions.Select(t => (t.FromMapId, t.RequestedMapId, t.ReachedMapId, t.Outcome)));
        Assert.Equal(TestPlay.TreeElementId, result.TargetElementId);
        Assert.Equal(TestPlay.Ash, result.ExpectedItemId);
        Assert.Contains(useCell, result.ApproachPath);
        Assert.Equal(result.UseCell, useCell);
        Assert.True(result.UseRequestSent);
        Assert.True(result.UseStartedObserved);
        Assert.True(result.UseEndedObserved);
        Assert.Equal((0, 3, 3), (result.InventoryBefore, result.InventoryAfter, result.InventoryGain));
        Assert.Equal((100, 105, 1L, 2L), (result.WeightBefore, result.WeightAfter, result.WeightRevisionBefore, result.WeightRevisionAfter));
        Assert.Single(connection.Sent.OfType<InteractiveUseRequest>());

        var jobTrace = result.StageTrace.Single(entry => entry.StageCode == "CHECK_JOB");
        var podsTrace = result.StageTrace.Single(entry => entry.StageCode == "CHECK_PODS");
        var startCell = result.ApproachPath[0];
        var lines = new[]
        {
            $"MISSION START destination_map={result.DestinationMapId}",
            $"CHARACTER weight={result.WeightBefore} max_weight={podsTrace.Values["maxWeight"]} load={podsTrace.Values["loadPercent"]}%",
            $"JOB level={jobTrace.Values["jobLevel"]} required_level={jobTrace.Values["requiredLevel"]} status=OK",
            $"ROUTE start_map={result.StartMapId} destination_map={result.DestinationMapId} transitions={result.PlannedTransitionCount}",
            "MAP CHANGE 1 confirmed",
            $"RESOURCE element={result.TargetElementId} item={result.ExpectedItemId} status=AVAILABLE",
            $"APPROACH from_cell={startCell} use_cell={result.UseCell} status=CONFIRMED",
            $"HARVEST request_sent={result.UseRequestSent.ToString().ToLowerInvariant()} used={result.UseStartedObserved.ToString().ToLowerInvariant()} ended={result.UseEndedObserved.ToString().ToLowerInvariant()}",
            $"INVENTORY item={result.ExpectedItemId} before={result.InventoryBefore} after={result.InventoryAfter} gain={result.InventoryGain}",
            $"PODS after={result.WeightAfter} threshold={result.ThresholdPercent}%",
            "MISSION PASS",
        };
        Assert.All(lines, line => output.WriteLine(line));
        Assert.Equal(11, lines.Length);
        Assert.Contains($"MISSION START destination_map={TestPlay.MapId}", lines[0]);
        Assert.Contains("CHARACTER weight=100", lines[1]);
        Assert.Contains("JOB level=1 required_level=1 status=OK", lines[2]);
        Assert.Contains("ROUTE", lines[3]);
        Assert.Equal("MAP CHANGE 1 confirmed", lines[4]);
        Assert.Contains($"RESOURCE element={TestPlay.TreeElementId} item={TestPlay.Ash} status=AVAILABLE", lines[5]);
        Assert.StartsWith("APPROACH from_cell=", lines[6]);
        Assert.Equal("HARVEST request_sent=true used=true ended=true", lines[7]);
        Assert.Contains($"INVENTORY item={TestPlay.Ash} before=0 after=3 gain=3", lines[8]);
        Assert.Contains("PODS after=105 threshold=90%", lines[9]);
        Assert.Equal("MISSION PASS", lines[10]);
        Assert.Equal(13, result.StageTrace.Length);
    }

    [Fact]
    public async Task TwoMapTransitionsAreConfirmedInOrder()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.EastMapId);
        await SetReadinessAsync(connection, bot);
        var run = Runner(play).RunAsync(bot, new HarvestMissionRequest(TestPlay.NorthMapId, TestPlay.Ash), TestContext.Current.CancellationToken);

        await WalkAndChangeAsync(play, connection, TestPlay.MapId, 300, 1);
        await WalkAndChangeAsync(play, connection, TestPlay.NorthMapId, 300, 3);
        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, run);

        Assert.Equal(HarvestMissionVerdict.Blocked, result.Verdict);
        Assert.Equal(HarvestMissionStage.DiscoverTarget, result.Stage);
        Assert.Equal("TARGET_NOT_FOUND", result.ReasonCode);
        Assert.Equal(2, result.PlannedTransitionCount);
        Assert.Collection(result.TravelTransitions,
            transition => Assert.Equal((TestPlay.EastMapId, (long?)TestPlay.MapId, (long?)TestPlay.MapId, "Changed"),
                (transition.FromMapId, transition.RequestedMapId, transition.ReachedMapId, transition.Outcome)),
            transition => Assert.Equal((TestPlay.MapId, (long?)TestPlay.NorthMapId, (long?)TestPlay.NorthMapId, "Changed"),
                (transition.FromMapId, transition.RequestedMapId, transition.ReachedMapId, transition.Outcome)));
        Assert.Equal(TestPlay.NorthMapId, bot.State.MapId);
        Assert.True(bot.State.IsPositionTrusted);
        Assert.Empty(connection.Sent.OfType<InteractiveUseRequest>());
        Assert.Equal(4, connection.Sent.Count);
    }
}
