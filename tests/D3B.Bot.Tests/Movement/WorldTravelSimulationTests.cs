using D3B.Bot.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Movement;

public sealed class WorldTravelSimulationTests
{
    [Fact]
    public async Task AFirstJourneyFromAPlacedPositionReachesTheTargetOnAConfirmedCell()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        var journey = bot.TravelAsync(TestPlay.EastMapId, TestContext.Current.CancellationToken);

        var firstRequest = Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 1));
        var start = bot.State.Cell!.Value;
        var end = firstRequest.KeyMovements[^1] & 0xFFF;

        Assert.Equal(TestPlay.NorthMapId, firstRequest.MapId);
        Assert.True(CellPathfinder.TryFind(play.Maps.Get(firstRequest.MapId).Cells.Span, start, end, out var path));

        var announced = path.Cells.ToArray();

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { announced } });
        await bot.ProcessedAsync();

        Assert.Equal(TestPlay.NorthMapId, bot.State.MapId);
        Assert.Equal(start, bot.State.Cell);
        Assert.Equal(announced, bot.State.Path);
        Assert.True(bot.State.IsWalking);

        connection.Client(new MapMovementConfirmRequest());
        connection.Response(new MapMovementConfirmResponse());
        await bot.ProcessedAsync();

        Assert.Equal(end, bot.State.Cell);
        Assert.Empty(bot.State.Path);
        Assert.False(bot.State.IsWalking);

        Assert.Equal(new MapChangeRequest { MapId = (int)TestPlay.MapId }, await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        await bot.ProcessedAsync();

        Assert.Equal(TestPlay.MapId, bot.State.MapId);
        Assert.Null(bot.State.Cell);

        connection.Client(new MapInformationRequest { MapId = TestPlay.MapId });
        connection.Server(TestPlay.Information(20, TestPlay.MapId));
        await bot.ProcessedAsync();

        Assert.Equal(20, bot.State.Cell);

        Assert.Equal(TestPlay.ActionCell, await TestWalks.WalkedAsync(
            play,
            connection,
            3,
            new MapCurrentEvent { MapId = TestPlay.EastMapId }));
        await bot.ProcessedAsync();

        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
        Assert.Null(bot.State.Cell);

        connection.Client(new MapInformationRequest { MapId = TestPlay.EastMapId });
        connection.Server(TestPlay.Information(20, TestPlay.MapId));
        await bot.ProcessedAsync();

        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
        Assert.Null(bot.State.Cell);

        connection.Server(TestPlay.Information(253, TestPlay.EastMapId));
        await bot.ProcessedAsync();

        Assert.Equal(TravelOutcome.Arrived, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
        Assert.Equal(253, bot.State.Cell);
        Assert.Equal(3, connection.Sent.Count);
    }

    [Fact]
    public async Task ARefusedWalkLeavesTheJourneyOnTheServerNamedCellAndEndsWithoutAPath()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(TestPlay.ActionCell + 1);
        var journey = bot.TravelAsync(TestPlay.EastMapId, TestContext.Current.CancellationToken);
        var start = bot.State.Cell!.Value;

        Assert.IsType<MapMovementRequest>(await TestWalks.SentAsync(play, connection, 1));

        var stood = MapGeometry.PointOf(start);

        connection.Server(new MapMovementRefusedEvent { X = stood.X, Y = stood.Y });
        await bot.ProcessedAsync();

        Assert.Equal(TravelOutcome.NoPath, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Equal(TestPlay.MapId, bot.State.MapId);
        Assert.Equal(start, bot.State.Cell);
        Assert.Empty(bot.State.Path);
        Assert.False(bot.State.IsWalking);
        Assert.Single(connection.Sent);
    }
}
