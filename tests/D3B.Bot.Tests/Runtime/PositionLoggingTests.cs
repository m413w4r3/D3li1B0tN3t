using D3B.Engine.Maps;
using D3B.Bot.Movement;
using D3B.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Tests.Runtime;

public sealed class PositionLoggingTests
{
    private static string[] PositionMessages(TestPlay play)
    {
        return play.Log.GetSnapshot()
            .Where(record => record.Message.StartsWith($"Bot {TestPlay.CharacterId}:", StringComparison.Ordinal))
            .Select(record => record.Message)
            .ToArray();
    }

    [Fact]
    public async Task PositionLogsOnlyMapAndCellTransitions()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        Assert.Equal(
        [
            $"Bot {TestPlay.CharacterId}: plays through game connection g1; position unknown",
            $"Bot {TestPlay.CharacterId}: map {TestPlay.MapId} known; cell unknown until its information",
            $"Bot {TestPlay.CharacterId}: placed on map {TestPlay.MapId} at cell 433",
        ], PositionMessages(play));

        connection.Server(new InventoryWeightEvent { Weight = 10, MaxWeight = 100 });
        connection.Server(TestPlay.Information(433));
        await bot.ProcessedAsync();

        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        await bot.ProcessedAsync();

        connection.Server(TestPlay.Information(433));
        connection.Server(new MapCurrentEvent { MapId = TestPlay.EastMapId });
        await bot.ProcessedAsync();

        connection.Server(new MapCurrentEvent { MapId = TestPlay.EastMapId });
        connection.Server(TestPlay.Information(420, TestPlay.EastMapId));
        await bot.ProcessedAsync();

        Assert.Equal(
        [
            $"Bot {TestPlay.CharacterId}: plays through game connection g1; position unknown",
            $"Bot {TestPlay.CharacterId}: map {TestPlay.MapId} known; cell unknown until its information",
            $"Bot {TestPlay.CharacterId}: placed on map {TestPlay.MapId} at cell 433",
            $"Bot {TestPlay.CharacterId}: map {TestPlay.MapId} repeated; cell cleared",
            $"Bot {TestPlay.CharacterId}: position unknown on map {TestPlay.MapId} until it is placed",
            $"Bot {TestPlay.CharacterId}: placed on map {TestPlay.MapId} at cell 433",
            $"Bot {TestPlay.CharacterId}: map changed from {TestPlay.MapId} to {TestPlay.EastMapId}; cell unknown until its information",
            $"Bot {TestPlay.CharacterId}: position unknown on map {TestPlay.EastMapId} until it is placed",
            $"Bot {TestPlay.CharacterId}: placed on map {TestPlay.EastMapId} at cell 420",
        ], PositionMessages(play));

