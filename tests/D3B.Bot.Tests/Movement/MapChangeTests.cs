using D3B.Bot.Characters;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Engine.Maps;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.World;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Movement;

public sealed class MapChangeTests
{
    private static async Task LeaveHeldWalkAsync(TestPlay play, TestConnection connection, BotActor bot, int destination)
    {
        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, bot.State.Cell!.Value, destination, out var path));
        var walk = bot.WalkAsync(destination, TestContext.Current.CancellationToken);

        await TestWalks.SentAsync(play, connection, connection.Sent.Count + 1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });

        Assert.Equal(WalkOutcome.NotConfirmed, await TestWalks.AdvanceUntilDoneAsync(play.Time, walk));
        Assert.True(bot.State.IsWalkHeld);
    }

    [Fact]
    public async Task AScrollWalksToTheNearestCellOfItsEdgeThenAsksForTheNeighbour()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        // From cell 433, east edge, row 30: straight north to row 1, two rows a step.
        Assert.Equal(27, await TestWalks.WalkedAsync(play, connection, 1));
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await connection.SentAsync(2));

        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        await bot.ProcessedAsync();
        Assert.False(change.IsCompleted);

        connection.Client(new MapInformationRequest { MapId = TestPlay.NorthMapId });
        connection.Server(TestPlay.Information(538, TestPlay.NorthMapId));

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        await bot.ProcessedAsync();
        Assert.Equal(TestPlay.NorthMapId, bot.State.MapId);
        Assert.Equal(538, bot.State.Cell);
        Assert.Equal(2, connection.Sent.Count);
    }

    [Fact]
    public async Task AScrollLeavesByAnotherCellOfItsEdgeThanTheOneThePreviousWalkEndedOn()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        // The previous walk ended on cell 27, the edge's nearest from 433, as on the map before of a journey straight north.
        connection.Client(new MapMovementRequest { MapId = (int)TestPlay.MapId });
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 461, 433 } });
        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());
        connection.Client(new MapMovementRequest { MapId = (int)TestPlay.MapId });
        await bot.ProcessedAsync();
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 27 } });
        await bot.ProcessedAsync();
        TestWalks.Arrive(connection, TestPlay.MapId, 433);
        await bot.ProcessedAsync();

        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);
        var exit = await TestWalks.WalkedAsync(play, connection, 1);

        Assert.NotEqual(27, exit);
        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await connection.SentAsync(2));

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 538);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task AWalkToTheEdgeThatSetsOffAMapActionAsksForNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await TestWalks.WalkedAsync(play, connection, 1, new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        connection.Server(TestPlay.Information(538, TestPlay.NorthMapId));

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AMapChangeIsOverOnceTheCharacterIsPlaced()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        Assert.Equal(MapChangeOutcome.Changed, await change);
    }

    [Fact]
    public async Task ACharacterOnACellOfTheEdgeAsksAtOnce()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, await connection.SentAsync(1));

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task ACharacterOnACellOfTheEdgeTheServerStillHoldsWalkingStepsAsideFirst()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(48);

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, 48, 20, out var path));

        var walk = bot.WalkAsync(20, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        Assert.Equal(WalkOutcome.NotConfirmed, await TestWalks.AdvanceUntilDoneAsync(play.Time, walk));

        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);
        var aside = Assert.IsType<MapMovementRequest>(await connection.SentAsync(2));

        Assert.Equal(20, aside.KeyMovements[0] & 0xFFF);

        await TestWalks.WalkedAsync(play, connection, 2);

        // From the cell it stepped to, the nearest of the edge's, walked to unless it is one.
        var request = await TestWalks.SentAsync(play, connection, 3);

        if (request is MapMovementRequest)
        {
            await TestWalks.WalkedAsync(play, connection, 3);
            request = await TestWalks.SentAsync(play, connection, 4);
        }

        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.NorthMapId }, request);

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task AFailedHeldWalkReleaseBlocksMapChangeAndDoesNotRetry()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(48);

        await LeaveHeldWalkAsync(play, connection, bot, 20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);
        var aside = Assert.IsType<MapMovementRequest>(await connection.SentAsync(2));
        var heldCell = aside.KeyMovements[0] & 0xFFF;
        var point = MapGeometry.PointOf(heldCell);

        connection.Server(new MapMovementRefusedEvent { X = point.X, Y = point.Y });

        Assert.Equal(MapChangeOutcome.Busy, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Equal(2, connection.Sent.Count);
        Assert.All(connection.Sent, request => Assert.IsType<MapMovementRequest>(request));
        Assert.Contains(play.Log.GetSnapshot(), record => record.Message.Contains("held-walk release failed", StringComparison.Ordinal)
            && record.Message.Contains("outcome Refused", StringComparison.Ordinal)
            && record.Message.Contains("resulting provenance ServerRefused", StringComparison.Ordinal)
            && record.Message.Contains("no further movement follows", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AHeldCharacterWithAStaleStepSourceDoesNotSendAReleaseWalk()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(48);
        await LeaveHeldWalkAsync(play, connection, bot, 20);

        var step = play.StepTo(bot, TestPlay.NorthMapId);
        var vertices = play.World.Graph.Vertices.ToArray();
        var wrongFrom = Array.FindIndex(vertices, vertex => vertex.MapId == TestPlay.NorthMapId);
        Assert.True(wrongFrom >= 0);
        Assert.NotEqual(step.From, wrongFrom);

        var sentBefore = connection.Sent.Count;
        var change = bot.ChangeMapAsync(step with { From = wrongFrom }, TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!change.IsCompleted && connection.Sent.Count == sentBefore && DateTime.UtcNow < deadline)
            await Task.Delay(5, TestContext.Current.CancellationToken);

        if (connection.Sent.Count > sentBefore)
            await TestWalks.WalkedAsync(play, connection, sentBefore + 1);

        Assert.Equal(MapChangeOutcome.NoPath, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Equal(sentBefore, connection.Sent.Count);
    }

    [Fact]
    public async Task AnArrivedReleaseWithoutAProvenPositionBlocksMapChange()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(48);

        await LeaveHeldWalkAsync(play, connection, bot, 20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);
        var aside = Assert.IsType<MapMovementRequest>(await connection.SentAsync(2));
        var from = aside.KeyMovements[0] & 0xFFF;
        var to = aside.KeyMovements[^1] & 0xFFF;

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(TestPlay.MapId).Cells.Span, from, to, out var asidePath));
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { asidePath.Cells.ToArray() } });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Response(new MapMovementConfirmResponse());

        Assert.Equal(MapChangeOutcome.Busy, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Null(bot.State.Cell);
        Assert.False(bot.State.IsPositionTrusted);
        Assert.Equal(2, connection.Sent.Count);
        Assert.All(connection.Sent, request => Assert.IsType<MapMovementRequest>(request));
    }

    [Fact]
    public async Task AFailedHeldWalkReleaseBlocksLeavingTheHavenBag()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var information = TestPlay.Information(433);
        information.HavenBag = new HavenBagInformation { Owner = new Character { Id = TestPlay.CharacterId } };
        connection.Server(information);
        await bot.ProcessedAsync();

        await LeaveHeldWalkAsync(play, connection, bot, 388);
        var leave = bot.RunAsync((context, token) => context.LeaveHavenBagAsync(token), TestContext.Current.CancellationToken);
        var aside = Assert.IsType<MapMovementRequest>(await connection.SentAsync(2));
        var heldCell = aside.KeyMovements[0] & 0xFFF;
        var point = MapGeometry.PointOf(heldCell);

        connection.Server(new MapMovementRefusedEvent { X = point.X, Y = point.Y });

        Assert.Equal(MapChangeOutcome.Busy, await TestWalks.AdvanceUntilDoneAsync(play.Time, leave));
        Assert.Equal(2, connection.Sent.Count);
        Assert.All(connection.Sent, request => Assert.IsType<MapMovementRequest>(request));
        Assert.DoesNotContain(connection.Sent, request => request is HavenBagEnterRequest);
    }

    [Fact]
    public async Task AMapActionWalksOntoItsCellAndTheServerNamesTheNewMapBeforeAnsweringTheConfirmation()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.EastMapId), TestContext.Current.CancellationToken);

        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(play, connection, 1, new MapCurrentEvent { MapId = TestPlay.EastMapId }));

        connection.Client(new MapInformationRequest { MapId = TestPlay.EastMapId });
        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        await bot.ProcessedAsync();
        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
        Assert.Equal(253, bot.State.Cell);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ACharacterOnAMapActionsCellStepsOffThenBack()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(TestPlay.ActionCell);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.EastMapId), TestContext.Current.CancellationToken);

        var off = await TestWalks.WalkedAsync(play, connection, 1);

        Assert.NotEqual(TestPlay.ActionCell, off);
        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(play, connection, 2, new MapCurrentEvent { MapId = TestPlay.EastMapId }));

        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task ACharacterTheServerSendsElsewhereIsElsewhere()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        TestWalks.Arrive(connection, TestPlay.EastMapId, 253);

        Assert.Equal(MapChangeOutcome.Elsewhere, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task AMapChangeTheServerDoesNotAnswerIsOverAfterItsDelay()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);

        Assert.Equal(MapChangeOutcome.NoAnswer, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Equal(TestPlay.MapId, bot.State.MapId);
    }

    [Fact]
    public async Task ANewMapWhoseInformationDoesNotComeLeavesTheCharacterNotPlaced()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });

        Assert.Equal(MapChangeOutcome.NotPlaced, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Null(bot.State.Cell);
        Assert.Equal(CellProvenance.Unknown, bot.State.Provenance);
        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(538, TestContext.Current.CancellationToken));
        Assert.Single(connection.Sent);

        connection.Server(TestPlay.Information(552, TestPlay.NorthMapId));
        await bot.ProcessedAsync();

        var neighbours = Enumerable.Range(0, MapCell.DirectionCount).Select(direction => MapGeometry.Neighbour(552, direction)).Where(cell => cell >= 0).ToArray();
        Assert.True(CellPathfinder.TryFindNearest(play.Maps.Get(TestPlay.NorthMapId).Cells.Span, 552, neighbours, out var path));
        var walk = bot.WalkAsync(path.End, TestContext.Current.CancellationToken);
        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 2));
        Assert.Equal(path.End, await TestWalks.WalkedAsync(play, connection, 2));
        Assert.Equal(WalkOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, walk));
    }

    [Fact]
    public async Task AnInformationOfTheOldMapPlacesNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(20);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.NorthMapId), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(TestPlay.Information(20));
        connection.Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        await bot.ProcessedAsync();

        Assert.False(change.IsCompleted);

        connection.Server(TestPlay.Information(552, TestPlay.NorthMapId));

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
    }

    [Fact]
    public async Task AStepThatDoesNotLeaveWhereTheCharacterStandsSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var south = new WorldTransition(WorldTransitionType.Scroll, 2, 539, -1, TestPlay.MapId, -1, -1);

        Assert.Equal(MapChangeOutcome.NoPath, await bot.ChangeMapAsync(new WorldStep(0, 1, 0, south), TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnEdgeTheCharacterCannotReachSendsNothing()
    {
        // Rows 0 to 3 blocked: no cell of the north edge can be walked on.
        await using var play = new TestPlay([.. Enumerable.Range(0, 56)]);
        var (connection, bot) = await play.EnterAsync(433);
        var step = new WorldStep(1, 0, 1, play.World.Graph.Transitions.Span[1]);

        Assert.Equal(MapChangeOutcome.NoPath, await bot.ChangeMapAsync(step, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACharacterThatWalksIsBusy()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var step = play.StepTo(bot, TestPlay.NorthMapId);

        connection.Client(new MapMovementRequest { MapId = (int)TestPlay.MapId });
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 405 } });
        await bot.ProcessedAsync();

        Assert.Equal(MapChangeOutcome.Busy, await bot.ChangeMapAsync(step, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ADisconnectedMapChangeIsBusyAndSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var step = play.StepTo(bot, TestPlay.NorthMapId);

        connection.Close();
        await bot.ProcessedAsync();

        Assert.Equal(MapChangeOutcome.Busy, await bot.ChangeMapAsync(step, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ADoorIsUsedFromNextToItsElementThenTheServerNamesTheNewMap()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.InsideMapId), TestContext.Current.CancellationToken);

        var cell = await TestWalks.WalkedAsync(play, connection, 1);

        Assert.NotEqual(-1, MapGeometry.Direction(cell, TestPlay.DoorCell));
        Assert.Equal(new InteractiveUseRequest { SkillInstanceId = TestPlay.DoorInstanceId, ElementId = TestPlay.DoorElementId }, await connection.SentAsync(2));

        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.DoorElementId, SkillId = 184 });
        TestWalks.Arrive(connection, TestPlay.InsideMapId, 396);

        Assert.Equal(MapChangeOutcome.Changed, await TestWalks.AdvanceUntilDoneAsync(play.Time, change));
        Assert.Equal(TestPlay.InsideMapId, bot.State.MapId);
        Assert.Equal(2, connection.Sent.Count);
    }

    [Fact]
    public async Task ADoorTheServerRefusesIsRefused()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(287);
        var change = bot.ChangeMapAsync(play.StepTo(bot, TestPlay.InsideMapId), TestContext.Current.CancellationToken);

        Assert.IsType<InteractiveUseRequest>(await connection.SentAsync(1));
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.DoorElementId, SkillInstanceId = 184 });

        Assert.Equal(MapChangeOutcome.Refused, await change);
        Assert.Equal(TestPlay.MapId, bot.State.MapId);
    }

    [Fact]
    public async Task ADoorWhoseSkillTheServerDoesNotOfferSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var step = play.StepTo(bot, TestPlay.InsideMapId);

        connection.Server(new InteractiveElementUpdatedEvent { Element = new InteractiveElement { ElementId = TestPlay.DoorElementId, InteractiveId = 316 } });
        await bot.ProcessedAsync();

        Assert.Equal(MapChangeOutcome.NoPath, await bot.ChangeMapAsync(step, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ARefusalWithoutPendingMovementBlocksHeldWalkRecovery()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(48);

        await LeaveHeldWalkAsync(play, connection, bot, 20);
        var point = MapGeometry.PointOf(bot.State.Cell!.Value);
        connection.Server(new MapMovementRefusedEvent { X = point.X, Y = point.Y });
        await bot.ProcessedAsync();

        Assert.True(bot.State.IsWalkHeld);
        Assert.True(bot.State.MovementAnswerAmbiguous);
        Assert.Equal(CellProvenance.Estimated, bot.State.Provenance);

        var secondFrameSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Relay.FrameSent += count =>
        {
            if (count == 2)
                secondFrameSent.TrySetResult();
        };

        var release = bot.RunAsync(
            (context, cancellationToken) => context.ReleaseHeldWalkAsync(cancellationToken),
            TestContext.Current.CancellationToken);
        var first = await Task.WhenAny(release, secondFrameSent.Task).WaitAsync(TestContext.Current.CancellationToken);

        if (first == secondFrameSent.Task)
            connection.Server(new MapMovementRefusedEvent { X = point.X, Y = point.Y });

        var outcome = await release.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(WalkOutcome.Busy, outcome);
        Assert.True(bot.State.MovementAnswerAmbiguous);
        Assert.Single(connection.Sent);
    }
}
