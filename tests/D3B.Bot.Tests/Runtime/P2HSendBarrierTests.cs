using D3B.Bot.Movement;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Network;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Runtime;

public sealed class P2HSendBarrierTests
{
    [Fact]
    public async Task AnOlderSessionCannotReplyThroughTheNewSessionsTrustedDialog()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        var (firstConnection, bot) = await play.EnterAsync(300);
        var firstSession = Assert.Single(observer.Sessions);
        var secondConnection = play.Connect(TestPlay.Account);

        secondConnection.Server(TestPlay.Chosen());
        secondConnection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        secondConnection.Server(TestPlay.Information(300));
        secondConnection.Server(new NpcDialogCreationEvent { MapId = TestPlay.MapId, NpcActorId = TestPlay.WayActorId });
        secondConnection.Server(new NpcDialogQuestionEvent
        {
            MessageId = 1,
            Replies = { new NpcReply { ReplyId = TestPlay.WayReplyId } },
        });
        await bot.ProcessedAsync();

        Assert.True(bot.State.IsInDialog);
        Assert.True(bot.State.IsPositionTrusted);

        var exception = await Record.ExceptionAsync(() => firstSession.SendAsync(
            new NpcDialogReplyRequest { ReplyId = TestPlay.WayReplyId },
            TestContext.Current.CancellationToken));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(firstConnection.Sent);
    }

    [Fact]
    public async Task TheSessionGateRejectsObjectUseFromAClientReportedCell()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        var (connection, bot) = await play.EnterAsync(300);

        connection.Client(new MapMovementRequest { MapId = (int)TestPlay.MapId });
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301 } });
        connection.Client(new MapMovementCancelRequest { CellId = 300 });
        await bot.ProcessedAsync();

        var session = Assert.Single(observer.Sessions);
        var send = session.SendAsync(new ObjectUseRequest { Uid = 50 }, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => send);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AStoppingSignalRejectsMovementBeforeTheCurrentSessionClears()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        var (connection, bot) = await play.EnterAsync(300);
        var session = connection.Session;
        var state = bot.State;
        var request = new ObjectUseRequest { Uid = 50 };

        Assert.True(bot.IsConnected);
        Assert.True(state.IsPositionTrusted);
        Assert.True(bot.CanSendRawRequest(session, request));

        bot.BeginStopping();

        Assert.True(bot.IsConnected);
        Assert.Same(state, bot.State);
        Assert.True(bot.State.IsPositionTrusted);

        var exception = await Record.ExceptionAsync(() => session.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task StoppingClosesMovementAuthorizationBeforeTheInboxDrains()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        var (connection, bot) = await play.EnterAsync(300);
        var expectationRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expectationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseExpectation = new ManualResetEventSlim();

        var behavior = bot.RunAsync(
            async (context, token) =>
            {
                context.Expect((_, message, _) =>
                {
                    if (message is KamasUpdateEvent)
                    {
                        expectationEntered.TrySetResult();

                        if (!releaseExpectation.Wait(TimeSpan.FromSeconds(10)))
                            throw new TimeoutException("The test did not release the actor's inbox.");
                    }

                    return false;
                });
                expectationRegistered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);

                return true;
            },
            TestContext.Current.CancellationToken);

        await expectationRegistered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await bot.ProcessedAsync();
        connection.Server(new KamasUpdateEvent { Kamas = 1 });
        await expectationEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        var stopping = bot.DisposeAsync();
        var session = Assert.Single(observer.Sessions);
        var send = session.SendAsync(new ObjectUseRequest { Uid = 50 }, TestContext.Current.CancellationToken);
        var exception = await Record.ExceptionAsync(() => send);

        releaseExpectation.Set();
        await stopping;
        await Assert.ThrowsAsync<BotInterruptedException>(() => behavior);

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task QueuedAttachAndPlacementCannotRestoreMovementAuthorizationWhileStopping()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        var (connection, bot) = await play.EnterAsync(300);
        var inboxHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reattached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseInbox = new ManualResetEventSlim();
        using var releaseWaiter = new ManualResetEventSlim();
        var initialState = bot.State;
        // Hold the loop so attach, placement, and behavior-start inputs queue before disposal.
        var inboxWaiter = bot.WaitUntilAsync(
            current =>
            {
                if (current.State.MapId != initialState.MapId || current.State.Cell != initialState.Cell)
                    return false;

                inboxHeld.TrySetResult();

                if (!releaseInbox.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("The test did not release the actor's inbox.");

                return true;
            },
            TestContext.Current.CancellationToken);

        await inboxHeld.Task.WaitAsync(TestContext.Current.CancellationToken);

        var secondConnection = play.Connect(TestPlay.Account);
        var secondSession = secondConnection.Session;
        var placedState = initialState with
        {
            MapId = TestPlay.NorthMapId,
            Cell = 350,
            Provenance = CellProvenance.ServerPlaced,
        };
        secondSession.SendGate = request => bot.CanSendRawRequest(secondSession, request);
        secondSession.SendBarrier = bot.ProcessedAsync;
        bot.Attach(secondSession, placedState);
        bot.See(secondSession, RelayDirection.FromServer, new MapCurrentEvent { MapId = TestPlay.NorthMapId });
        bot.See(secondSession, RelayDirection.FromServer, TestPlay.Information(350, TestPlay.NorthMapId));

        var reattachWaiter = bot.WaitUntilAsync(
            current =>
            {
                if (current.State.MapId != TestPlay.NorthMapId || current.State.Cell != 350)
                    return false;

                reattached.TrySetResult();

                if (!releaseWaiter.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("The test did not release the state waiter.");

                return true;
            },
            TestContext.Current.CancellationToken);
        var queuedStart = bot.RunAsync(
            async (_, token) =>
            {
                startEntered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);

                return true;
            },
            TestContext.Current.CancellationToken);

        var stopping = bot.DisposeAsync().AsTask();
        releaseInbox.Set();
        await Task.WhenAny(reattached.Task, stopping).WaitAsync(TestContext.Current.CancellationToken);

        Exception? sendException;

        if (reattached.Task.IsCompleted)
        {
            sendException = await Record.ExceptionAsync(() => secondSession.SendAsync(
                new ObjectUseRequest { Uid = 50 },
                TestContext.Current.CancellationToken));
        }
        else
        {
            await stopping;
            sendException = await Record.ExceptionAsync(() => secondSession.SendAsync(
                new ObjectUseRequest { Uid = 50 },
                TestContext.Current.CancellationToken));
        }

        releaseWaiter.Set();
        await stopping;
        await Assert.ThrowsAsync<BotInterruptedException>(() => queuedStart);
        await Record.ExceptionAsync(() => inboxWaiter);
        await Record.ExceptionAsync(() => reattachWaiter);

        Assert.IsType<InvalidOperationException>(sendException);
        Assert.Empty(secondConnection.Sent);
        Assert.False(bot.IsConnected);
        Assert.Equal(initialState.MapId, bot.State.MapId);
        Assert.Equal(initialState.Cell, bot.State.Cell);
        Assert.False(startEntered.Task.IsCompleted);
    }

    [Fact]
    public async Task AQueuedMapChangeIsAppliedBeforeGuardedSendAuthorization()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        var expectationRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expectationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var beginSend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sendAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseExpectation = new ManualResetEventSlim();
        var guardCalls = 0;
        var send = bot.RunAsync(
            async (context, token) =>
            {
                context.Expect((_, message, _) =>
                {
                    if (message is KamasUpdateEvent)
                    {
                        expectationEntered.TrySetResult();

                        if (!releaseExpectation.Wait(TimeSpan.FromSeconds(10)))
                            throw new TimeoutException("The test did not release the actor's inbox.");
                    }

                    return false;
                });
                expectationRegistered.TrySetResult();

                await beginSend.Task.WaitAsync(token);
                var pending = context.SendGuardedAsync(
                    new MapChangeRequest { MapId = (int)TestPlay.EastMapId },
                    () =>
                    {
                        Interlocked.Increment(ref guardCalls);

                        return MovementGuard.IsAllowed(context, TestPlay.MapId);
                    },
                    token);
                sendAttempted.TrySetResult();

                return await pending;
            },
            TestContext.Current.CancellationToken);

        await expectationRegistered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await bot.ProcessedAsync();
        connection.Server(new KamasUpdateEvent { Kamas = 1 });
        await expectationEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        connection.Server(new MapCurrentEvent { MapId = TestPlay.EastMapId });
        beginSend.TrySetResult();
        await sendAttempted.Task.WaitAsync(TestContext.Current.CancellationToken);

        var completedBeforeRelease = send.IsCompleted;
        var guardCallsBeforeRelease = Volatile.Read(ref guardCalls);
        var sentBeforeRelease = connection.Sent.Count;
        releaseExpectation.Set();

        var sent = await send;
        await bot.ProcessedAsync();

        Assert.False(completedBeforeRelease);
        Assert.Equal(0, guardCallsBeforeRelease);
        Assert.Equal(0, sentBeforeRelease);
        Assert.False(sent);
        Assert.Equal(1, Volatile.Read(ref guardCalls));
        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
        Assert.Null(bot.State.Cell);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task BindingACharacterInstallsItsInboxBarrier()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        await play.EnterAsync(300);

        Assert.NotNull(Assert.Single(observer.Sessions).SendBarrier);
    }

    [Fact]
    public async Task ASessionWaitsForItsBarrierBeforeCheckingAMovementGate()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        await play.EnterAsync(300);
        var session = Assert.Single(observer.Sessions);
        var order = new List<string>();
        session.SendBarrier = async () =>
        {
            order.Add("barrier");
            await Task.Yield();
        };
        session.SendGate = _ =>
        {
            order.Add("gate");

            return true;
        };

        await session.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken);

        Assert.Equal(["barrier", "gate"], order);
    }

    [Fact]
    public async Task ASessionDoesNotWaitForItsBarrierBeforeANonMovementGate()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        await play.EnterAsync(300);
        var session = Assert.Single(observer.Sessions);
        var barriers = 0;
        session.SendBarrier = () =>
        {
            Interlocked.Increment(ref barriers);

            return Task.CompletedTask;
        };

        await session.SendAsync(new DialogLeaveRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(0, Volatile.Read(ref barriers));
    }
}