        Assert.DoesNotContain(play.Log.GetSnapshot(), record => record.Message.Contains("ticket-of-g1", StringComparison.Ordinal));
        Assert.All(
            play.Log.GetSnapshot().Where(record => record.Message.StartsWith($"Bot {TestPlay.CharacterId}:", StringComparison.Ordinal)),
            record => Assert.Equal(LogLevel.Information, record.Level));
    }

    [Fact]
    public async Task AServerShownWalkAndItsConfirmationAreLoggedSeparately()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 432, 431, 416, 388 } });
        await bot.ProcessedAsync();

        connection.Server(new MapMovementConfirmResponse());
        await bot.ProcessedAsync();

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 400, 401 } });
        await bot.ProcessedAsync();

        Assert.Contains(PositionMessages(play), message => message == $"Bot {TestPlay.CharacterId}: walk shown on map {TestPlay.MapId} from cell 433 to cell 388, 4 steps, not confirmed");
        Assert.Contains(PositionMessages(play), message => message == $"Bot {TestPlay.CharacterId}: walk confirmed on map {TestPlay.MapId} at cell 388");
        Assert.Contains(PositionMessages(play), message => message == $"Bot {TestPlay.CharacterId}: ignored self walk on map {TestPlay.MapId} because its start differs from the known cell");
    }

    [Fact]
    public async Task AnIgnoredSelfWalkIsLoggedWithoutClaimingItsPosition()
    {
        await using var play = new TestPlay();
        var connection = play.Connect(TestPlay.Account);

        connection.Server(TestPlay.Chosen());
        Assert.True(play.Bots.TryGet(TestPlay.Key, out var bot));
        await bot.ProcessedAsync();

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 432 } });
        await bot.ProcessedAsync();

        var rejection = Assert.Single(PositionMessages(play), message => message.Contains("ignored self walk", StringComparison.Ordinal));

        Assert.Equal($"Bot {TestPlay.CharacterId}: ignored self walk because the character is not placed on a map", rejection);
        Assert.DoesNotContain("from cell", rejection, StringComparison.Ordinal);
        Assert.DoesNotContain("to cell", rejection, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWalkRefusalReportsItsCellOrUnknownPosition()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var point = MapGeometry.PointOf(388);

        connection.Server(new MapMovementRefusedEvent { X = point.X, Y = point.Y });
        await bot.ProcessedAsync();

        connection.Server(new MapMovementRefusedEvent { X = int.MaxValue, Y = int.MinValue });
        await bot.ProcessedAsync();

        Assert.Contains(PositionMessages(play), message => message == $"Bot {TestPlay.CharacterId}: walk refused on map {TestPlay.MapId} at cell 388");
        Assert.Contains(PositionMessages(play), message => message == $"Bot {TestPlay.CharacterId}: position unknown after a refused walk on map {TestPlay.MapId}");
    }

    [Fact]
    public async Task AClientCancellationIsNotLoggedAsServerConfirmed()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 432, 431 } });
        await bot.ProcessedAsync();

        connection.Client(new MapMovementCancelRequest { CellId = 432 });
        await bot.ProcessedAsync();

        connection.Client(new MapMovementCancelRequest { CellId = 432 });
        await bot.ProcessedAsync();

        Assert.Contains(PositionMessages(play), message => message == $"Bot {TestPlay.CharacterId}: walk cancelled by the client on map {TestPlay.MapId} at cell 432, not server-confirmed");

        var ignored = Assert.Single(play.Log.GetSnapshot(), record => record.Message.Contains("ignored client walk cancellation", StringComparison.Ordinal));

        Assert.Equal(LogLevel.Debug, ignored.Level);
        Assert.Contains("no walk is active", ignored.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWalkSettledWithoutConfirmationIsMarkedEstimated()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 433, 432, 431 } });
        await bot.ProcessedAsync();
        await bot.SettleWalkAsync();

        var settled = Assert.Single(PositionMessages(play), message => message.Contains("walk settled", StringComparison.Ordinal));

        Assert.Equal($"Bot {TestPlay.CharacterId}: walk settled on map {TestPlay.MapId} at cell 431, estimated while the server may still hold it", settled);
        Assert.DoesNotContain("confirmed", settled, StringComparison.Ordinal);
        Assert.True(bot.State.IsWalkHeld);
    }

    [Fact]
    public async Task AnInvalidMapPlacementMakesThePositionUnknownOnce()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        connection.Server(new MapInformationEvent { MapId = TestPlay.MapId });
        await bot.ProcessedAsync();

        connection.Server(new MapInformationEvent { MapId = TestPlay.MapId });
        await bot.ProcessedAsync();

        Assert.Equal(1, PositionMessages(play).Count(message => message == $"Bot {TestPlay.CharacterId}: position unknown on map {TestPlay.MapId} until it is placed"));
        Assert.Null(bot.State.Cell);
    }

    [Fact]
    public async Task DetachAndReconnectLogEachConnectionAndItsPosition()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        connection.Close();
        await bot.ProcessedAsync();

        var reconnect = play.Connect(TestPlay.Account);

        reconnect.Server(TestPlay.Chosen());
        await bot.ProcessedAsync();
        reconnect.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        reconnect.Server(TestPlay.Information(434));
        await bot.ProcessedAsync();

        Assert.Contains(PositionMessages(play), message => message == $"Bot {TestPlay.CharacterId}: game connection g1 closed; last known map {TestPlay.MapId} cell 433");
        Assert.Contains(PositionMessages(play), message => message == $"Bot {TestPlay.CharacterId}: plays through game connection g2; position unknown");
        Assert.Contains(PositionMessages(play), message => message == $"Bot {TestPlay.CharacterId}: placed on map {TestPlay.MapId} at cell 434");
    }

    [Fact]
    public async Task NavigationRefusalLogsProvenanceAndReasonOnceWithoutPayload()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(368);

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 368, 369 } });
        connection.Client(new MapMovementCancelRequest { CellId = 368 });
        await bot.ProcessedAsync();

        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(434, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);

        for (var index = 0; index < 100; index++)
            connection.Server(new InventoryWeightEvent { Weight = index, MaxWeight = 100 });

        await bot.ProcessedAsync();

        var refusal = Assert.Single(PositionMessages(play), message => message.Contains("navigation refused", StringComparison.Ordinal));

        Assert.Equal($"Bot {TestPlay.CharacterId}: navigation refused at map {TestPlay.MapId} cell 368; provenance ClientReported, reason ClientReported; nothing was sent", refusal);
        Assert.DoesNotContain(play.Log.GetSnapshot(), record => record.Message.Contains("ticket-of-g1", StringComparison.Ordinal));
    }
}
