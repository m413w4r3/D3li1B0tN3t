using D3B.Bot.Characters;
using D3B.Bot.Hosting;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Network;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Bot.Tests.Movement;

public sealed class SupervisedMoveTests
{
    private static SupervisedMoveSession Session(TestPlay play)
    {
        return new SupervisedMoveSession(play.Bots);
    }

    [Fact]
    public async Task ReadinessIsPassiveAndReportsAPlacedCharacter()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);

        var readiness = Session(play).Readiness();

        Assert.True(readiness.Connected);
        Assert.True(readiness.SessionAttached);
        Assert.False(readiness.TranslationBlocked);
        Assert.Equal(TestPlay.MapId, readiness.MapId);
        Assert.Equal(20, readiness.Cell);
        Assert.Equal(CellProvenance.ServerPlaced, readiness.Provenance);
        Assert.True(readiness.PositionTrusted);
        Assert.Equal(MovementRefusal.None, readiness.Guard);
        Assert.True(readiness.CanMove);
        Assert.Equal("READY", readiness.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AConnectionAloneMovesNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);

        await bot.ProcessedAsync();

        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task TheConsoleDoesNothingWithoutACommand()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);
        var console = new SupervisedMoveConsole(Session(play), NullLoggerFactory.Instance);
        var output = new StringWriter();

        await console.RunAsync(new StringReader(string.Empty), output, TestContext.Current.CancellationToken);

        Assert.Empty(connection.Sent);
        Assert.Contains("P2J supervised move enabled", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReadinessCommandSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);
        var console = new SupervisedMoveConsole(Session(play), NullLoggerFactory.Instance);
        var output = new StringWriter();

        await console.RunAsync(new StringReader("p2j readiness\n"), output, TestContext.Current.CancellationToken);

        Assert.Empty(connection.Sent);
        Assert.Contains("P2J READY", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("reason=READY", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task APlanCheckDoesNotConsumeTheSingleMove()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var session = Session(play);

        var plan = session.Check(TestPlay.MapId, TestPlay.NorthMapId);

        Assert.True(plan.Ready);
        Assert.Equal("PLAN_READY", plan.Reason);
        Assert.NotNull(plan.ExitCell);
        Assert.NotNull(plan.Rules);
        Assert.Empty(plan.Rules.KnownZaaps);
        Assert.Equal(bot.World.NpcWays.Count, plan.Rules.NoNpcWays.Count);
        Assert.Empty(connection.Sent);

        var move = session.MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        Assert.Equal(SupervisedMoveOutcome.Pass, (await TestWalks.AdvanceUntilDoneAsync(play.Time, move)).Outcome);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public void ALastWalkEndThatIsTheOnlyExitIsRefused()
    {
        var state = new CharacterState
        {
            Key = TestPlay.Key,
            Name = "Character",
            MapId = TestPlay.MapId,
            Cell = 433,
            LastWalkEnd = 20,
            Provenance = CellProvenance.ServerPlaced,
        };

        Assert.True(SupervisedMove.HasOnlyLastWalkEndExit(state, [20]));
        Assert.False(SupervisedMove.HasOnlyLastWalkEndExit(state, [20, 30]));
        Assert.False(SupervisedMove.HasOnlyLastWalkEndExit(state with { Cell = 20 }, [20]));
        Assert.False(SupervisedMove.HasOnlyLastWalkEndExit(state with { LastWalkEnd = null }, [20]));
    }

    [Fact]
    public async Task AMapActionOnTheWalkPathIsRefused()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var session = Session(play);
        var found = false;

        // The test map has map actions at cells 200 and 409. Find a placed cell whose shortest walk to the scroll exit
        // crosses one of them, so the planned approach itself would trigger an unrequested map action.
        for (var cell = 0; cell < 560 && !found; cell++)
        {
            connection.Server(TestPlay.Information(cell));
            await bot.ProcessedAsync();

            found = session.Check(TestPlay.MapId, TestPlay.NorthMapId).Reason is "MAP_ACTION_ON_PATH";
        }

        Assert.True(found, "No placement made the scroll approach cross a map-action cell.");

        var result = await session.MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, result.Outcome);
        Assert.Equal("MAP_ACTION_ON_PATH", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task UnknownPositionBlocksTheMove()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);

        // The same map repeated clears the cell until a new placement.
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        await bot.ProcessedAsync();

        var result = await Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, result.Outcome);
        Assert.Equal("GUARD_CELL_UNKNOWN", result.Reason);
        Assert.Equal(MovementRefusal.CellUnknown, Session(play).Readiness().Guard);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AHeldWalkBlocksTheMove()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 433, 20, out var path));

        var walk = bot.WalkAsync(20, TestContext.Current.CancellationToken);

        await TestWalks.SentAsync(play, connection, 1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });

        Assert.Equal(WalkOutcome.NotConfirmed, await TestWalks.AdvanceUntilDoneAsync(play.Time, walk));
        Assert.True(bot.State.IsWalkHeld);
        Assert.Equal(CellProvenance.Estimated, bot.State.Provenance);

        var result = await Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, result.Outcome);
        Assert.Equal("GUARD_WALK_HELD", result.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AFightBlocksTheMove()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);

        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await bot.ProcessedAsync();

        var result = await Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, result.Outcome);
        Assert.Equal("GUARD_FIGHTING", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ADisconnectionBlocksTheMove()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);

        connection.Close();
        await bot.ProcessedAsync();

        var result = await SupervisedMove.ExecuteAsync(bot, TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, result.Outcome);
        Assert.Equal("DISCONNECTED", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ATranslationFailureAfterPlacementBlocksTheMove()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);

        connection.ServerRaw(TestGame.UnknownCarriedEvent());
        await bot.ProcessedAsync();

        var readiness = Session(play).Readiness();

        Assert.True(readiness.TranslationBlocked);
        Assert.False(readiness.TranslationBlockedAtFirstPlacement);
        Assert.Equal(MovementRefusal.None, readiness.Guard);
        Assert.False(readiness.CanMove);
        Assert.Equal("TRANSLATION_LATCH_CLOSED", readiness.Reason);

        var result = await Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, result.Outcome);
        Assert.Equal("TRANSLATION_LATCH_CLOSED", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ATranslationFailureBeforePlacementIsReported()
    {
        await using var play = new TestPlay();
        var connection = play.Connect(TestPlay.Account);

        connection.Server(TestPlay.Chosen());
        connection.ServerRaw(TestGame.UnknownCarriedEvent());
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(TestPlay.Information(20));

        Assert.True(play.Bots.TryGet(TestPlay.Key, out var bot));
        await bot.ProcessedAsync();
        play.Time.Advance(BotDelays.Measured.Display);

        var readiness = Session(play).Readiness();

        Assert.True(readiness.TranslationBlocked);
        Assert.True(readiness.TranslationBlockedAtFirstPlacement);
        Assert.True(readiness.PositionTrusted);
        Assert.Equal(MovementRefusal.None, readiness.Guard);
        Assert.False(readiness.CanMove);
        Assert.Equal("TRANSLATION_LATCH_CLOSED", readiness.Reason);

        var result = await Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, result.Outcome);
        Assert.Equal("TRANSLATION_LATCH_CLOSED", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ANonNeighbourDestinationIsBlocked()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20, TestPlay.NorthMapId);

        var result = await Session(play).MoveAsync(TestPlay.NorthMapId, TestPlay.EastMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, result.Outcome);
        Assert.Equal("TARGET_NOT_NEIGHBOUR", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ADoorDestinationIsForbidden()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);

        var result = await Session(play).MoveAsync(TestPlay.MapId, TestPlay.InsideMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, result.Outcome);
        Assert.Equal("FORBIDDEN_TRANSITION", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AMapActionDestinationIsForbidden()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);

        var result = await Session(play).MoveAsync(TestPlay.MapId, TestPlay.EastMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, result.Outcome);
        Assert.Equal("FORBIDDEN_TRANSITION", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AHavenBagIsRefused()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(324);

        var information = TestPlay.Information(324, TestPlay.HavenBagMapId);
        information.HavenBag = new HavenBagInformation { Owner = new Character { Id = TestPlay.CharacterId } };

        connection.Server(new MapCurrentEvent { MapId = TestPlay.HavenBagMapId });
        connection.Server(information);
        await bot.ProcessedAsync();
        play.Time.Advance(BotDelays.Measured.Display);

        var result = await Session(play).MoveAsync(TestPlay.HavenBagMapId, TestPlay.MapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, result.Outcome);
        Assert.Equal("HAVEN_BAG", result.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AWalkRefusalFailsWithoutAMapChangeRequest()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(433);
        var move = Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        var request = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 1));
        var point = MapGeometry.PointOf(request.KeyMovements[0] & 0xFFF);

        connection.Server(new MapMovementRefusedEvent { X = point.X, Y = point.Y });

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, move);

        Assert.Equal(SupervisedMoveOutcome.Failed, result.Outcome);
        Assert.Equal("WALK_NOT_COMPLETED", result.Reason);
        Assert.Single(connection.Sent);
        Assert.DoesNotContain(connection.Sent, sent => sent is MapChangeRequest);
    }

    [Fact]
    public async Task ABlockedFirstMoveStillConsumesTheAuthorization()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);

        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        await bot.ProcessedAsync();

        var session = Session(play);
        var first = await session.MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, first.Outcome);
        Assert.Equal("GUARD_CELL_UNKNOWN", first.Reason);

        var second = await session.MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, second.Outcome);
        Assert.Equal("ALREADY_CONSUMED", second.Reason);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task TwoConcurrentMoveRequestsLaunchOnlyOne()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);
        var session = Session(play);

        var first = session.MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);
        var second = await session.MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, second.Outcome);
        Assert.Equal("ALREADY_CONSUMED", second.Reason);

        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        Assert.Equal(SupervisedMoveOutcome.Pass, (await TestWalks.AdvanceUntilDoneAsync(play.Time, first)).Outcome);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ASingleScrollMovesOnceAndConfirmsTheTarget()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);
        var session = Session(play);
        var move = session.MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        var request = Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));

        Assert.Equal((int)TestPlay.NorthMapId, request.MapId);

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, move);

        Assert.Equal(SupervisedMoveOutcome.Pass, result.Outcome);
        Assert.Equal("PASS", result.Reason);
        Assert.Equal(TestPlay.NorthMapId, result.Evidence.EndMapId);
        Assert.Equal(552, result.Evidence.EndCell);
        Assert.Equal(CellProvenance.ServerPlaced, result.Evidence.EndProvenance);
        Assert.True(result.Evidence.EndPositionTrusted);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AWalkToTheEdgeThenOneMapChangeIsTheMostSent()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(433);
        var move = Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        await TestWalks.WalkedAsync(play, connection, 1);
        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 2));
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 538);

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, move);

        Assert.Equal(SupervisedMoveOutcome.Pass, result.Outcome);
        Assert.Equal(2, connection.Sent.Count);
        Assert.Single(connection.Sent.OfType<MapMovementRequest>());
        Assert.Single(connection.Sent.OfType<MapChangeRequest>());
    }

    [Fact]
    public async Task AnUnexpectedDestinationFailsWithoutRetry()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var move = Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));

        connection.Server(new MapCurrentEvent { MapId = TestPlay.EastMapId });
        connection.Client(new MapInformationRequest { MapId = TestPlay.EastMapId });
        connection.Server(TestPlay.Information(300, TestPlay.EastMapId));

        var result = await TestWalks.AdvanceUntilDoneAsync(play.Time, move);

        Assert.Equal(SupervisedMoveOutcome.Failed, result.Outcome);
        Assert.Equal("UNEXPECTED_MAP", result.Reason);
        Assert.Equal(TestPlay.EastMapId, result.Evidence.EndMapId);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ASecondMoveIsRefusedAfterASuccess()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);
        var session = Session(play);
        var first = session.MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        Assert.Equal(SupervisedMoveOutcome.Pass, (await TestWalks.AdvanceUntilDoneAsync(play.Time, first)).Outcome);

        var second = await session.MoveAsync(TestPlay.NorthMapId, TestPlay.MapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, second.Outcome);
        Assert.Equal("ALREADY_CONSUMED", second.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ATimeoutFailsAndTheSecondMoveIsStillRefused()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);
        var session = Session(play);
        var first = session.MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));

        var failed = await TestWalks.AdvanceUntilDoneAsync(play.Time, first);

        Assert.Equal(SupervisedMoveOutcome.Failed, failed.Outcome);
        Assert.Equal("NO_ANSWER", failed.Reason);

        var second = await session.MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Blocked, second.Outcome);
        Assert.Equal("ALREADY_CONSUMED", second.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task CancellingTheMoveInterruptsWithoutRetry()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);
        using var cancellation = new CancellationTokenSource();
        var move = Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, cancellation.Token);

        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));
        cancellation.Cancel();

        var result = await move.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Interrupted, result.Outcome);
        Assert.Equal("CANCELLED", result.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ThePlayerActingInterruptsTheMove()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var move = Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));

        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });
        await bot.ProcessedAsync();

        var result = await move.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Interrupted, result.Outcome);
        Assert.Equal("INTERRUPTED_PLAYER", result.Reason);
        Assert.True(bot.IsPaused);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task TheConnectionClosingInterruptsTheMove()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);
        var move = Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));

        connection.Close();

        var result = await move.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Interrupted, result.Outcome);
        Assert.Equal("INTERRUPTED_DISCONNECTED", result.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AFightBeginningInterruptsTheMove()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);
        var move = Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));

        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });

        var result = await move.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Interrupted, result.Outcome);
        Assert.Equal("INTERRUPTED_FIGHT", result.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task StoppingTheBotInterruptsTheMove()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var move = Session(play).MoveAsync(TestPlay.MapId, TestPlay.NorthMapId, TestContext.Current.CancellationToken);

        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));

        await bot.DisposeAsync();

        var result = await move.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(SupervisedMoveOutcome.Interrupted, result.Outcome);
        Assert.Equal("INTERRUPTED_HOST_STOPPING", result.Reason);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task TheConsoleReportsASuccessAndRefusesTheSecondMove()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);
        var console = new SupervisedMoveConsole(Session(play), NullLoggerFactory.Instance);
        var output = new StringWriter();
        var input = new StringReader($"p2j move {TestPlay.MapId} {TestPlay.NorthMapId}\np2j move {TestPlay.MapId} {TestPlay.NorthMapId}\n");
        var running = console.RunAsync(input, output, TestContext.Current.CancellationToken);

        Assert.IsType<MapChangeRequest>(await TestWalks.SentAsync(play, connection, 1));
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        await running.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var text = output.ToString();

        Assert.Contains("P2J MOVE outcome=PASS reason=PASS", text, StringComparison.Ordinal);
        Assert.Contains("P2J MOVE outcome=BLOCKED reason=ALREADY_CONSUMED", text, StringComparison.Ordinal);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AnInvalidCommandSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(20);
        var console = new SupervisedMoveConsole(Session(play), NullLoggerFactory.Instance);
        var output = new StringWriter();

        await console.RunAsync(new StringReader("p2j move\np2j readiness nope\np2j elsewhere 1 2\n"), output, TestContext.Current.CancellationToken);

        Assert.Empty(connection.Sent);
        Assert.Contains("P2J COMMAND invalid", output.ToString(), StringComparison.Ordinal);
    }
}
