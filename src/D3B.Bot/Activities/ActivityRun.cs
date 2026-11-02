using System.Diagnostics.CodeAnalysis;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Journal;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;

namespace D3B.Bot.Activities;

/// <summary>An activity a bot runs: its steps one after another, and what becomes of it (D87).</summary>
/// <remarks>
/// <para>
/// Each step, and each unloading and recovery, is a behavior the bot starts once it can act: a game connection plays the
/// character, placed on a map and out of a fight, and the player has not taken it over. Until then the run waits, with no
/// delay: for the client's new connection to take the bot back (D65), for the player to resume it (D66), for a fight to
/// end (D88). Before each step, a full character is unloaded instead; still full after that, the activity fails.
/// </para>
/// <para>
/// An interruption cancels the step in progress. The activity says what it leads to (<see cref="BotActivity.OnInterrupted"/>);
/// to resume, the run waits until the bot can act, recovers the character (<see cref="BotActivity.RecoverAsync"/>), then goes
/// on with the next step. The host stopping ends the run, as <see cref="Stop"/> does.
/// </para>
/// </remarks>
[SuppressMessage("Design", "CA1001", Justification = "The stop's token source holds no timer, and nothing reads its wait handle; disposed, a stop asked once the run is over would throw.")]
public sealed class ActivityRun
{
    private const int NoInterruption = -1;
    private const long NotEnded = -1;

    private readonly BotActor _bot;
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource<ActivityOutcome> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _status = (int)ActivityStatus.Waiting;
    private int _interruption = NoInterruption;
    private readonly DateTimeOffset _startedAt;
    private long _endedAt = NotEnded;
    private int _steps;
    private Exception? _failure;

    // Only the run's own sequence of behaviors reads and writes them.
    private bool _unloaded;
    private bool _stillFull;
    private int _fightsSeen;

    /// <summary>Gets the activity.</summary>
    public BotActivity Activity { get; }

    /// <summary>Gets the name the activity was started under, such as its profile's, if its starter gave one.</summary>
    public string? Title { get; }

    /// <summary>Gets the character as the activity found it, from which what it gained is told, such as its professions' experience (D146).</summary>
    public CharacterState StartedWith { get; }

    /// <summary>Gets what the activity does now.</summary>
    public ActivityStatus Status => (ActivityStatus)Volatile.Read(ref _status);

    /// <summary>Gets the last interruption that cancelled a step, if any.</summary>
    public Interruption? LastInterruption => Volatile.Read(ref _interruption) is var reason and not NoInterruption ? (Interruption)reason : null;

    /// <summary>Gets the number of steps the activity ran to their end.</summary>
    public int Steps => Volatile.Read(ref _steps);

    /// <summary>Gets when the activity started, by the bot's clock.</summary>
    public DateTimeOffset StartedAt => _startedAt;

    /// <summary>Gets when the activity ended, by the bot's clock, once it is over.</summary>
    public DateTimeOffset? EndedAt => Volatile.Read(ref _endedAt) is var ticks and not NotEnded ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;

    /// <summary>Gets the error that made the activity fail, as it threw it; none when it failed for a character still full.</summary>
    public Exception? Failure => Volatile.Read(ref _failure);

    /// <summary>Gets whether the activity is over.</summary>
    public bool IsOver => _completion.Task.IsCompleted;

    /// <summary>Gets whether the activity failed for a character still full once unloaded, which no error says.</summary>
    /// <remarks>Read once the run is over: the run writes it before it completes.</remarks>
    public bool IsStillFull => IsOver && _stillFull;

    /// <summary>Gets a task that completes once the activity is over, with how it ended.</summary>
    public Task<ActivityOutcome> Completion => _completion.Task;

    /// <summary>Raised when what the activity does changes, from the thread that runs it: a handler must be quick, and must not throw.</summary>
    public event EventHandler<ActivityStatus>? StatusChanged;

    internal ActivityRun(BotActor bot, BotActivity activity, string? title = null)
    {
        _bot = bot;
        Activity = activity;
        Title = title;
        StartedWith = bot.State;
        _startedAt = bot.Time.GetUtcNow();
    }

    /// <summary>Stops the activity: the step in progress is cancelled, and no other starts.</summary>
    public void Stop()
    {
        // Cancelled off the caller's thread: the step's code runs where its token's callbacks run.
        _ = _stop.CancelAsync();
    }

    internal void Start()
    {
        _ = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        BotLog.ActivityStarted(_bot.Log, _bot.Key.CharacterId, Activity.Name);
        _bot.Journal.Add(new ActivityStartedEntry(_bot.Time.GetUtcNow(), Activity.Name));

        var outcome = await LoopAsync().ConfigureAwait(false);

        BotLog.ActivityOver(_bot.Log, _bot.Key.CharacterId, Activity.Name, Steps, outcome);
        _bot.Journal.Add(new ActivityOverEntry(_bot.Time.GetUtcNow(), Activity.Name, Steps, outcome, Failure?.Message, _stillFull));

        // An activity is made for one start (D93): nothing but its run holds it, so what it holds is let go of once over.
        if (Activity is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                BotLog.ActivityFailed(_bot.Log, _bot.Key.CharacterId, Activity.Name, exception);
            }
        }

