using System.Collections.Immutable;
using System.Threading.Channels;
using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Network;
using D3B.Data.Maps;
using D3B.Engine.World;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Runtime;

/// <summary>A character D3B plays: an actor with one inbox, read on one logical thread, so its state needs no lock (D9).</summary>
/// <remarks>
/// <para>
/// One bot per character of an account (D65). Its inbox gets every message the character's game connection carries,
/// both sides, from the relay's read loops; its loop turns each into the next snapshot of the state
/// (<see cref="CharacterEvents"/>), which anyone reads. A bot outlives its game connections: when the client connects
/// again and chooses the character, the new connection takes the old one's place, and the state starts over from what
/// the server sends.
/// </para>
/// <para>
/// A behavior is an async method that reads snapshots and sends requests (<see cref="RunAsync"/>), one at a time. An
/// interruption cancels it: the game connection closing, the host stopping, a fight beginning (D88), or the player acting
/// on the character. The player wins: a request the player sends that acts on the character (<see cref="PlayerActions"/>)
/// interrupts the bot's behavior, and the bot then pauses, starting nothing until it is resumed (D66).
/// </para>
/// <para>
/// An activity chains behaviors, one step after another, and takes the character back after each interruption
/// (<see cref="StartActivity"/>, D87). Anyone may wait for what a bot's state becomes (<see cref="WaitUntilAsync"/>), as
/// another bot following it will.
/// </para>
/// </remarks>
public sealed class BotActor : IAsyncDisposable
{
    /// <summary>The number of entries a bot's journal keeps: older ones are dropped (D113).</summary>
    public const int JournalCapacity = 2_000;

    private const long NeverEnded = long.MinValue;

    private readonly Channel<Input> _inbox = Channel.CreateUnbounded<Input>(new UnboundedChannelOptions { SingleReader = true });
    private readonly List<GameExpectation> _expectations = [];
    private readonly List<Waiter> _waiters = [];
    private readonly List<Starting> _parked = [];
    private readonly Lock _activityLock = new();
    private readonly AsyncLocal<RunningBehavior?> _movementSendBehavior = new();
    private readonly ILogger _log;
    private readonly Lazy<WorldPathfinder> _world;
    private readonly Lazy<BankDirectory> _banks;
    private readonly Func<BotDelays> _delays;
    private readonly Task _loop;

    // The loop writes these; the session gate reads the current behavior.
    private volatile GameSession? _session;
    private volatile RunningBehavior? _behavior;
    // Queued inputs can drain before Stopping, so handlers need the synchronous dispose signal.
    private volatile bool _stopping;

    // The loop writes these, and anyone reads them.
    private volatile CharacterState _state;
    private volatile bool _connected;
    private volatile bool _paused;
    private volatile bool _acting;
    private volatile bool _fightsAlone;
    private volatile ActivityRun? _activity;
    private volatile FightEnd? _lastFightEnd;
    private ImmutableHashSet<long> _noTeleportFrom = [];
    private ImmutableHashSet<int> _noNpcWays = [];
    private int _fightsBegun;
    private int _lastEndedFight;
    private long _placedAt;
    private long _useEndedAt = NeverEnded;
    private long _fightBeganAt = NeverEnded;
    private long _regeneratingSince = NeverEnded;

    /// <summary>Gets who the bot plays.</summary>
    public CharacterKey Key { get; }

    /// <summary>Gets the character's state now: an immutable snapshot, which the bot's loop replaces as messages come.</summary>
    public CharacterState State => _state;

    /// <summary>Gets whether a game connection plays the character now.</summary>
    public bool IsConnected => _connected;

    /// <summary>Gets whether the player took the character over from a behavior: the bot starts nothing until <see cref="Resume"/>.</summary>
    public bool IsPaused => _paused;

    /// <summary>Gets whether a behavior runs.</summary>
    public bool IsActing => _acting;

    /// <summary>Gets or sets whether the AI plays every fight of the character, whatever its activity says, off at first: the host's <c>Fights:Alone</c>, for the measures (D235, D244).</summary>
    public bool FightsAlone
    {
        get => _fightsAlone;
        set => _fightsAlone = value;
    }

