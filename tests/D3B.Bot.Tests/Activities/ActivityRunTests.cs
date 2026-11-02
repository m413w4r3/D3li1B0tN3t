using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Activities;

public sealed class ActivityRunTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task StepsRunOneAfterAnotherUntilTheActivityIsDone()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        Assert.Same(run, bot.Activity);
        Assert.Equal(1, await activity.SteppingAsync(bot));
        Assert.Equal(ActivityStatus.Stepping, run.Status);

        connection.Server(new KamasUpdateEvent { Kamas = 1 });

        Assert.Equal(2, await activity.SteppingAsync(bot));

        connection.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(2, run.Steps);
        Assert.Equal(ActivityStatus.Over, run.Status);
        Assert.Null(run.LastInterruption);
    }

    [Fact]
    public async Task TheNextStepWaitsTheTimeBetweenTwoActions()
    {
        _play.Bots.Delays = BotDelays.Measured;

        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Server(new KamasUpdateEvent { Kamas = 1 });
        await WaitForAsync(() => run.Steps is 1);

        // The client's display time at least, measured: the next step starts once it passed.
        _play.Time.Advance(BotDelays.Measured.BetweenSteps - TimeSpan.FromMilliseconds(1));
        await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.Equal(1, activity.Started);

        _play.Time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Equal(2, await activity.SteppingAsync(bot));
        run.Stop();
        await run.Completion;
    }
    [Fact]
    public async Task OneActivityRunsAtATime()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var run = bot.StartActivity(new TestActivity());

        Assert.Throws<InvalidOperationException>(() => bot.StartActivity(new TestActivity()));

        run.Stop();
        await run.Completion;

        var next = new TestActivity();

        bot.StartActivity(next);
        await next.SteppingAsync(bot);
        connection.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await bot.Activity!.Completion);
    }

    [Fact]
    public async Task AFullCharacterIsUnloadedBeforeTheNextStep()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity { Full = state => state.Load >= 0.9 };

        connection.Server(new InventoryWeightEvent { Weight = 7_000, MaxWeight = 7_590 });
        await bot.ProcessedAsync();

        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);

        Assert.Equal(1, activity.Unloads);

        connection.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
        Assert.Equal(1, run.Steps);
    }

    [Fact]
    public async Task ACharacterStillFullOnceUnloadedFailsTheActivity()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity { Full = _ => true, UnloadEmpties = false };

        connection.Server(new InventoryWeightEvent { Weight = 7_590, MaxWeight = 7_590 });

        var run = bot.StartActivity(activity);

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
        Assert.Equal(1, activity.Unloads);
        Assert.Equal(0, activity.Started);
    }

    [Fact]
    public async Task ThePlayerTakingOverHoldsTheActivityUntilTheBotIsResumedThenRecovers()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Client(new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 2 });
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting);
        await bot.ProcessedAsync();

        Assert.True(bot.IsPaused);
        Assert.Equal(1, activity.Started);
        Assert.Equal(Interruption.Player, run.LastInterruption);

        bot.Resume();

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.Equal([Interruption.Player], activity.Recoveries);

        connection.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
    }

    [Fact]
    public async Task TheRecoveryLeavesTheBankThePlayerLeftOpen()
    {
        var (connection, bot) = await _play.EnterAsync(396, TestPlay.InsideMapId);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Client(new NpcActionRequest { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId, Action = 3 });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage });
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting);

        bot.Resume();

        Assert.IsType<DialogLeaveRequest>(await connection.SentAsync(1));

        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.False(bot.State.IsInDialog);
    }

    [Fact]
    public async Task ADisconnectionHoldsTheActivityUntilTheClientsNewConnectionPlacesTheCharacter()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Close();
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting);

        Assert.Equal(Interruption.Disconnected, run.LastInterruption);
        Assert.False(bot.IsPaused);

        var (again, _) = await _play.EnterAsync(301);

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.Equal([Interruption.Disconnected], activity.Recoveries);

        again.Server(new KamasUpdateEvent { Kamas = 0 });

        Assert.Equal(ActivityOutcome.Done, await run.Completion);
    }

    [Fact]
    public async Task AFightHoldsTheActivityUntilItEndsAndTheCharacterIsPlaced()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(new MapCurrentEvent { MapId = 191_104_008 });
        await WaitForAsync(() => run.Status is ActivityStatus.Waiting);

        Assert.Equal(Interruption.Fight, run.LastInterruption);

        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        await bot.ProcessedAsync();

        Assert.Equal(1, activity.Started);

        connection.Server(TestPlay.Information(358));

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.Equal([Interruption.Fight], activity.Recoveries);
    }

    [Fact]
    public async Task AFightBegunBetweenTwoStepsIsTheActivitysAsOneThatCutAStep()
    {
        // Measured in game (D266): a protector's fight began as the harvest's step was over, and the activity never knew it.
        _play.Bots.Delays = BotDelays.Measured;

        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Server(new KamasUpdateEvent { Kamas = 1 });
        await WaitForAsync(() => run.Steps is 1);

        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(TestPlay.Information(358));
        await bot.ProcessedAsync();
        _play.Time.Advance(BotDelays.Measured.BetweenSteps);

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.Equal([Interruption.Fight], activity.Interruptions);
        Assert.Equal([Interruption.Fight], activity.Recoveries);
        Assert.Equal(Interruption.Fight, run.LastInterruption);

        run.Stop();
        await run.Completion;
    }

    [Fact]
    public async Task AnActivityMayPauseTheBotOnAFightWhichTheCardResumes()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity { Response = InterruptionResponse.Pause };
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        await WaitForAsync(() => bot.IsPaused);

        connection.Server(new ContextDestroyEvent());
        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(TestPlay.Information(358));
        await bot.ProcessedAsync();

        // Over, the fight leaves the bot paused: nothing starts before the card resumes it.
        Assert.Equal(1, activity.Started);
        Assert.Equal(ActivityStatus.Waiting, run.Status);

        bot.Resume();

        Assert.Equal(2, await activity.SteppingAsync(bot));
        Assert.Equal([Interruption.Fight], activity.Recoveries);
    }
    [Fact]
    public async Task AnActivityMayEndOnAnInterruption()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity { Response = InterruptionResponse.Stop };
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        connection.Client(new MapMovementRequest { MapId = TestPlay.MapId, KeyMovements = { 300, 301 } });

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Equal([Interruption.Player], activity.Interruptions);
        Assert.Empty(activity.Recoveries);
    }

    [Fact]
    public async Task StoppingTheRunCancelsItsStep()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);
        run.Stop();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Empty(activity.Interruptions);

        await bot.ProcessedAsync();

        Assert.False(bot.IsActing);
        Assert.False(bot.IsPaused);
    }

    [Fact]
    public async Task AStepThatThrowsFailsTheActivity()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var run = bot.StartActivity(new TestActivity { Throws = true });

        Assert.Equal(ActivityOutcome.Failed, await run.Completion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheBotStoppingEndsTheActivityWhetherItStepsOrWaits(bool stepping)
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var activity = new TestActivity();
        var run = bot.StartActivity(activity);

        await activity.SteppingAsync(bot);

        if (!stepping)
        {
            connection.Close();
            await WaitForAsync(() => run.Status is ActivityStatus.Waiting);
        }

        await bot.DisposeAsync();

        Assert.Equal(ActivityOutcome.Stopped, await run.Completion);
        Assert.Throws<ObjectDisposedException>(() => bot.StartActivity(new TestActivity()));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The activity never got there.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}
