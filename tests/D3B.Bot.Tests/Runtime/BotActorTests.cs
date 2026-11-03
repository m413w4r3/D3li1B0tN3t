using D3B.Bot.Runtime;
using D3B.Bot.Tests.Network;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Runtime;

public sealed class BotActorTests : IAsyncDisposable
{
    private static readonly Dictionary<string, IMessage> _byItself = new()
    {
        ["confirmation"] = new MapMovementConfirmRequest(),
        ["map information"] = new MapInformationRequest { MapId = TestPlay.MapId },
        ["leaving a dialog"] = new DialogLeaveRequest(),
    };

    private readonly TestPlay _play = new();

    public static TheoryData<string> WhatTheClientSendsByItself()
    {
        return [.. _byItself.Keys];
    }

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task AnExpectationGetsTheMessageOnceTheStateHoldsIt()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var kamas = bot.RunAsync(
            async (context, token) =>
            {
                var update = context.Expect((side, message, state) => message is KamasUpdateEvent && state.Kamas == 42);

                await context.SendAsync(new DialogLeaveRequest(), token);

                return (await update.WaitAsync(TimeSpan.FromSeconds(30), token) as KamasUpdateEvent)?.Kamas;
            },
            TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new KamasUpdateEvent { Kamas = 41 });
        connection.Server(new KamasUpdateEvent { Kamas = 42 });

