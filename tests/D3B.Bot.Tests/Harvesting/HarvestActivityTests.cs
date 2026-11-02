using System.Collections.Immutable;
using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Journal;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestActivityTests : IAsyncDisposable
{
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en");

    private readonly TestPlay _play = new();
    private readonly HarvestSightings _sightings = new();
    private readonly GraphicSkills _graphics = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static string Say(GameText text)
    {
        return string.Format(_english, text.Format.In(_english), [.. text.Values.Select(value => value.Text ?? value.Id.ToString(_english))]);
    }

    private static async Task<ActivityReport> ReportedAsync(BotActivity activity, Func<ActivityReport, bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (activity.Report is not { } report || !condition(report))
        {
            Assert.True(DateTime.UtcNow < deadline, $"The activity still says {(activity.Report is { } last ? Say(last.Doing) : "nothing")}.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        return activity.Report;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The bot never got there.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    private static CharacterState Weighing(int weight, int maxWeight)
    {
        return new CharacterState { Key = TestPlay.Key, Name = "Character", Weight = weight, MaxWeight = maxWeight };
    }

    private HarvestActivity Activity(HarvestSettings settings)
    {
        return new HarvestActivity(settings, _play.World, _play.Tables, _play.Maps, _sightings, _graphics, _play.Places, new Regrowths(), _play.Time);
    }

    [Fact]
    public async Task TheWantedResourceIsHarvestedThenTheTourGoesOnToTheNextMap()
    {
        var (connection, bot) = await _play.EnterAsync(433);
        var activity = Activity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] });
        var run = bot.StartActivity(activity);

        await TestWalks.WalkedAsync(_play, connection, 1);

        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = TestPlay.TreeInstanceId, ElementId = TestPlay.TreeElementId }, await connection.SentAsync(2));
        Assert.Equal(TestPlay.TreeElementId, activity.Report!.Focus!.TargetElementId);
        Assert.Equal("Harvesting 303", Say(activity.Report.Doing));

        connection.Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: false) });
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 15 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent { Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 } });

        var travelling = await ReportedAsync(activity, report => Say(report.Doing) == "Heading to the next map");

        Assert.Equal(["303 3"], travelling.Counts.Select(count => Say(count.Text)));
        Assert.Equal(TestPlay.Ash, travelling.Counts[0].ItemId);
        Assert.True(_sightings.TryGet(TestPlay.MapId, out var seen));
        Assert.Equal([TestPlay.Ash], seen.Resources);
        Assert.Contains(travelling.Focus!.Zone, map => map.MapId == TestPlay.MapId && map.State is ZoneMapState.Visited);
        Assert.Equal("Tour 1, map 2 of 4", Say(travelling.Progress!.Text));

        // The journey to the nearest map of the tour begins with a walk to the edge, an action cell or the door.
        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(_play, connection, 3));

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task ThePlaceTheActivityChoosesIsTheSubAreaRichestInWhatTheCharacterCanHarvestNamedFirst()
    {
        var (connection, bot) = await _play.EnterAsync(433);
        var activity = Activity(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], Place = HarvestPlace.Automatic });
        var run = bot.StartActivity(activity);

        await TestWalks.WalkedAsync(_play, connection, 1);

        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = TestPlay.TreeInstanceId, ElementId = TestPlay.TreeElementId }, await connection.SentAsync(2));
        Assert.Equal($"{TestPlay.SubAreaId}, tour 1, map 1 of 4", Say(activity.Report!.Progress!.Text));
        Assert.Equal(5, activity.TeleportChanges);

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task AResourceAboveTheCharactersProfessionIsWarnedOfAndLeftForTheOthers()
    {
        var (connection, bot) = await _play.EnterAsync(190);
        var activity = Activity(new HarvestSettings { Resources = [TestPlay.Chestnut, TestPlay.Ash], Area = 1 });
        var run = bot.StartActivity(activity);

        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = TestPlay.TreeInstanceId, ElementId = TestPlay.TreeElementId }, await connection.SentAsync(1));

        var report = activity.Report!;

        Assert.Equal(["473 needs 2 20, the character is 1"], report.Warnings.Select(Say));
        Assert.Equal(["473 0", "303 0"], report.Counts.Select(count => Say(count.Text)));
        Assert.Equal([TestPlay.TreeElementId], report.Focus!.WantedElementIds);

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task AFullCharacterBanksWhatHarvestingBringsAndKeepsTheRest()
    {
        var (connection, bot) = await _play.EnterAsync(396, TestPlay.InsideMapId);

        connection.Server(new InventoryContentEvent
        {
            Kamas = 1_000,
            Objects =
            {
                new InventoryObject { Item = new ObjectItem { Uid = 30, ItemId = 2_400, Quantity = 1 }, Position = 1 },
                new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 40 }, Position = 63 },
                new InventoryObject { Item = new ObjectItem { Uid = 13, ItemId = 1_782, Quantity = 21 }, Position = 63 },
            },
        });
        connection.Server(new InventoryWeightEvent { Weight = 7_000, MaxWeight = 7_590 });
        await bot.ProcessedAsync();

        var activity = Activity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] });
        var run = bot.StartActivity(activity);

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(_play, connection, 1));
        Assert.Equal("Going to the bank", Say(activity.Report!.Doing));

        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 48_366, Replies = { new NpcReply { ReplyId = TestPlay.ChestReplyId } } });

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.ChestReplyId }, await TestWalks.SentAsync(_play, connection, 2));

        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage, MaxSlots = int.MaxValue });
        connection.Server(new StorageContentEvent { Kamas = 5 });

        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { 12 } }, await TestWalks.SentAsync(_play, connection, 3));

        connection.Server(new ObjectsDeletedEvent { Uids = { 12 } });
        connection.Server(new InventoryWeightEvent { Weight = 700, MaxWeight = 7_590 });

        Assert.Equal(new DialogLeaveRequest(), await TestWalks.SentAsync(_play, connection, 4));

        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });

        var after = await ReportedAsync(activity, report => report.Counts.Any(count => Say(count.Text) == "1 bank"));

        Assert.Equal(["303 0", "1 bank"], after.Counts.Select(count => Say(count.Text)));
        Assert.Equal([13, 30], bot.State.Inventory.Keys);

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task ACharacterStillFullOnceBankedFailsTheActivitySayingWhatToChange()
    {
        var (connection, bot) = await _play.EnterAsync(396, TestPlay.InsideMapId);

        connection.Server(new InventoryContentEvent
        {
            Objects =
            {
                new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 40 }, Position = 63 },
                new InventoryObject { Item = new ObjectItem { Uid = 13, ItemId = 1_782, Quantity = 300 }, Position = 63 },
            },
        });
        connection.Server(new InventoryWeightEvent { Weight = 7_000, MaxWeight = 7_590 });
        await bot.ProcessedAsync();

        var run = bot.StartActivity(Activity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] }));

        await TestWalks.SentAsync(_play, connection, 1);
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 48_366, Replies = { new NpcReply { ReplyId = TestPlay.ChestReplyId } } });
        await TestWalks.SentAsync(_play, connection, 2);
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage, MaxSlots = int.MaxValue });
        connection.Server(new StorageContentEvent());
        await TestWalks.SentAsync(_play, connection, 3);

        // The ash goes; what is left still weighs more than the share the character banks at.
        connection.Server(new ObjectsDeletedEvent { Uids = { 12 } });
        connection.Server(new InventoryWeightEvent { Weight = 6_900, MaxWeight = 7_590 });
        await TestWalks.SentAsync(_play, connection, 4);
        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });

        Assert.Equal(ActivityOutcome.Failed, await run.Completion.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        var failure = Assert.IsType<ActivityException>(run.Failure);

        Assert.StartsWith("Still full once banked, 6900 of 7590 pods: what is left is not harvest.", Say(failure.Reason!), StringComparison.Ordinal);
        Assert.Contains("90%", Say(failure.Reason!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AZoneWithoutAMapFailsTheActivitySayingWhy()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var run = bot.StartActivity(Activity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [999] }));

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);

        var failure = Assert.IsType<ActivityException>(run.Failure);

        Assert.Equal("The zone holds no map to harvest.", Say(failure.Reason!));
    }

    [Fact]
    public async Task AFightResumesTheActivityWhichSaysItWaitsMeanwhile()
    {
        var (connection, bot) = await _play.EnterAsync(433);
        var activity = Activity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] });
        var run = bot.StartActivity(activity);

        await connection.SentAsync(1);
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });

        await ReportedAsync(activity, report => Say(report.Doing) == "Attacked: waiting for the fight's end");
        Assert.Equal(Interruption.Fight, run.LastInterruption);
        Assert.False(run.IsOver);

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public void ACharacterIsFullAtTheSettingsShareOfItsPods()
    {
        var activity = Activity(new HarvestSettings { FullAt = 80 });

        Assert.False(activity.IsFull(Weighing(6_071, 7_590)));
        Assert.True(activity.IsFull(Weighing(6_072, 7_590)));
        Assert.False(activity.IsFull(Weighing(0, 0)));
    }

    [Fact]
    public async Task AFightAsAHarvestEndsIsAProtectorsAndPausesTheBotWhenTheProfileSaysSo()
    {
        var (connection, bot) = await _play.EnterAsync(433);
        var activity = Activity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId], AfterFight = HarvestAfterFight.Pause });
        var run = bot.StartActivity(activity);

        await TestWalks.WalkedAsync(_play, connection, 1);
        await connection.SentAsync(2);

        // Measured: the protector's fight comes 2 ms after the harvest's end, its objects between.
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });

        var fighting = await ReportedAsync(activity, report => Say(report.Doing) == "A protector attacked: waiting for the fight's end");

        Assert.Contains("1 fight, protectors 1", fighting.Counts.Select(count => Say(count.Text)));

        // The AI plays the fight first: the profile's answer comes once it is over (D198).
        await bot.ProcessedAsync();
        Assert.False(bot.IsPaused);

        connection.Server(Ended(FightOutcome.Won));
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        await WaitForAsync(() => bot.IsPaused);

        var interrupted = bot.Journal.All().OfType<ActivityInterruptedEntry>().Single();

        Assert.Equal((Interruption.Fight, InterruptionResponse.Pause), (interrupted.Reason, interrupted.Response));
        Assert.Equal("a protector, as harvesting 303 ended", Say(interrupted.Note!));
        Assert.Contains(bot.Journal.All(), entry => entry is BotEventEntry { Event: BotEvent.PausedAfterFight });
        Assert.False(run.IsOver);

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    [Fact]
    public async Task AFightOtherwiseIsAnAggressionAndEndsTheActivityWhenTheProfileSaysSo()
    {
        var (connection, bot) = await _play.EnterAsync(433);
        var activity = Activity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId], AfterFight = HarvestAfterFight.Stop });
        var run = bot.StartActivity(activity);

        await connection.SentAsync(1);
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await bot.ProcessedAsync();

        Assert.False(run.IsOver);

        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Equal("an aggression", Say(bot.Journal.All().OfType<ActivityInterruptedEntry>().Single().Note!));
        Assert.False(bot.IsPaused);
    }

    [Fact]
    public async Task ALostFightIsAnsweredAsTheProfileSaysAndThreeInARowEndTheActivity()
    {
        var (connection, bot) = await _play.EnterAsync(433);
        var activity = Activity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId], AfterLostFight = HarvestAfterFight.Pause });
        var run = bot.StartActivity(activity);

        await connection.SentAsync(1);
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(Ended(FightOutcome.Lost));
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        await WaitForAsync(() => bot.IsPaused);

        Assert.Equal(InterruptionResponse.Resume, bot.Journal.All().OfType<ActivityInterruptedEntry>().Single().Response);
        Assert.Equal(InterruptionResponse.Pause, activity.OnFightOver(new FightEnd(FightOutcome.Lost, 0, 0, ImmutableSortedDictionary<int, int>.Empty), InterruptionResponse.Resume));
        Assert.Equal(InterruptionResponse.Stop, activity.OnFightOver(new FightEnd(FightOutcome.Lost, 0, 0, ImmutableSortedDictionary<int, int>.Empty), InterruptionResponse.Resume));
        Assert.Equal(InterruptionResponse.Resume, activity.OnFightOver(new FightEnd(FightOutcome.Won, 0, 0, ImmutableSortedDictionary<int, int>.Empty), InterruptionResponse.Resume));
        Assert.False(run.IsOver);

        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
    }

    private static FightEndEvent Ended(FightOutcome outcome)
    {
        return new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = TestPlay.CharacterId }, Outcome = outcome } } };
    }

    [Fact]
    public async Task UnderTheShareOfLifeTheCharacterEatsBeforeItHarvestsAndABagIsOpenedFirst()
    {
        var (connection, bot) = await _play.EnterAsync(433);

        connection.Server(new InventoryContentEvent
        {
            Objects =
            {
                new InventoryObject { Item = new ObjectItem { Uid = 50, ItemId = TestPlay.Bread, Quantity = 3 }, Position = InventoryItem.NotWorn },
                new InventoryObject { Item = new ObjectItem { Uid = 51, ItemId = TestPlay.WheatBag, Quantity = 1 }, Position = InventoryItem.NotWorn },
            },
        });
        connection.Server(new LifeRegenEndEvent { Life = 300, MaxLife = 1_000 });
        await bot.ProcessedAsync();

        var activity = Activity(new HarvestSettings
        {
            Resources = [TestPlay.Ash],
            SubAreas = [TestPlay.SubAreaId],
            OpenBags = true,
            Heal = true,
            HealBelow = 50,
            Food = [TestPlay.Bread],
        });
        var run = bot.StartActivity(activity);

        Assert.Equal(new ObjectUseRequest { Uid = 51 }, await connection.SentAsync(1));
        connection.Server(new ObjectDeletedEvent { Uid = 51 });
        connection.Server(new InventoryWeightEvent { Weight = 100, MaxWeight = 1_000 });

        // The whole stack asked for in one request: the server eats only what the life lacks (D156).
        Assert.Equal(new ObjectUseMultipleRequest { Uid = 50, Quantity = 3 }, await TestWalks.SentAsync(_play, connection, 2));
        connection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 50, Quantity = 2 } });
        connection.Server(new LifeRegenEndEvent { Life = 1_000, MaxLife = 1_000 });
        connection.Server(new InventoryWeightEvent { Weight = 99, MaxWeight = 1_000 });

        // Whole again, the character goes on harvesting.
        await TestWalks.WalkedAsync(_play, connection, 3);

        var report = await ReportedAsync(activity, report => report.Counts.Any(count => Say(count.Text) == "1 food eaten"));

        Assert.Contains("1 bag opened", report.Counts.Select(count => Say(count.Text)));

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task WithNoFoodChosenTheCharacterEatsWhatOnlyHealsTheSmallestHealFirst()
    {
        var (connection, bot) = await _play.EnterAsync(433);

        connection.Server(new InventoryContentEvent
        {
            Objects =
            {
                new InventoryObject { Item = new ObjectItem { Uid = 52, ItemId = TestPlay.ClassPotion, Quantity = 1 }, Position = InventoryItem.NotWorn },
                new InventoryObject { Item = new ObjectItem { Uid = 53, ItemId = TestPlay.Loaf, Quantity = 2 }, Position = InventoryItem.NotWorn },
                new InventoryObject { Item = new ObjectItem { Uid = 50, ItemId = TestPlay.Bread, Quantity = 3 }, Position = InventoryItem.NotWorn },
            },
        });
        connection.Server(new LifeRegenEndEvent { Life = 300, MaxLife = 1_000 });
        await bot.ProcessedAsync();

        var activity = Activity(new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId], Heal = true, HealBelow = 50 });
        var run = bot.StartActivity(activity);

        // The bread heals less than the loaf; the potion, whose use does more than heal, is never eaten.
        Assert.Equal(new ObjectUseMultipleRequest { Uid = 50, Quantity = 3 }, await TestWalks.SentAsync(_play, connection, 1));
        connection.Server(new ObjectDeletedEvent { Uid = 50 });
        connection.Server(new LifeRegenEndEvent { Life = 336, MaxLife = 1_000 });

        Assert.Equal(new ObjectUseMultipleRequest { Uid = 53, Quantity = 2 }, await TestWalks.SentAsync(_play, connection, 2));
        connection.Server(new ObjectDeletedEvent { Uid = 53 });
        connection.Server(new LifeRegenEndEvent { Life = 416, MaxLife = 1_000 });

        // Nothing left that heals: the life regenerates, the potion left alone.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (activity.Report is not { } report || !Say(report.Doing).StartsWith("Waiting for its life to regenerate", StringComparison.Ordinal))
        {
            Assert.True(DateTime.UtcNow < deadline, $"The activity still says {(activity.Report is { } last ? Say(last.Doing) : "nothing")}.");
            _play.Time.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.DoesNotContain(connection.Sent, message => message is ObjectUseRequest { Uid: 52 } or ObjectUseMultipleRequest { Uid: 52 });

        run.Stop();
        await run.Completion;
    }
}
