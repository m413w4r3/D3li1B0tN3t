using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Movement;

public sealed class WorldTravelTests
{
    private static async Task<(TestConnection Connection, BotActor Bot)> KnowingTheEastZaapAsync(TestPlay play)
    {
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId, TestPlay.SubscribedAccount);

        connection.Server(new ZaapKnownListEvent { MapIds = { TestPlay.EastMapId } });
        connection.Server(new KamasUpdateEvent { Kamas = 500 });
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    [Fact]
    public async Task AJourneyChangesMapsOneAfterAnotherUntilTheMapSought()
    {
        // From the north map: south by a scroll, then east by the map action.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        var journey = bot.TravelAsync(TestPlay.EastMapId, TestContext.Current.CancellationToken);

        Assert.True(await TestWalks.WalkedAsync(play, connection, 1) >= 532);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(play, connection, 2));
        TestWalks.Arrive(connection, TestPlay.MapId, 20);

        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(play, connection, 3, new MapCurrentEvent { MapId = TestPlay.EastMapId }));
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
        Assert.Equal(3, connection.Sent.Count);
    }

    [Fact]
    public async Task AJourneyStopsOnTheFirstMapOfTheWayWhereTheActivityHasWork()
    {
        // From the north map to the east one, through the middle map, where the activity has work.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        var asked = new List<long?>();
        var journey = bot.RunAsync(
            (context, token) => context.TravelAsync(
                new HashSet<long> { TestPlay.EastMapId },
                state =>
                {
                    asked.Add(state.MapId);

                    return state.MapId == TestPlay.MapId;
                },
                token),
            TestContext.Current.CancellationToken);

        Assert.True(await TestWalks.WalkedAsync(play, connection, 1) >= 532);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(play, connection, 2));
        TestWalks.Arrive(connection, TestPlay.MapId, 20);

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Equal(TestPlay.MapId, bot.State.MapId);
        Assert.Equal(2, connection.Sent.Count);
        Assert.Equal([TestPlay.MapId], asked);
    }

    [Fact]
    public async Task AJourneyGoesThroughADoor()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(287);
        var journey = bot.TravelAsync(TestPlay.InsideMapId, TestContext.Current.CancellationToken);

        Assert.IsType<InteractiveUseRequest>(await connection.SentAsync(1));
        TestWalks.Arrive(connection, TestPlay.InsideMapId, 396);

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ACharacterOnTheMapSoughtHasArrived()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        Assert.Equal(TravelOutcome.Arrived, await bot.TravelAsync(TestPlay.MapId, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AMapNoPathLeadsToSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        Assert.Equal(TravelOutcome.NoPath, await bot.TravelAsync(42, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AMapChangeThatFailsIsLeftOutAndAnotherWayTaken()
    {
        // North by the scroll first, which the server ignores; then by the map action.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var journey = bot.TravelAsync(TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<MapChangeRequest>(await connection.SentAsync(1));

        while (connection.Sent.Count < 2)
        {
            play.Time.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Equal(TestPlay.NorthActionCell, await TestWalks.WalkedAsync(play, connection, 2, new MapCurrentEvent { MapId = TestPlay.NorthMapId }));
        connection.Server(TestPlay.Information(552, TestPlay.NorthMapId));

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
    }

    [Fact]
    public async Task AJourneyWhoseOnlyWayFailsEndsWithNoPath()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(TestPlay.ActionCell + 1);
        var journey = bot.TravelAsync(TestPlay.EastMapId, TestContext.Current.CancellationToken);

        // The server never shows the walk onto the action cell: the change fails, and no other way leads east.
        Assert.Equal(TravelOutcome.NoPath, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ACharacterThatWalksIsBusy()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 405 } });
        await bot.ProcessedAsync();

        Assert.Equal(TravelOutcome.Busy, await bot.TravelAsync(TestPlay.NorthMapId, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public void TheFactsOfACharacterAreItsStateAtAMoment()
    {
        var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var state = new CharacterState
        {
            Key = TestPlay.Key,
            Name = "Character",
            Level = 50,
            MapId = TestPlay.MapId,
            SubAreaId = TestPlay.SubAreaId,
            Cell = 300,
            Inventory = ImmutableSortedDictionary.CreateRange(new Dictionary<int, InventoryItem>
            {
                [1] = new(1, 289, 10, 63),
                [2] = new(2, 289, 5, 63),
                [3] = new(3, 1782, 1, 63),
            }),
            Jobs = ImmutableSortedDictionary.CreateRange(new Dictionary<int, CharacterJob> { [28] = new(28, 12, 0, 0, 0, []) }),
            SubscriptionEnd = now.AddDays(2),
        };

        var facts = WorldTravel.FactsOf(state, now);

        Assert.Equal(50, facts.Level);
        Assert.Equal(15, facts.Items![289]);
        Assert.Equal(1, facts.Items[1782]);
        Assert.Equal(12, facts.JobLevels![28]);
        Assert.True(facts.Subscribed);
        Assert.False(WorldTravel.FactsOf(state, now.AddDays(3)).Subscribed);
        Assert.False(WorldTravel.FactsOf(state with { SubscriptionEnd = null }, now).Subscribed);
    }

    [Fact]
    public async Task AJourneyTeleportsToAZaapTheCharacterKnows()
    {
        // From the north map, the east map is two changes away on foot, one by teleporting to its zaap.
        await using var play = new TestPlay();
        var (connection, bot) = await KnowingTheEastZaapAsync(play);
        var journey = bot.TravelAsync(TestPlay.EastMapId, TestContext.Current.CancellationToken);

        Assert.Equal(new TeleportRequest { MapId = TestPlay.EastMapId, Source = TeleporterType.Map }, await connection.SentAsync(1));
        connection.Server(new MapCurrentEvent { MapId = TestPlay.EastMapId });
        connection.Server(new KamasUpdateEvent { Kamas = 490 });
        connection.Server(new DialogLeaveEvent { Type = DialogType.Teleport });
        connection.Client(new MapInformationRequest { MapId = TestPlay.EastMapId });
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AnActivitysJourneyTakesAZaapOnlyWhenItSavesAsManyChangesAsTheActivitySays()
    {
        // The east map's zaap saves one change of two: an activity asking two walks, south by the scroll first.
        await using var play = new TestPlay();
        var (connection, bot) = await KnowingTheEastZaapAsync(play);
        var run = bot.StartActivity(new JourneyActivity(TestPlay.EastMapId, 2));

        Assert.True(await TestWalks.WalkedAsync(play, connection, 1) >= 532);
        Assert.DoesNotContain(connection.Sent, request => request is TeleportRequest);

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task ARefusedTeleportLeavesTeleportsOutOfTheJourney()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await KnowingTheEastZaapAsync(play);
        var journey = bot.TravelAsync(TestPlay.EastMapId, TestContext.Current.CancellationToken);

        Assert.IsType<TeleportRequest>(await connection.SentAsync(1));
        connection.Server(new TextInformationEvent { TextId = 416, Type = TextInformationType.Error });

        // On foot: south by the scroll, then east by the map action.
        Assert.True(await TestWalks.WalkedAsync(play, connection, 2) >= 532);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(play, connection, 3));
        TestWalks.Arrive(connection, TestPlay.MapId, 20);

        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(play, connection, 4, new MapCurrentEvent { MapId = TestPlay.EastMapId }));
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Single(connection.Sent, request => request is TeleportRequest);

        // The next journeys teleport from that map no more (D248).
        Assert.Equal([TestPlay.NorthMapId], bot.NoTeleportFrom);
    }

    [Fact]
    public async Task AJourneyFromTheHavenBagLeavesItFirst()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        var information = TestPlay.Information(324, TestPlay.HavenBagMapId);

        information.HavenBag = new HavenBagInformation { Owner = new Character { Id = TestPlay.CharacterId } };
        connection.Server(new MapCurrentEvent { MapId = TestPlay.HavenBagMapId });
        connection.Server(information);
        await bot.ProcessedAsync();

        var journey = bot.TravelAsync(TestPlay.MapId, TestContext.Current.CancellationToken);

        Assert.Equal(new HavenBagEnterRequest { OwnerId = TestPlay.CharacterId }, await TestWalks.SentAsync(play, connection, 1));
        TestWalks.Arrive(connection, TestPlay.MapId, 300);

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.False(bot.State.IsInHavenBag);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AJourneyGoesThroughAnNpcsDialog()
    {
        // As Incarnam's portal (D253): from the inside, only the NPC's dialog leads north, a question then another.
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);
        var journey = bot.TravelAsync(TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(new NpcActionRequest { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId }, await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 30_637, Replies = { new NpcReply { ReplyId = TestPlay.WayReplyId } } });

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.WayReplyId }, await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new NpcDialogQuestionEvent
        {
            MessageId = 30_638,
            Replies = { new NpcReply { ReplyId = TestPlay.WayLastReplyId }, new NpcReply { ReplyId = TestPlay.StayReplyId } },
        });

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.WayLastReplyId }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 300);

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Equal(3, connection.Sent.Count);
        Assert.Empty(bot.NoNpcWays);

        var change = Assert.Single(bot.Journal.All().OfType<MapChangeEntry>());

        Assert.Equal((TestPlay.InsideMapId, (long?)TestPlay.NorthMapId, MapChangeKind.Npc, MapChangeOutcome.Changed), (change.From, change.Reached, change.Kind, change.Outcome));
    }

    [Fact]
    public async Task AnNpcsDialogOfferingNoneOfItsWaysRepliesIsLeftAndTheWayRefusedForTheBotsLife()
    {
        // As a portal asking to talk to someone else first: the dialog is left, and no journey asks the NPC again.
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);
        var journey = bot.TravelAsync(TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 30_636, Replies = { new NpcReply { ReplyId = TestPlay.StayReplyId } } });

        Assert.IsType<DialogLeaveRequest>(await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });

        Assert.Equal(TravelOutcome.NoPath, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Equal([0], bot.NoNpcWays);
        Assert.Equal(TravelOutcome.NoPath, await bot.TravelAsync(TestPlay.NorthMapId, TestContext.Current.CancellationToken));
        Assert.Equal(2, connection.Sent.Count);
    }

    [Fact]
    public async Task AnNpcTheServerDidNotPlaceIsNotTalkedTo()
    {
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);
        var information = TestPlay.Information(300, TestPlay.InsideMapId);

        information.Actors.Remove(information.Actors.Single(actor => actor.Id == TestPlay.WayActorId));
        connection.Server(information);
        await bot.ProcessedAsync();

        Assert.Equal(TravelOutcome.NoPath, await bot.TravelAsync(TestPlay.NorthMapId, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
        Assert.Empty(bot.NoNpcWays);
    }

    [Fact]
    public async Task AnNpcWhoMovesTheCharacterAtOnceIsFollowedAndTheQuestionWithoutReplyLeftToTheClient()
    {
        // As Incarnam's portal for a character of level 92 (D253): the move as it is talked to, then on the new map a
        // question offering no reply, which the client closes by itself; the bot goes on, not paused.
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);
        var journey = bot.TravelAsync(TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.NorthMapId, NpcActorId = -20_005 });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 30_636 });
        connection.Client(new DialogLeaveRequest());
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Client(new MapInformationRequest { MapId = TestPlay.NorthMapId });
        connection.Server(TestPlay.Information(300, TestPlay.NorthMapId));

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.False(bot.IsPaused);
        Assert.Single(connection.Sent);
        Assert.Empty(bot.NoNpcWays);
    }

    [Fact]
    public async Task AQuestionWithoutReplyTheClientLeavesOpenOnTheNewMapIsLeft()
    {
        await using var play = TestPlay.ThroughAnNpc();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.InsideMapId);
        var journey = bot.TravelAsync(TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<NpcActionRequest>(await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.NorthMapId, NpcActorId = -20_005 });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 30_636 });
        connection.Client(new MapInformationRequest { MapId = TestPlay.NorthMapId });
        connection.Server(TestPlay.Information(300, TestPlay.NorthMapId));

        Assert.IsType<DialogLeaveRequest>(await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.False(bot.State.IsInDialog);
    }
}