    /// <summary>Gets how the character's last fight ended, once the server said, if it ever did.</summary>
    public FightEnd? LastFightEnd => _lastFightEnd;

    /// <summary>Gets the maps the server refused the character a teleport from since the bot was made, whatever their flag says (D248).</summary>
    public IReadOnlySet<long> NoTeleportFrom => Volatile.Read(ref _noTeleportFrom);

    /// <summary>Gets the NPCs' ways the character was refused since the bot was made, by index in <see cref="WorldPathfinder.NpcWays"/> (D253).</summary>
    public IReadOnlySet<int> NoNpcWays => Volatile.Read(ref _noNpcWays);

    /// <summary>Gets how many fights began since the bot was made: the last one's number.</summary>
    public int FightsBegun => Volatile.Read(ref _fightsBegun);

    /// <summary>Gets the number of the fight <see cref="LastFightEnd"/> ended, as <see cref="FightsBegun"/> counted it.</summary>
    public int LastEndedFight => Volatile.Read(ref _lastEndedFight);

    /// <summary>Gets whether the AI plays the character's fights now: the host's switch, or its activity's own fights as its profile says; out of an activity, the fights are the player's (D235, D244).</summary>
    public bool Fights => _fightsAlone || _activity is { IsOver: false, Activity.FightsItsFights: true };

    /// <summary>Gets the activity the bot runs, or ran last, if any.</summary>
    public ActivityRun? Activity => _activity;

    /// <summary>Gets what the bot did and met, its actions once over, for the app (D112).</summary>
    public RecentItems<JournalEntry> Journal { get; } = new(JournalCapacity);

    internal GameMaps Maps { get; }

    internal WorldPathfinder World => _world.Value;

    internal BankDirectory Banks => _banks.Value;

    internal TimeProvider Time { get; }

    internal ILogger Log => _log;

    /// <summary>Gets how long the bot's behaviors wait now, which the app may change while they run (D151).</summary>
    internal BotDelays Delays => _delays();

    /// <summary>Gets when the client has drawn the map the server last placed the character on.</summary>
    /// <remarks>Reckoned from the display time now: raised, it holds the requests to come on a map placed before.</remarks>
    internal DateTimeOffset DrawnAt => new DateTimeOffset(Volatile.Read(ref _placedAt), TimeSpan.Zero) + Delays.Display;

    /// <summary>Gets when the server last ended a use of the character's own, if it ever did.</summary>
    internal DateTimeOffset? UseEndedAt => Volatile.Read(ref _useEndedAt) is var ticks and not NeverEnded ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;

    /// <summary>Gets when the server last created a fight's context for the character, if it ever did.</summary>
    internal DateTimeOffset? FightBeganAt => Volatile.Read(ref _fightBeganAt) is var ticks and not NeverEnded ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;

    internal bool CanSendRawRequest(GameSession session, IMessage request)
    {
        if (!ReferenceEquals(session, _session))
            return !MovementRequests.IsMovementCapable(request);

        if (!MovementRequests.IsMovementCapable(request))
            return true;

        return request switch
        {
            MapMovementRequest => CanSendRawMovementRequest(),
            ObjectUseRequest or ObjectUseMultipleRequest => MovementGuard.Refuse(State, _connected) is MovementRefusal.None,
            NpcDialogReplyRequest => _connected
                && State.IsInDialog
                && State.IsPositionTrusted
                && !State.IsWalking
                && !State.IsWalkHeld
                && !State.IsFighting,
            _ => false,
        };
    }

    internal async Task SendFromBehaviorAsync(RunningBehavior behavior, IMessage request, CancellationToken cancellationToken)
    {
        if (request is MapMovementRequest)
            _movementSendBehavior.Value = behavior;

        try
        {
            await behavior.Session.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _movementSendBehavior.Value = null;
        }
    }

    private bool CanSendRawMovementRequest()
    {
        var sendingBehavior = _movementSendBehavior.Value;

        // A fight behavior may send a raw walk; an observer with the same session must not inherit that permission.
        _movementSendBehavior.Value = null;

        return State.IsFighting
            && _behavior is { IsFight: true } behavior
            && ReferenceEquals(behavior, sendingBehavior);
    }