        Volatile.Write(ref _endedAt, _bot.Time.GetUtcNow().UtcTicks);
        SetStatus(ActivityStatus.Over);
        _completion.TrySetResult(outcome);
    }

    private async Task<ActivityOutcome> LoopAsync()
    {
        Interruption? interrupted = null;

        _fightsSeen = _bot.FightsBegun;

        while (true)
        {
            Interruption cut;

            SetStatus(ActivityStatus.Waiting);

            try
            {
                if (interrupted is { } reason)
                {
                    await _bot.RunWhenReadyAsync((context, token) => RecoverAsync(context, reason, token), _stop.Token).ConfigureAwait(false);
                    interrupted = null;
                    SetStatus(ActivityStatus.Waiting);
                }

                switch (await _bot.RunWhenReadyAsync(StepAsync, _stop.Token).ConfigureAwait(false))
                {
                    case Next.Done:
                        return ActivityOutcome.Done;
                    case Next.StillFull:
                        BotLog.ActivityStillFull(_bot.Log, _bot.Key.CharacterId, Activity.Name);
                        _stillFull = true;

                        return ActivityOutcome.Failed;
                }

                // Measured through the app: steps sent one after another went faster than the client showed them. The
                // client's display time at least, fixed and the owner's to raise, is let pass after each (D154).
                await Task.Delay(_bot.Delays.BetweenSteps, _bot.Time, _stop.Token).ConfigureAwait(false);

                continue;
            }
            catch (BotInterruptedException exception) when (exception.Reason is not Interruption.Stopped)
            {
                cut = exception.Reason;
            }
            catch (BotInterruptedException)
            {
                return ActivityOutcome.Stopped;
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                return ActivityOutcome.Stopped;
            }
            catch (ObjectDisposedException exception) when (exception.ObjectName == typeof(BotActor).FullName)
            {
                return ActivityOutcome.Stopped;
            }
            catch (Exception exception)
            {
                BotLog.ActivityFailed(_bot.Log, _bot.Key.CharacterId, Activity.Name, exception);
                Volatile.Write(ref _failure, exception);

                return ActivityOutcome.Failed;
            }

            interrupted = cut;
            _fightsSeen = _bot.FightsBegun;
            Volatile.Write(ref _interruption, (int)cut);

            // An unloading cut short is no unloading: the next step tries it again.
            _unloaded = false;

            InterruptionResponse response;
            GameText? note;

            try
            {
                response = Activity.OnInterrupted(cut);
                note = Activity.Describe(cut);
            }
            catch (Exception exception)
            {
                BotLog.ActivityFailed(_bot.Log, _bot.Key.CharacterId, Activity.Name, exception);
                Volatile.Write(ref _failure, exception);

                return ActivityOutcome.Failed;
            }

            BotLog.ActivityInterrupted(_bot.Log, _bot.Key.CharacterId, Activity.Name, cut, response);
            _bot.Journal.Add(new ActivityInterruptedEntry(_bot.Time.GetUtcNow(), Activity.Name, cut, response, note));

            // A fight the AI plays for the activity is answered once over: paused or stopped before, the AI would stop too.
            if (cut is Interruption.Fight && Activity.FightsItsFights)
            {
                try
                {
                    response = Activity.OnFightOver(await FoughtAsync().ConfigureAwait(false), response);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                {
                    return ActivityOutcome.Stopped;
                }
                catch (ObjectDisposedException exception) when (exception.ObjectName == typeof(BotActor).FullName)
                {
                    return ActivityOutcome.Stopped;
                }
                catch (Exception exception)
                {
                    BotLog.ActivityFailed(_bot.Log, _bot.Key.CharacterId, Activity.Name, exception);
                    Volatile.Write(ref _failure, exception);

                    return ActivityOutcome.Failed;
                }
            }

            if (response is InterruptionResponse.Stop)
                return ActivityOutcome.Stopped;

            if (response is InterruptionResponse.Pause)
                _bot.Pause();
        }
    }

    private async Task<FightEnd?> FoughtAsync()
    {
        // The fight that cut the step, counted as its context came: it may be over already, a short one, and its end is kept.
        var fight = _bot.FightsBegun;

        await _bot.WaitUntilAsync(bot => bot.State.Fight is not { Phase: not FightPhase.Ended } || !bot.IsConnected || bot.FightsBegun != fight, _stop.Token).ConfigureAwait(false);

        return _bot.LastEndedFight == fight ? _bot.LastFightEnd : null;
    }

    private async Task<bool> RecoverAsync(BotContext context, Interruption reason, CancellationToken cancellationToken)
    {
        SetStatus(ActivityStatus.Recovering);
        await Activity.RecoverAsync(context, reason, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async Task<Next> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        // A protector attacks 2 ms after the harvest's end (D144), once the step that harvested is over and before the next
        // begins, so that no step was cut and the activity neither counted nor answered the fight (D266): a fight begun
        // between steps is the activity's as one that cut a step.
        if (_bot.FightsBegun != _fightsSeen)
            throw new BotInterruptedException(Interruption.Fight);

        var state = context.State;

        if (Activity.IsFull(state))
        {
            if (_unloaded)
                return Next.StillFull;

            SetStatus(ActivityStatus.Unloading);
            BotLog.ActivityUnloading(_bot.Log, _bot.Key.CharacterId, Activity.Name, state.Weight, state.MaxWeight);
            _bot.Journal.Add(new ActivityUnloadingEntry(_bot.Time.GetUtcNow(), Activity.Name, state.Weight, state.MaxWeight));
            _unloaded = true;

            return await Activity.UnloadAsync(context, cancellationToken).ConfigureAwait(false) ? Next.Step : Next.StillFull;
        }

        _unloaded = false;
        SetStatus(ActivityStatus.Stepping);

        var more = await Activity.StepAsync(context, cancellationToken).ConfigureAwait(false);

        Interlocked.Increment(ref _steps);

        return more ? Next.Step : Next.Done;
    }

    private void SetStatus(ActivityStatus status)
    {
        if (Interlocked.Exchange(ref _status, (int)status) != (int)status)
            StatusChanged?.Invoke(this, status);
    }

    private enum Next
    {
        Step,
        Done,
        StillFull,
    }
}
