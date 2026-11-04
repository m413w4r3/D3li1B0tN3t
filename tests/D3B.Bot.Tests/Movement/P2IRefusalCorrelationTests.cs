using D3B.Bot.Characters;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Movement;

public sealed class P2IRefusalCorrelationTests
{
    [Fact]
    public async Task ALateSameMapRefusalLeavesThePositionAmbiguousUntilFreshPlacement()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        var firstWalk = bot.WalkAsync(301, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        Assert.Equal(WalkOutcome.NoAnswer, await TestWalks.AdvanceUntilDoneAsync(play.Time, firstWalk));

        var secondWalk = bot.WalkAsync(302, TestContext.Current.CancellationToken);

        await connection.SentAsync(2);

        var refusedCell = 311;
        var point = MapGeometry.PointOf(refusedCell);
        connection.Server(new MapMovementRefusedEvent { X = point.X, Y = point.Y });
        await bot.ProcessedAsync();

        Assert.Equal(WalkOutcome.Refused, await secondWalk);
        Assert.Equal(refusedCell, bot.State.Cell);
        Assert.Equal(CellProvenance.ServerRefused, bot.State.Provenance);
        Assert.False(bot.State.IsPositionTrusted);
        Assert.Equal(MovementRefusal.AmbiguousPosition, MovementGuard.Refuse(bot.State, bot.IsConnected));

        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(303, TestContext.Current.CancellationToken));
        Assert.Equal(2, connection.Sent.Count);

        connection.Server(TestPlay.Information(refusedCell));
        await bot.ProcessedAsync();

        Assert.Equal(refusedCell, bot.State.Cell);
        Assert.Equal(CellProvenance.ServerPlaced, bot.State.Provenance);
        Assert.True(bot.State.IsPositionTrusted);
        Assert.Equal(MovementRefusal.None, MovementGuard.Refuse(bot.State, bot.IsConnected));
    }
}