    internal BotActor(CharacterState state, GameMaps maps, Lazy<WorldPathfinder> world, Lazy<BankDirectory> banks, Func<BotDelays> delays, TimeProvider time, ILogger log)
    {
        Key = state.Key;
        _state = state;
        Maps = maps;
        _world = world;
        _banks = banks;
        _delays = delays;
        Time = time;
        _log = log;
        _loop = Task.Run(ReadInboxAsync);
    }

    /// <summary>Runs a behavior, the only one while it runs.</summary>
    /// <typeparam name="T">What the behavior gives.</typeparam>
    /// <param name="behavior">The behavior: an async method reading the bot's state and sending requests through its context.</param>
    /// <param name="cancellationToken">Stops the behavior.</param>
    /// <returns>What the behavior gave.</returns>
    /// <exception cref="InvalidOperationException">No game connection plays the character, the player took it over, or a behavior runs.</exception>
    /// <exception cref="BotInterruptedException">The player acted, the game connection closed, a fight began, or the host is stopping.</exception>
    /// <exception cref="ObjectDisposedException">The bot was stopped.</exception>
    public Task<T> RunAsync<T>(Func<BotContext, CancellationToken, Task<T>> behavior, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        return RunBehaviorAsync(behavior, Readiness.Now, cancellationToken);
    }

    /// <summary>Lets the bot act again after the player took the character over.</summary>
    public void Resume()
    {
        Post(new Resuming());
    }

    /// <summary>Reckons the character's life at a time: the life the server last said, and a point each interval it regenerates since (D153).</summary>
    /// <param name="at">The time, by the bot's clock.</param>
    /// <returns>The life, at most the most; <see langword="null"/> while the server has not said it.</returns>
    /// <remarks>
    /// Measured on client 3.7.12.12: out of fights the life regenerates a point every interval the server gives, 5 tenths of
    /// a second in all captures, from the life the characteristics hold as the regeneration starts, up to the most.
    /// </remarks>
    public int? LifeAt(DateTimeOffset at)
    {
        var state = _state;

        if (state.Life is not { } life || state.MaxLife is not { } most)
            return state.Life;

        var since = Volatile.Read(ref _regeneratingSince);

        if (state.LifeRegenInterval is not { } interval || since == NeverEnded)
            return Math.Min(life, most);

        var points = (at - new DateTimeOffset(since, TimeSpan.Zero)).Ticks / (interval * TimeSpan.TicksPerMillisecond * 100);

        return (int)Math.Clamp(life + Math.Max(0, points), 0, most);
    }

    /// <summary>Remembers that the server refused the character a teleport from a map, so that no journey teleports from it again (D248).</summary>
    /// <param name="mapId">The map.</param>
    internal void RefuseTeleportFrom(long mapId)
    {
        ImmutableInterlocked.Update(ref _noTeleportFrom, (maps, map) => maps.Add(map), mapId);
    }

    /// <summary>Remembers that an NPC's dialog did not offer the character its way, so that no journey takes it again (D253).</summary>
    /// <param name="way">The way, by index in <see cref="WorldPathfinder.NpcWays"/>.</param>
    internal void RefuseNpcWay(int way)
    {
        ImmutableInterlocked.Update(ref _noNpcWays, (ways, refused) => ways.Add(refused), way);
    }

    /// <summary>Pauses the bot as the player's hand does, such as after a fight an activity's settings pause it after: it starts nothing until <see cref="Resume"/> (D144).</summary>
    internal void Pause()
    {
        Post(new Pausing());
    }

    /// <summary>Starts an activity: its steps, one after another, until it is done, fails or is stopped (D87).</summary>
    /// <param name="activity">The activity.</param>
    /// <param name="title">The name it is started under, such as its profile's, which the app shows.</param>
    /// <returns>The activity's run, which says how it goes and stops it.</returns>
    /// <exception cref="InvalidOperationException">The bot already runs an activity.</exception>
    /// <exception cref="ObjectDisposedException">The bot was stopped.</exception>
    public ActivityRun StartActivity(BotActivity activity, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(activity);

        lock (_activityLock)
        {
            ObjectDisposedException.ThrowIf(_inbox.Reader.Completion.IsCompleted, this);

            if (_activity is { IsOver: false })
                throw new InvalidOperationException("The bot already runs an activity.");

            var run = new ActivityRun(this, activity, title);

            _activity = run;
            run.Start();

            return run;
        }
    }