        Assert.Equal(42, await kamas);
    }

    [Fact]
    public async Task ABehaviorCannotSendRawMovementFromATrustedPosition()
    {
        var (connection, bot) = await _play.EnterAsync(300);

        var exception = await Record.ExceptionAsync(() => bot.RunAsync(
            async (context, token) =>
            {
                await context.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, token);

                return true;
            },
            TestContext.Current.CancellationToken));

        var error = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("guarded movement actions", error.Message, StringComparison.Ordinal);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ABehaviorCannotSendRawMovementFromAClientReportedPosition()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301 } });
        connection.Client(new MapMovementCancelRequest { CellId = 300 });
        await bot.ProcessedAsync();

        var exception = await Record.ExceptionAsync(() => bot.RunAsync(
            async (context, token) =>
            {
                await context.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, token);

                return true;
            },
            TestContext.Current.CancellationToken));

        var error = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("guarded movement actions", error.Message, StringComparison.Ordinal);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task RawMapTransitionRequestsAreProhibitedOutsideFight()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        IMessage[] requests =
        [
            new MapChangeRequest { MapId = 1 },
            new TeleportRequest { MapId = TestPlay.EastMapId, Source = TeleporterType.Map },
            new HavenBagEnterRequest { OwnerId = TestPlay.CharacterId },
            new InteractiveUseRequest { ElementId = TestPlay.TreeElementId, SkillInstanceId = TestPlay.TreeInstanceId },
            new NpcActionRequest { Action = 3, MapId = TestPlay.MapId, NpcActorId = TestPlay.WayActorId },
        ];

        foreach (var request in requests)
        {
            var exception = await Record.ExceptionAsync(() => bot.RunAsync(
                async (context, token) =>
                {
                    await context.SendAsync(request, token);

                    return true;
                },
                TestContext.Current.CancellationToken));

            Assert.IsType<InvalidOperationException>(exception);
        }

        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task RawMapTransitionRequestsAreProhibitedDuringFight()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await bot.ProcessedAsync();
        IMessage[] requests =
        [
            new MapChangeRequest { MapId = 1 },
            new TeleportRequest { MapId = TestPlay.EastMapId, Source = TeleporterType.Map },
            new HavenBagEnterRequest { OwnerId = TestPlay.CharacterId },
            new InteractiveUseRequest { ElementId = TestPlay.TreeElementId, SkillInstanceId = TestPlay.TreeInstanceId },
            new NpcActionRequest { Action = 3, MapId = TestPlay.MapId, NpcActorId = TestPlay.WayActorId },
        ];

        foreach (var request in requests)
        {
            var exception = await Record.ExceptionAsync(() => bot.RunAsync(
                async (context, token) =>
                {
                    await context.SendAsync(request, token);

                    return true;
                },
                TestContext.Current.CancellationToken));

            Assert.IsType<InvalidOperationException>(exception);
        }

        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ARoleplayBehaviorCannotSendRawMovementDuringAFight()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await bot.ProcessedAsync();

        var exception = await Record.ExceptionAsync(() => bot.RunAsync(
            async (context, token) =>
            {
                await context.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, token);

                return true;
            },
            TestContext.Current.CancellationToken));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AFightBehaviorCanSendRawMovementDuringAFight()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await bot.ProcessedAsync();

        var sent = await bot.RunInFightAsync(
            async (context, token) =>
            {
                await context.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, token);

                return true;
            },
            TestContext.Current.CancellationToken);

        Assert.True(sent);
        Assert.IsType<MapMovementRequest>(Assert.Single(connection.Sent));
    }

    [Fact]
    public async Task AnObserverCannotSendRawMovementFromATrustedRoleplayPosition()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        var (connection, _) = await play.EnterAsync(300);

        var exception = await Record.ExceptionAsync(() => observer.Sessions[0].SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnObserverCannotSendRawMovementDuringAFightWithoutAFightBehavior()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        var (connection, bot) = await play.EnterAsync(300);
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await bot.ProcessedAsync();

        var exception = await Record.ExceptionAsync(() => observer.Sessions[0].SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnObserverCannotSendRawMovementWhileAFightBehaviorRuns()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        var (connection, bot) = await play.EnterAsync(300);
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await bot.ProcessedAsync();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var behavior = bot.RunInFightAsync(
            async (_, token) =>
            {
                started.TrySetResult();
                await finish.Task.WaitAsync(token);

                return true;
            },
            TestContext.Current.CancellationToken);

        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        var exception = await Record.ExceptionAsync(() => observer.Sessions[0].SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        finish.TrySetResult();
        await behavior;

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnObserverRegisteredBeforeBotsCannotSendMovementDuringSessionStart()
    {
        Exception? failure = null;
        var observer = new RecordingObserver
        {
            OnStarted = session =>
            {
                try
                {
                    session.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }).GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            },
        };
        await using var play = TestPlay.WithObserversBeforeBots(observer);

        var connection = play.Connect(TestPlay.Account);

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnObserverCannotSendANpcDialogReplyFromAnUntrustedDialogPosition()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        var (connection, bot) = await play.EnterAsync(300);
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301 } });
        connection.Client(new MapMovementCancelRequest { CellId = 300 });
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.MapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 1, Replies = { new NpcReply { ReplyId = TestPlay.WayReplyId } } });
        await bot.ProcessedAsync();

        Assert.True(bot.State.IsInDialog);
        Assert.False(bot.State.IsPositionTrusted);

        var exception = await Record.ExceptionAsync(() => observer.Sessions[0].SendAsync(
            new NpcDialogReplyRequest { ReplyId = TestPlay.WayReplyId },
            TestContext.Current.CancellationToken));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnObserverCanSendANpcDialogReplyFromATrustedDialogPosition()
    {
        var observer = new RecordingObserver();
        await using var play = new TestPlay([observer]);
        var (connection, bot) = await play.EnterAsync(300);
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.MapId, NpcActorId = TestPlay.WayActorId });
        connection.Server(new NpcDialogQuestionEvent { MessageId = 1, Replies = { new NpcReply { ReplyId = TestPlay.WayReplyId } } });
        await bot.ProcessedAsync();

        Assert.True(bot.State.IsInDialog);
        Assert.True(bot.State.IsPositionTrusted);

        await observer.Sessions[0].SendAsync(new NpcDialogReplyRequest { ReplyId = TestPlay.WayReplyId }, TestContext.Current.CancellationToken);

        Assert.IsType<NpcDialogReplyRequest>(Assert.Single(connection.Sent));
    }

    [Fact]
    public async Task AFailedGuardedSendClearsThePendingMovementMap()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        connection.Client(new MapMovementRequest { MapId = (int)TestPlay.MapId });
        await bot.ProcessedAsync();
        Assert.Equal(TestPlay.MapId, bot.State.PendingMovementMapId);

        bot.MarkMovementRequested(TestPlay.NorthMapId);
        await bot.ProcessedAsync();
        Assert.Equal(TestPlay.MapId, bot.State.PendingMovementMapId);

        var sent = await bot.RunAsync(
            async (context, token) => await context.SendGuardedAsync(
                new MapMovementRequest { MapId = (int)TestPlay.MapId },
                () => false,
                token),
            TestContext.Current.CancellationToken);
        await bot.ProcessedAsync();

        Assert.False(sent);
        Assert.Null(bot.State.PendingMovementMapId);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AClosedConnectionDuringGuardedSendClearsThePendingMovementMap()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var failure = bot.RunAsync(
            async (context, token) =>
            {
                connection.Close();

                return await context.SendGuardedAsync(
                    new MapMovementRequest { MapId = (int)TestPlay.MapId },
                    () => true,
                    token);
            },
            TestContext.Current.CancellationToken);

        await Record.ExceptionAsync(() => failure);
        await bot.ProcessedAsync();

        Assert.Null(bot.State.PendingMovementMapId);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ARawFightMovementCannotEscapeAfterTheFightEndsDuringTheDisplayWait()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await bot.ProcessedAsync();
        _play.Bots.Delays = _play.Bots.Delays with { Display = TimeSpan.FromSeconds(1) };

        var behavior = bot.RunInFightAsync(
            async (context, token) =>
            {
                await context.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, token);

                return true;
            },
            TestContext.Current.CancellationToken);

        await Task.Delay(20, TestContext.Current.CancellationToken);
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        await bot.ProcessedAsync();
        _play.Time.Advance(TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior);

        Assert.Contains("guarded movement actions", exception.Message, StringComparison.Ordinal);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnExpectationEndsAfterItsDelayByTheBotsClock()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var wait = new KamasWait();
        var kamas = bot.RunAsync(wait.RunAsync, TestContext.Current.CancellationToken);

        await wait.ExpectingAsync(bot);
        _play.Time.Advance(TimeSpan.FromSeconds(29));
        await Task.Delay(20, TestContext.Current.CancellationToken);

        Assert.False(kamas.IsCompleted);

        _play.Time.Advance(TimeSpan.FromSeconds(1));

        Assert.Null(await kamas);
    }

    [Fact]
    public async Task OneBehaviorRunsAtATime()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var wait = new KamasWait();
        var first = bot.RunAsync(wait.RunAsync, TestContext.Current.CancellationToken);

        await wait.ExpectingAsync(bot);

        await Assert.ThrowsAsync<InvalidOperationException>(() => bot.RunAsync(new KamasWait().RunAsync, TestContext.Current.CancellationToken));

        connection.Server(new KamasUpdateEvent { Kamas = 7 });

        Assert.Equal(7, await first);
        await bot.ProcessedAsync();
        Assert.False(bot.IsActing);
    }

    [Fact]
    public async Task ABehaviorRunsOnlyWhileAGameConnectionPlaysTheCharacter()
    {
        var (connection, bot) = await _play.EnterAsync(300);

        connection.Close();
        await bot.ProcessedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => bot.RunAsync(new KamasWait().RunAsync, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ThePlayerActingInterruptsTheBehaviorAndPausesTheBotUntilResumed()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var wait = new KamasWait();
        var behavior = bot.RunAsync(wait.RunAsync, TestContext.Current.CancellationToken);

        await wait.ExpectingAsync(bot);
        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });

        var interrupted = await Assert.ThrowsAsync<BotInterruptedException>(() => behavior);

        Assert.Equal(Interruption.Player, interrupted.Reason);
        Assert.True(bot.IsPaused);
        await Assert.ThrowsAsync<InvalidOperationException>(() => bot.RunAsync(new KamasWait().RunAsync, TestContext.Current.CancellationToken));

        bot.Resume();
        await bot.ProcessedAsync();

        Assert.False(bot.IsPaused);

        var again = new KamasWait();
        var resumed = bot.RunAsync(again.RunAsync, TestContext.Current.CancellationToken);

        await again.ExpectingAsync(bot);
        connection.Server(new KamasUpdateEvent { Kamas = 9 });

        Assert.Equal(9, await resumed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ThePlayerTeleportingOrOpeningTheHavenBagInterruptsTheBot(bool teleporting)
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var wait = new KamasWait();
        var behavior = bot.RunAsync(wait.RunAsync, TestContext.Current.CancellationToken);

        await wait.ExpectingAsync(bot);
        connection.Client(teleporting
            ? new TeleportRequest { MapId = TestPlay.EastMapId, Source = TeleporterType.Map }
            : new HavenBagEnterRequest { OwnerId = TestPlay.CharacterId });

        Assert.Equal(Interruption.Player, (await Assert.ThrowsAsync<BotInterruptedException>(() => behavior)).Reason);
        Assert.True(bot.IsPaused);
    }

    [Theory]
    [MemberData(nameof(WhatTheClientSendsByItself))]
    public async Task WhatTheClientSendsByItselfDoesNotInterruptTheBot(string request)
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var wait = new KamasWait();
        var behavior = bot.RunAsync(wait.RunAsync, TestContext.Current.CancellationToken);

        await wait.ExpectingAsync(bot);
        connection.Client(_byItself[request]);
        connection.Server(new KamasUpdateEvent { Kamas = 3 });

        Assert.Equal(3, await behavior);
        Assert.False(bot.IsPaused);
    }

    [Fact]
    public async Task ThePlayerLeavingTheBankInterruptsTheBot()
    {
        var (connection, bot) = await _play.EnterAsync(396, TestPlay.InsideMapId);

        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage });

        var wait = new KamasWait();
        var behavior = bot.RunAsync(wait.RunAsync, TestContext.Current.CancellationToken);

        await wait.ExpectingAsync(bot);
        connection.Client(new DialogLeaveRequest());

        Assert.Equal(Interruption.Player, (await Assert.ThrowsAsync<BotInterruptedException>(() => behavior)).Reason);
        Assert.True(bot.IsPaused);
    }

    [Fact]
    public async Task TheClientLeavingByItselfOnceTheServerEndedTheBankDoesNotInterruptTheBot()
    {
        var (connection, bot) = await _play.EnterAsync(396, TestPlay.InsideMapId);

        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage });

        var wait = new KamasWait();
        var behavior = bot.RunAsync(wait.RunAsync, TestContext.Current.CancellationToken);

        await wait.ExpectingAsync(bot);

        // The relay shows the server's end before the client gets it, so the bot's loop sees it before the client's requests.
        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });
        connection.Client(new DialogLeaveRequest());
        connection.Client(new DialogLeaveRequest());
        connection.Server(new KamasUpdateEvent { Kamas = 4 });

        Assert.Equal(4, await behavior);
        Assert.False(bot.IsPaused);
    }

    [Fact]
    public async Task ThePlayerActingWhileTheBotIdlesDoesNotPauseIt()
    {
        var (connection, bot) = await _play.EnterAsync(300);

        connection.Client(new MapMovementRequest { MapId = TestPlay.MapId, KeyMovements = { 300, 301 } });
        await bot.ProcessedAsync();

        Assert.False(bot.IsPaused);
    }

    [Fact]
    public async Task TheConnectionClosingInterruptsTheBehaviorWithoutPausingTheBot()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var wait = new KamasWait();
        var behavior = bot.RunAsync(wait.RunAsync, TestContext.Current.CancellationToken);

        await wait.ExpectingAsync(bot);
        connection.Close();

        var interrupted = await Assert.ThrowsAsync<BotInterruptedException>(() => behavior);

        Assert.Equal(Interruption.Disconnected, interrupted.Reason);
        Assert.False(bot.IsPaused);
    }

    [Fact]
    public async Task AFightBeginningInterruptsTheBehaviorWithoutPausingTheBot()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var wait = new KamasWait();
        var behavior = bot.RunAsync(wait.RunAsync, TestContext.Current.CancellationToken);

        await wait.ExpectingAsync(bot);
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });

        Assert.Equal(Interruption.Fight, (await Assert.ThrowsAsync<BotInterruptedException>(() => behavior)).Reason);
        Assert.False(bot.IsPaused);
        Assert.True(bot.State.IsFighting);
    }

    [Fact]
    public async Task AWaitEndsOnceItsConditionHoldsAndAtOnceWhenItHoldsAlready()
    {
        var (connection, bot) = await _play.EnterAsync(300);

        await bot.WaitUntilAsync(b => b.State.Cell is 300, TestContext.Current.CancellationToken);

        var waiting = bot.WaitUntilAsync(b => b.State.Kamas == 5, TestContext.Current.CancellationToken);

        await bot.ProcessedAsync();
        connection.Server(new KamasUpdateEvent { Kamas = 4 });
        await bot.ProcessedAsync();

        Assert.False(waiting.IsCompleted);

        connection.Server(new KamasUpdateEvent { Kamas = 5 });

        await waiting;
    }

    [Fact]
    public async Task AWaitEndsWithItsTokenOrTheBot()
    {
        var (_, bot) = await _play.EnterAsync(300);

        using var stop = new CancellationTokenSource();

        var cancelled = bot.WaitUntilAsync(_ => false, stop.Token);
        var stopped = bot.WaitUntilAsync(_ => false, TestContext.Current.CancellationToken);

        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

        await bot.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => stopped);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => bot.WaitUntilAsync(_ => true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StoppingTheBotInterruptsItsBehavior()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var wait = new KamasWait();
        var behavior = bot.RunAsync(wait.RunAsync, TestContext.Current.CancellationToken);

        await wait.ExpectingAsync(bot);
        await bot.DisposeAsync();

        var interrupted = await Assert.ThrowsAsync<BotInterruptedException>(() => behavior);

        Assert.Equal(Interruption.Stopped, interrupted.Reason);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => bot.RunAsync(new KamasWait().RunAsync, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheCallerStoppingTheBehaviorIsNoInterruption()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var wait = new KamasWait();

        using var stop = new CancellationTokenSource();

        var behavior = bot.RunAsync(wait.RunAsync, stop.Token);

        await wait.ExpectingAsync(bot);
        await stop.CancelAsync();

        var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior);

        Assert.IsNotType<BotInterruptedException>(cancelled);
        Assert.False(bot.IsPaused);
    }

    /// <summary>A behavior that waits for a kamas update from the server, and gives the kamas it holds.</summary>
    private sealed class KamasWait
    {
        private readonly TaskCompletionSource _expecting = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<long?> RunAsync(BotContext context, CancellationToken cancellationToken)
        {
            var update = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is KamasUpdateEvent);

            // The delay starts when the wait does, before the test moves the clock.
            var waiting = update.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

            _expecting.TrySetResult();

            return (await waiting as KamasUpdateEvent)?.Kamas;
        }

        /// <summary>Waits until the bot's loop has the behavior's expectation: a frame shown after it reaches it.</summary>
        public async Task ExpectingAsync(BotActor bot)
        {
            await _expecting.Task;
            await bot.ProcessedAsync();
        }
    }
}
