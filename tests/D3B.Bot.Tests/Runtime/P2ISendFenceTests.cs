using D3B.Bot.Movement;
using D3B.Bot.Tests.Network;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

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
    public async Task AFramePostedAfterTheProcessingMarkerIsOutsideThatMovementCutoff()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);
        var wakes = 0;
        var markerWakeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseWake = new ManualResetEventSlim();
        using var releaseGate = new ManualResetEventSlim();
        using var waiterCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var waiter = bot.WaitUntilAsync(
            _ =>
            {
                if (Interlocked.Increment(ref wakes) == 4)
                {
                    markerWakeEntered.TrySetResult();

                    if (!releaseWake.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("The test did not release the actor's inbox.");
                }

                return false;
            },
            waiterCancellation.Token);
        await bot.ProcessedAsync();

        var sending = bot.RunAsync(
            async (context, cancellationToken) => await context.SendGuardedAsync(
                new MapChangeRequest { MapId = (int)TestPlay.EastMapId },
                () =>
                {
                    gateEntered.TrySetResult();

                    if (!releaseGate.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("The test did not release the movement guard.");

                    return MovementGuard.IsAllowed(context, TestPlay.MapId);
                },
                cancellationToken),
            TestContext.Current.CancellationToken);

        try
        {
            await Task.WhenAll(
                markerWakeEntered.Task.WaitAsync(TestContext.Current.CancellationToken),
                gateEntered.Task.WaitAsync(TestContext.Current.CancellationToken));

            // This frame is posted behind the already handled Processing marker, while its sender is paused at the gate.
            connection.Server(new MapCurrentEvent { MapId = TestPlay.EastMapId });
            releaseGate.Set();

            Assert.True(await sending);
            Assert.Equal(TestPlay.MapId, bot.State.MapId);
            Assert.Single(connection.Sent);
        }
        finally
        {
            releaseGate.Set();
            releaseWake.Set();
            waiterCancellation.Cancel();
        }

        await bot.ProcessedAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);

        Assert.Equal(TestPlay.EastMapId, bot.State.MapId);
        Assert.Single(connection.Sent);
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
    public async Task ARelayObservationFailureDuringTheInboxBarrierPreventsMovementInjection()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(300);
        var barrierEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.Relay.ObservationWait = _ => Task.CompletedTask;
        connection.Session.SendBarrier = async () =>
        {
            barrierEntered.TrySetResult();
            await releaseBarrier.Task.WaitAsync(TestContext.Current.CancellationToken);
        };
        connection.Session.SendGate = _ => true;

        var sending = connection.Session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken);

        try
        {
            await barrierEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
            connection.Relay.FailObservation();
            releaseBarrier.TrySetResult();

            await Assert.ThrowsAsync<RelayInjectionException>(() => sending);
        }
        finally
        {
            releaseBarrier.TrySetResult();
        }

        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnUnknownCarriedTypeDuringTheInboxBarrierLatchesFailureForMovement()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(300);
        var barrierEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Relay.ObservationWait = _ => Task.CompletedTask;
        connection.Session.SendBarrier = async () =>
        {
            barrierEntered.TrySetResult();
            await releaseBarrier.Task.WaitAsync(TestContext.Current.CancellationToken);
        };
        connection.Session.SendGate = _ => true;
        var unknown = new GameMessage
        {
            Event = new GameEvent
            {
                Content = new Any { TypeUrl = "type.ankama.com/unknown.message", Value = ByteString.Empty },
            },
        }.ToByteArray();

        var sending = connection.Session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken);

        try
        {
            await barrierEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
            connection.ServerRaw(unknown);
            releaseBarrier.TrySetResult();

            await Assert.ThrowsAsync<RelayInjectionException>(() => sending);
        }
        finally
        {
            releaseBarrier.TrySetResult();
        }

        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task BytesWithoutAGameMessageLatchTranslationFailureForMovement()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(300);
        connection.Session.SendBarrier = () => Task.CompletedTask;
        connection.Session.SendGate = _ => true;

        connection.ServerRaw([0x12, 0x7F, 0x01]);

        await Assert.ThrowsAsync<RelayInjectionException>(() => connection.Session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnUnderstoodGameEnvelopeWithoutContentDoesNotLatchTranslationFailure()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(300);
        connection.Session.SendBarrier = () => Task.CompletedTask;
        connection.Session.SendGate = _ => true;

        connection.ServerRaw(new GameMessage { Request = new GameRequest { Id = -1 } }.ToByteArray());

        await connection.Session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken);

        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task APartialButReadableMessageDoesNotLatchTranslationFailure()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(300);
        connection.Session.SendBarrier = () => Task.CompletedTask;
        connection.Session.SendGate = _ => true;
        var envelope = GameMessage.Parser.ParseFrom(TestGame.Event(new MapInformationEvent { MapId = (int)TestPlay.MapId }));
        var value = envelope.Event.Content.Value.ToByteArray();
        envelope.Event.Content.Value = ByteString.CopyFrom([.. value, 0xA0, 0x01, 0x01]);

        connection.ServerRaw(envelope.ToByteArray());

        await connection.Session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken);

        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task CancellingTheRelayWaitPreventsMovementInjection()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(300);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        connection.Relay.ObservationWait = async cancellationToken =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };

        var sending = connection.Session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            cancellation.Token);

        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);

        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ClosingTheConnectionDuringTheRelayWaitPreventsMovementInjection()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(300);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.Relay.ObservationWait = async cancellationToken =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };

        var sending = connection.Session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken);

        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        connection.Close();

        await Assert.ThrowsAsync<RelayInjectionException>(() => sending);

        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnInboxHandlerFailureBeforeTheMovementMarkerFailsClosed()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        // A null attached state reaches the actor's normal handler catch and proves the barrier does not acknowledge it.
        bot.Attach(connection.Session, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.Session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

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