    /// <summary>Waits until a condition holds of the bot, tried by its loop now and after everything it handles.</summary>
    /// <param name="condition">Tells the bot as it is: its state, connection and pause. It must be quick, and must not wait.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes once the condition held.</returns>
    /// <exception cref="ObjectDisposedException">The bot was stopped.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public Task WaitUntilAsync(Func<BotActor, bool> condition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var waiter = new Waiter(condition, cancellationToken);

        if (!_inbox.Writer.TryWrite(new Waiting(waiter)))
            waiter.Fail(new ObjectDisposedException(nameof(BotActor), "The bot was stopped."));

        return waiter.Task;
    }

    internal void BeginStopping()
    {
        _stopping = true;
    }

    /// <summary>Stops the bot: its behavior is interrupted, and its inbox read to the end.</summary>
    /// <returns>A task that completes once the bot's loop is over.</returns>
    public async ValueTask DisposeAsync()
    {
        BeginStopping();
        _session = null;
        _connected = false;
        Post(new Stopping());
        _inbox.Writer.TryComplete();
        await _loop.ConfigureAwait(false);
    }

    /// <summary>Runs a behavior once the bot can act: a game connection plays the character, placed on a map and out of a fight, the player has not taken it over, and no behavior runs.</summary>
    /// <remarks>An activity's steps go this way, so that one waits, without a delay, for the client to come back, the player to resume the bot, or a fight to end.</remarks>
    internal Task<T> RunWhenReadyAsync<T>(Func<BotContext, CancellationToken, Task<T>> behavior, CancellationToken cancellationToken)
    {
        return RunBehaviorAsync(behavior, Readiness.OutOfFight, cancellationToken);
    }

    /// <summary>Runs a behavior once the bot can act in a fight: a game connection plays the character, in a fight not over, the player has not taken it over, and no behavior runs.</summary>
    /// <remarks>The AI's fight goes this way (D202): a step a fight cut ends first, and the player's hand in the fight interrupts it.</remarks>
    internal Task<T> RunInFightAsync<T>(Func<BotContext, CancellationToken, Task<T>> behavior, CancellationToken cancellationToken)
    {
        return RunBehaviorAsync(behavior, Readiness.InFight, cancellationToken);
    }

    internal void Attach(GameSession session, CharacterState state)
    {
        Post(new Attached(session, state));
    }

    internal void See(GameSession session, RelayDirection side, IMessage message)
    {
        Post(new Seen(session, side, message));
    }

    internal void Detach(GameSession session)
    {
        Post(new Detached(session));
    }

    /// <summary>Waits until the loop handled everything posted before, so that a test reads the state they made.</summary>
    internal Task ProcessedAsync()
    {
        var processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_inbox.Writer.TryWrite(new Processing(processed)))
            processed.TrySetResult();

        return processed.Task;
    }

    /// <summary>Has the loop take the walk's last cell as where the character stands, once the client did not confirm it (D85).</summary>
    internal Task SettleWalkAsync()
    {
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_inbox.Writer.TryWrite(new Settling(settled)))
            settled.TrySetResult();

        return settled.Task;
    }

    internal void MarkMovementRequested(long? mapId)
    {
        Post(new MovementRequested(mapId));
    }

    internal GameExpectation Expect(RunningBehavior owner, Func<RelayDirection, IMessage, CharacterState, bool> match)
    {
        var expectation = new GameExpectation(match, owner, Time);

        if (!_inbox.Writer.TryWrite(new Expecting(expectation)))
            expectation.Cancel();

        return expectation;
    }

    private async Task<T> RunBehaviorAsync<T>(Func<BotContext, CancellationToken, Task<T>> behavior, Readiness readiness, CancellationToken cancellationToken)
    {
        var started = new TaskCompletionSource<RunningBehavior>(TaskCreationOptions.RunContinuationsAsynchronously);

        // A start that waits may be cancelled before the bot can act; the loop then never starts it (OnStarting).
        using var registration = readiness is not Readiness.Now ? cancellationToken.Register(() => started.TrySetCanceled(cancellationToken)) : default;

        var posted = _inbox.Writer.TryWrite(new Starting(started, readiness, cancellationToken));

        ObjectDisposedException.ThrowIf(!posted, this);

        var running = await started.Task.ConfigureAwait(false);

        try
        {
            return await behavior(new BotContext(this, running), running.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (running.Reason is { } reason && exception is not BotInterruptedException)
        {
            // Whatever the behavior met once interrupted, such as a request that could not go, follows from the interruption.
            BotLog.Interrupted(_log, Key.CharacterId, reason);

            throw new BotInterruptedException(reason, exception);
        }
        finally
        {
            Post(new Finished(running));
        }
    }

    private void Post(Input input)
    {
        // Once the bot is stopped, what comes is dropped: no behavior runs any more to need it.
        _inbox.Writer.TryWrite(input);
    }

    private async Task ReadInboxAsync()
    {
        await foreach (var input in _inbox.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                Handle(input);
                Wake();
            }
            catch (Exception exception)
            {
                // A message the state handlers fail on must not stop the bot from seeing the next.
                BotLog.HandlerFailed(_log, Key.CharacterId, exception);
            }
        }
    }

    private void Handle(Input input)
    {
        switch (input)
        {
            case Seen seen:
                OnSeen(seen.Session, seen.Side, seen.Message);
                break;
            case Expecting expecting:
                OnExpecting(expecting.Expectation);
                break;
            case Starting starting:
                OnStarting(starting);
                break;
            case Waiting waiting:
                _waiters.Add(waiting.Waiter);
                break;
            case Finished finished:
                OnFinished(finished.Behavior);
                break;
            case Resuming:
                OnResuming();
                break;
            case Pausing:
                OnPausing();
                break;
            case Attached attached:
                OnAttached(attached.Session, attached.State);
                break;
            case Detached detached:
                OnDetached(detached.Session);
                break;
            case Stopping:
                OnStopping();
                break;
            case Settling settling:
                {
                    var before = _state;
                    var settled = CharacterEvents.NotConfirmed(before);

                    _state = settled;

                    if (before.IsWalking && settled.MapId is { } map && settled.Cell is { } cell)
                        BotLog.WalkSettled(_log, Key.CharacterId, map, cell);

                    settling.Settled.TrySetResult();
                    break;
                }
            case Processing processing:
                processing.Processed.TrySetResult();
                break;
            case MovementRequested movement:
                if (movement.MapId is null || movement.MapId == _state.MapId)
                    _state = _state with { PendingMovementMapId = movement.MapId };
                break;
        }
    }

    private void OnSeen(GameSession session, RelayDirection side, IMessage message)
    {
        if (_stopping || session != _session)
            return;

        var before = _state;
        var state = CharacterEvents.Apply(before, side, message);

        _state = state;

        LogPositionTransition(before, state, side, message);

        if (before.Use is not null && state.Use is null && message is InteractiveUseEndedEvent)
            Volatile.Write(ref _useEndedAt, Time.GetUtcNow().UtcTicks);

        if (before.Fight?.End is null && state.Fight?.End is { } end)
        {
            _lastFightEnd = end;
            Volatile.Write(ref _lastEndedFight, Volatile.Read(ref _fightsBegun));
            Journal.Add(new FightEndEntry(Time.GetUtcNow(), end.Outcome, end.Experience, end.Kamas, end.Items.Values.Sum()));
        }

        if (side is RelayDirection.FromServer && message is LifeRegenStartEvent)
            Volatile.Write(ref _regeneratingSince, Time.GetUtcNow().UtcTicks);

        if (side is RelayDirection.FromServer && message is MapInformationEvent information && information.MapId == state.MapId && state.Cell is not null)
            Volatile.Write(ref _placedAt, Time.GetUtcNow().UtcTicks);

        // A request to leave changes nothing of the state, so this is the dialog the client sent it in.
        if (side is RelayDirection.FromClient && _behavior is { } behavior && PlayerActions.Starts(state, message))
        {
            // The player's first request pauses the bot; those that come with it, such as a walk after its cancelling, add nothing.
            if (!_paused)
                Write(BotEvent.PausedByPlayer);

            _paused = true;
            Interrupt(behavior, Interruption.Player);
            BotLog.PausedByPlayer(_log, Key.CharacterId, message.Descriptor.Name);
        }

        if (side is RelayDirection.FromServer && message is ContextCreationEvent { Context: ContextCreationEvent.Types.GameContext.Fight })
        {
            Volatile.Write(ref _fightBeganAt, Time.GetUtcNow().UtcTicks);
            Interlocked.Increment(ref _fightsBegun);
            Write(BotEvent.FightBegan);

            // The client walks the character in a fight with the requests a behavior sends out of one: none may go on (D88).
            if (_behavior is { } fighting)
            {
                Interrupt(fighting, Interruption.Fight);
                BotLog.FightBegan(_log, Key.CharacterId);
            }
        }

        foreach (var expectation in _expectations)
            expectation.TryMatch(side, message, state);

        _expectations.RemoveAll(expectation => expectation.IsDone);
    }

    private void LogPositionTransition(CharacterState before, CharacterState state, RelayDirection side, IMessage message)
    {
        if (side is RelayDirection.FromServer)
        {
            switch (message)
            {
                case MapCurrentEvent current when state.MapId == current.MapId:
                    if (before.MapId is null)
                        BotLog.MapKnown(_log, Key.CharacterId, current.MapId);
                    else if (before.MapId == state.MapId && PositionChanged(before, state))
                        BotLog.MapRepeated(_log, Key.CharacterId, current.MapId);
                    else if (before.MapId != state.MapId && state.MapId is { } changedTo)
                        BotLog.MapChanged(_log, Key.CharacterId, before.MapId.Value, changedTo);

                    LogPositionUnknownIfCleared(before, state);
                    return;

                case MapInformationEvent information when state.MapId == information.MapId:
                    if (state.Cell is { } placed && before.Cell != placed)
                        BotLog.Placed(_log, Key.CharacterId, information.MapId, placed);
                    else
                        LogPositionUnknownIfCleared(before, state);

                    return;

                case MapMovementEvent movement when movement.ActorId == Key.CharacterId:
                    LogSelfWalk(before, state, movement);
                    return;

                case MapMovementConfirmResponse when before.IsWalking && !state.IsWalking && state.MapId is { } confirmedMap && state.Cell is { } confirmedCell:
                    BotLog.WalkConfirmed(_log, Key.CharacterId, confirmedMap, confirmedCell);
                    return;

                case MapMovementRefusedEvent when before.Cell is not null && before.MapId is { } refusedMap && before.PendingMovementMapId == refusedMap:
                    if (state.Cell is { } refusedCell)
                        BotLog.WalkRefused(_log, Key.CharacterId, refusedMap, refusedCell);
                    else
                        BotLog.WalkRefusedUnknown(_log, Key.CharacterId, refusedMap);

                    return;
            }
        }
        else if (side is RelayDirection.FromClient && message is MapMovementCancelRequest)
        {
            if (!before.IsWalking)
                BotLog.WalkCancelIgnored(_log, Key.CharacterId);
            else if (state.MapId is { } map && state.Cell is { } cell && !state.IsWalking)
                BotLog.WalkCancelled(_log, Key.CharacterId, map, cell);
            else
                BotLog.WalkCancelInvalid(_log, Key.CharacterId);
        }
    }

    private void LogSelfWalk(CharacterState before, CharacterState state, MapMovementEvent movement)
    {
        if (before.Cell is { } from
            && movement.Cells.Count > 0
            && movement.Cells.All(cell => cell >= 0 && cell < MapData.CellCount)
            && movement.Cells[0] == from
            && state.Path.SequenceEqual(movement.Cells))
        {
            if (!before.Path.SequenceEqual(state.Path) && state.MapId is { } map)
                BotLog.WalkShown(_log, Key.CharacterId, map, from, state.Path[^1], state.Path.Length - 1);

            return;
        }

        if (before.MapId is not { } knownMap)
        {
            BotLog.WalkIgnoredUnplaced(_log, Key.CharacterId);
            return;
        }

        if (before.Cell is null)
        {
            BotLog.WalkIgnoredUnplacedOnMap(_log, Key.CharacterId, knownMap);
            return;
        }

        if (movement.Cells.Count is 0 || movement.Cells.Any(cell => cell < 0 || cell >= MapData.CellCount))
        {
            BotLog.WalkIgnoredInvalid(_log, Key.CharacterId, knownMap);
            return;
        }

        if (movement.Cells[0] != before.Cell)
        {
            BotLog.WalkIgnoredStartMismatch(_log, Key.CharacterId, knownMap);
            return;
        }

        BotLog.WalkIgnoredInvalid(_log, Key.CharacterId, knownMap);
    }

    private void LogPositionUnknownIfCleared(CharacterState before, CharacterState state)
    {
        if (before.Cell is not null && state.Cell is null && state.MapId is { } map)
            BotLog.PositionUnknown(_log, Key.CharacterId, map);
    }

    private static bool PositionChanged(CharacterState before, CharacterState state)
    {
        return before.MapId != state.MapId
            || before.Cell != state.Cell
            || before.IsWalkHeld != state.IsWalkHeld
            || !before.Path.SequenceEqual(state.Path);
    }

    private void OnExpecting(GameExpectation expectation)
    {
        if (expectation.Owner == _behavior && expectation.Owner.Reason is null)
            _expectations.Add(expectation);
        else
            expectation.Cancel();
    }

    private void OnStarting(Starting starting)
    {
        var started = starting.Started;

        if (_stopping)
        {
            started.TrySetException(new BotInterruptedException(Interruption.Stopped));

            return;
        }

        if (starting.Readiness is not Readiness.Now)
        {
            if (CanAct(starting.Readiness))
                Start(starting);
            else if (!started.Task.IsCompleted)
                _parked.Add(starting);
        }
        else if (_session is null)
        {
            started.TrySetException(new InvalidOperationException("No game connection plays the bot's character."));
        }
        else if (_paused)
        {
            started.TrySetException(new InvalidOperationException("The player took the character over: resume the bot first."));
        }
        else if (_behavior is not null)
        {
            started.TrySetException(new InvalidOperationException("The bot already runs a behavior."));
        }
        else
        {
            Start(starting);
        }
    }

    private void Start(Starting starting)
    {
        var behavior = new RunningBehavior(_session!, starting.Readiness is Readiness.InFight, starting.CancellationToken);

        // A start that waited may have been cancelled meanwhile: then no behavior runs.
        if (!starting.Started.TrySetResult(behavior))
        {
            behavior.Release();

            return;
        }

        _behavior = behavior;
        _acting = true;
    }

    private bool CanAct(Readiness readiness)
    {
        if (_session is null || _paused || _behavior is not null)
            return false;

        return readiness is Readiness.InFight
            ? _state.Fight is { Phase: not FightPhase.Ended }
            : _state is { MapId: not null, Cell: not null, IsFighting: false };
    }

    private void Wake()
    {
        // Every waiter is tried, so that one whose condition throws fails alone.
        foreach (var waiter in _waiters)
            waiter.Try(this);

        _waiters.RemoveAll(waiter => waiter.IsDone);
        _parked.RemoveAll(starting => starting.Started.Task.IsCompleted);

        // The first parked start that can take the bot does; the others wait for the behavior it starts to finish.
        var first = _parked.FindIndex(starting => CanAct(starting.Readiness));

        if (first >= 0)
        {
            var starting = _parked[first];

            _parked.RemoveAt(first);
            Start(starting);
        }
    }

    private void OnFinished(RunningBehavior behavior)
    {
        if (behavior == _behavior)
        {
            CancelExpectations(behavior);
            _behavior = null;
            _acting = false;
        }

        behavior.Release();
    }

    private void OnResuming()
    {
        if (!_paused)
            return;

        _paused = false;
        BotLog.Resumed(_log, Key.CharacterId);
        Write(BotEvent.Resumed);
    }

    private void OnPausing()
    {
        if (_paused)
            return;

        _paused = true;
        BotLog.PausedAfterFight(_log, Key.CharacterId);
        Write(BotEvent.PausedAfterFight);
    }

    private void OnAttached(GameSession session, CharacterState state)
    {
        if (_stopping)
            return;

        if (_behavior is { } behavior)
            Interrupt(behavior, Interruption.Disconnected);

        _session = session;
        _state = state;
        _connected = true;

        if (state.MapId is { } map && state.Cell is { } cell)
            BotLog.AttachedAt(_log, Key.CharacterId, session.Id, map, cell);
        else if (state.MapId is { } knownMap)
            BotLog.AttachedOnMap(_log, Key.CharacterId, session.Id, knownMap);
        else
            BotLog.AttachedUnknown(_log, Key.CharacterId, session.Id);

        Write(BotEvent.Connected);
    }

    private void OnDetached(GameSession session)
    {
        if (session != _session)
            return;

        if (_behavior is { } behavior)
            Interrupt(behavior, Interruption.Disconnected);

        _session = null;
        _connected = false;

        if (_state.MapId is { } map && _state.Cell is { } cell)
            BotLog.DetachedAt(_log, Key.CharacterId, session.Id, map, cell);
        else if (_state.MapId is { } knownMap)
            BotLog.DetachedOnMap(_log, Key.CharacterId, session.Id, knownMap);
        else
            BotLog.DetachedUnknown(_log, Key.CharacterId, session.Id);

        Write(BotEvent.Disconnected);
    }

    private void OnStopping()
    {
        if (_behavior is { } behavior)
            Interrupt(behavior, Interruption.Stopped);

        foreach (var starting in _parked)
            starting.Started.TrySetException(new BotInterruptedException(Interruption.Stopped));

        foreach (var waiter in _waiters)
            waiter.Fail(new ObjectDisposedException(nameof(BotActor), "The bot was stopped."));

        _parked.Clear();
        _waiters.Clear();
        _session = null;
        _connected = false;
    }

    private void Write(BotEvent bot)
    {
        Journal.Add(new BotEventEntry(Time.GetUtcNow(), bot));
    }

    private void Interrupt(RunningBehavior behavior, Interruption reason)
    {
        behavior.Interrupt(reason);
        CancelExpectations(behavior);
    }

    private void CancelExpectations(RunningBehavior behavior)
    {
        foreach (var expectation in _expectations.Where(expectation => expectation.Owner == behavior))
            expectation.Cancel();

        _expectations.RemoveAll(expectation => expectation.IsDone);
    }

    private abstract record Input;

    private sealed record Seen(GameSession Session, RelayDirection Side, IMessage Message) : Input;

    private sealed record Expecting(GameExpectation Expectation) : Input;

    private sealed record Starting(TaskCompletionSource<RunningBehavior> Started, Readiness Readiness, CancellationToken CancellationToken) : Input;

    private sealed record Waiting(Waiter Waiter) : Input;

    private sealed record Finished(RunningBehavior Behavior) : Input;

    private sealed record Resuming : Input;

    private sealed record Pausing : Input;

    private sealed record Attached(GameSession Session, CharacterState State) : Input;

    private sealed record Detached(GameSession Session) : Input;

    private sealed record Stopping : Input;

    private sealed record Settling(TaskCompletionSource Settled) : Input;

    private sealed record Processing(TaskCompletionSource Processed) : Input;

    private sealed record MovementRequested(long? MapId) : Input;

    /// <summary>When a behavior may start.</summary>
    private enum Readiness
    {
        /// <summary>Now, or never: it fails when the bot cannot act.</summary>
        Now,

        /// <summary>Once the bot can act on a map, out of a fight.</summary>
        OutOfFight,

        /// <summary>Once the bot can act in a fight not over.</summary>
        InFight,
    }

    /// <summary>A condition someone waits for, tried by the bot's loop.</summary>
    private sealed class Waiter
    {
        private readonly Func<BotActor, bool> _condition;
        private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration _registration;

        public Task Task => _held.Task;

        public bool IsDone => _held.Task.IsCompleted;

        public Waiter(Func<BotActor, bool> condition, CancellationToken cancellationToken)
        {
            _condition = condition;
            _registration = cancellationToken.Register(() => _held.TrySetCanceled(cancellationToken));
        }

        public void Try(BotActor bot)
        {
            if (IsDone)
                return;

            bool held;

            try
            {
                held = _condition(bot);
            }
            catch (Exception exception)
            {
                Fail(exception);

                return;
            }

            if (held && _held.TrySetResult())
                _registration.Dispose();
        }

        public void Fail(Exception exception)
        {
            if (_held.TrySetException(exception))
                _registration.Dispose();
        }
    }
}
