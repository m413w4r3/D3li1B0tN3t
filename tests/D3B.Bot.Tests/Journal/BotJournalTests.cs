using D3B.Bot.Activities;
using D3B.Bot.Interactives;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Journal;

public sealed class BotJournalTests
{
    private static IReadOnlyList<JournalEntry> AfterConnecting(BotActor bot)
    {
        var entries = bot.Journal.All();

        Assert.Equal(BotEvent.Connected, Assert.IsType<BotEventEntry>(entries[0]).Event);

        return [.. entries.Skip(1)];
    }

    [Fact]
    public async Task AWalkAskedForItselfIsWrittenOnceOverWithHowItEndedAndHowLongItTook()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        play.Time.Advance(TimeSpan.FromMilliseconds(900));
        await TestWalks.WalkedAsync(play, connection, 1);

        Assert.Equal(WalkOutcome.Arrived, await walk);

        var entry = Assert.IsType<WalkEntry>(Assert.Single(AfterConnecting(bot)));

        Assert.Equal((0, TestPlay.MapId, 388, WalkOutcome.Arrived), (entry.Depth, entry.MapId, entry.Cell, entry.Outcome));
        Assert.Equal(TimeSpan.FromMilliseconds(900), entry.Duration);
        Assert.Equal(play.Time.GetUtcNow(), entry.At);
    }

    [Fact]
    public async Task AJourneysMapChangesLieWithinItAndTheWalksTheyMakeHaveNoEntry()
    {
        // From the north map: south by a scroll, then east by the map action.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        var journey = bot.TravelAsync(TestPlay.EastMapId, TestContext.Current.CancellationToken);

        await TestWalks.WalkedAsync(play, connection, 1);
        await TestWalks.SentAsync(play, connection, 2);
        TestWalks.Arrive(connection, TestPlay.MapId, 20);
        await TestWalks.WalkedAsync(play, connection, 3, new MapCurrentEvent { MapId = TestPlay.EastMapId });
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));

        var entries = AfterConnecting(bot);
        var scroll = Assert.IsType<MapChangeEntry>(entries[0]);
        var action = Assert.IsType<MapChangeEntry>(entries[1]);
        var travel = Assert.IsType<TravelEntry>(entries[2]);

        Assert.Equal(3, entries.Count);
        Assert.Equal((1, TestPlay.NorthMapId, (long?)TestPlay.MapId, MapChangeKind.Scroll, MapChangeOutcome.Changed), (scroll.Depth, scroll.From, scroll.To, scroll.Kind, scroll.Outcome));
        Assert.Equal((1, TestPlay.MapId, (long?)TestPlay.EastMapId, MapChangeKind.MapAction, MapChangeOutcome.Changed), (action.Depth, action.From, action.To, action.Kind, action.Outcome));
        Assert.Equal((0, (long?)TestPlay.NorthMapId, (long?)TestPlay.EastMapId, (long?)TestPlay.EastMapId, 2, TravelOutcome.Arrived), (travel.Depth, travel.From, travel.Sought, travel.Reached, travel.Changes, travel.Outcome));
    }

    [Fact]
    public async Task AUseIsWrittenWithWhatTheElementIs()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(TestPlay.TreeCell + 14);
        var use = bot.UseAsync(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestContext.Current.CancellationToken);

        Assert.IsType<InteractiveUseRequest>(await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.TreeElementId });

        var outcome = await TestWalks.AdvanceUntilDoneAsync(play.Time, use);
        var entry = Assert.IsType<UseEntry>(Assert.Single(AfterConnecting(bot)));

        Assert.Equal(UseOutcome.Refused, outcome);
        Assert.Equal((0, TestPlay.TreeElementId, (int?)34, TestPlay.TreeSkillId, UseOutcome.Refused), (entry.Depth, entry.ElementId, entry.InteractiveId, entry.SkillId, entry.Outcome));
    }

    [Fact]
    public async Task ThePlayersPauseIsWrittenOnceWhateverItsRequestsThenTheResuming()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Client(new MapMovementCancelRequest());
        connection.Client(new MapMovementRequest { MapId = TestPlay.MapId, KeyMovements = { 433, 432 } });

        await Assert.ThrowsAsync<BotInterruptedException>(() => walk);

        bot.Resume();
        await bot.ProcessedAsync();

        Assert.Equal([BotEvent.PausedByPlayer, BotEvent.Resumed], AfterConnecting(bot).Select(entry => Assert.IsType<BotEventEntry>(entry).Event));
    }

    [Fact]
    public async Task AnActivitysLifeIsWrittenFromItsStartToHowItEnded()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Client(new MapMovementRequest { MapId = TestPlay.MapId, KeyMovements = { 300, 301 } });
        await bot.ProcessedAsync();
        bot.Resume();
        await activity.SteppingAsync(bot);
        connection.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await run.Completion);

        var entries = AfterConnecting(bot);

        Assert.Equal(activity.Name, Assert.IsType<ActivityStartedEntry>(entries[0]).Activity);
        Assert.Equal(BotEvent.PausedByPlayer, Assert.IsType<BotEventEntry>(entries[1]).Event);
        Assert.Contains(entries, entry => entry is ActivityInterruptedEntry { Reason: Interruption.Player, Response: InterruptionResponse.Resume });
        Assert.Contains(entries, entry => entry is BotEventEntry { Event: BotEvent.Resumed });

        var over = Assert.IsType<ActivityOverEntry>(entries[^1]);

        Assert.Equal((1, ActivityOutcome.Done, (string?)null, false), (over.Steps, over.Outcome, over.Error, over.StillFull));
    }

    [Fact]
    public async Task AnActivityThatFailsIsWrittenWithItsError()
    {
        await using var play = new TestPlay();
        var (_, bot) = await play.EnterAsync(300);
        var run = bot.StartActivity(new TestActivity { Throws = true });

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal("The step failed.", Assert.IsType<ActivityOverEntry>(AfterConnecting(bot)[^1]).Error);
    }

    [Fact]
    public async Task AFightAndTheConnectionClosingAreWritten()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Close();
        await bot.ProcessedAsync();

        Assert.Equal([BotEvent.FightBegan, BotEvent.Disconnected], AfterConnecting(bot).Select(entry => Assert.IsType<BotEventEntry>(entry).Event));
    }

    [Fact]
    public async Task AFightsEndIsWrittenOnceWithItsOutcomeAndGains()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        var won = new FightResult
        {
            Outcome = FightOutcome.Won,
            Fighter = new FightResultFighter
            {
                FighterId = TestPlay.CharacterId,
                Character = new FightResultCharacter { Data = { new FightResultData { Experience = new FightExperience { Gained = 77 } } } },
            },
            Loot = new FightLoot { Kamas = 7, Objects = { new FightLootObjects { Objects = { new FightLootObject { ItemId = 1_984, Quantity = 2 } } } } },
        };

        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(new FightEndEvent { Results = { won } });
        connection.Server(new FightEndEvent { Results = { won } });
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        await bot.ProcessedAsync();

        var end = Assert.IsType<FightEndEntry>(Assert.Single(AfterConnecting(bot), entry => entry is FightEndEntry));

        Assert.Equal((FightOutcome.Won, 77, 7, 2), (end.Outcome, end.Experience, end.Kamas, end.Objects));
    }
}
