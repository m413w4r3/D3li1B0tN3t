using D3B.Bot.Movement;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Runtime;

public sealed class P2ISendFenceTests
{
    [Fact]
    public async Task ASessionWaitsForRelayObservationBeforeItsInboxBarrierAndGate()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(300);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();

        connection.Relay.ObservationWait = async cancellationToken =>
        {
            order.Add("relay");
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        };
        connection.Session.SendBarrier = () =>
        {
            order.Add("inbox");

            return Task.CompletedTask;
        };
        connection.Session.SendGate = _ =>
        {
            order.Add("gate");

            return true;
        };

        var sending = connection.Session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken);

        try
        {
            var reached = await Task.WhenAny(entered.Task, sending);

            Assert.Same(entered.Task, reached);
            Assert.False(sending.IsCompleted);
            Assert.Equal(["relay"], order);
        }
        finally
        {
            release.TrySetResult();
        }

        await sending;

        Assert.Equal(["relay", "inbox", "gate"], order);
    }

    [Fact]
    public async Task AGuardedSendWaitsForRelayObservationBeforeApplyingItsMovementGuard()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.Relay.ObservationWait = async cancellationToken =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        };

        var sending = bot.RunAsync(
            async (context, cancellationToken) => await context.SendGuardedAsync(
                new MapChangeRequest { MapId = (int)TestPlay.EastMapId },
                () => MovementGuard.IsAllowed(context, TestPlay.MapId),
                cancellationToken),
            TestContext.Current.CancellationToken);

        try
        {
            var reached = await Task.WhenAny(entered.Task, sending);

            Assert.Same(entered.Task, reached);
            Assert.False(sending.IsCompleted);
            Assert.Empty(connection.Sent);

            connection.Server(new MapCurrentEvent { MapId = TestPlay.EastMapId });
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.False(await sending);
        await bot.ProcessedAsync();

        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ARelayObservationFailurePreventsMovementInjection()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(300);
        var barriers = 0;

        connection.Relay.ObservationWait = _ => Task.FromException(new RelayInjectionException("The relay stream is no longer observable."));
        connection.Session.SendBarrier = () =>
        {
            barriers++;

            return Task.CompletedTask;
        };
        connection.Session.SendGate = _ => true;

        await Assert.ThrowsAsync<RelayInjectionException>(() => connection.Session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.Equal(0, barriers);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task NonMovementSendsDoNotWaitForRelayObservation()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(300);
        var waits = 0;

        connection.Relay.ObservationWait = _ =>
        {
            waits++;

            return Task.CompletedTask;
        };

        await connection.Session.SendAsync(new DialogLeaveRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(0, waits);
        Assert.Single(connection.Sent);
    }
}
