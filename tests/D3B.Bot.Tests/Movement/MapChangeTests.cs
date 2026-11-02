using D3B.Bot.Movement;
using D3B.Engine.Maps;
using D3B.Bot.Tests.Runtime;
using D3B.Data.World;
using D3B.Engine.World;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Movement;

public sealed class MapChangeTests
{
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
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 461, 433 } });
        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());
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

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 405 } });
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
}
