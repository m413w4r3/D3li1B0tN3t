using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Journal;
using D3B.Data.Maps;
using D3B.Engine.World;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Runtime;

/// <summary>What a behavior sees of its bot: the character's state, the game's data, the clock, and the game connection to act through (D9).</summary>
/// <remarks>
/// A behavior reads snapshots of the state and never changes it: the server's answers change it, through the bot's loop,
/// but for a walk the client did not confirm (<see cref="SettleWalkAsync"/>). It sends the requests that start an action
/// (D60), expecting their answers first (<see cref="Expect"/>).
/// </remarks>
public sealed class BotContext
{
    private readonly BotActor _bot;
    private readonly RunningBehavior _behavior;

    // Only the behavior's own sequence of calls begins and ends actions.
    private int _actions;

    /// <summary>Gets the character's state now: a snapshot, which the next message the bot sees replaces.</summary>
    public CharacterState State => _bot.State;

    internal bool IsConnected => _bot.IsConnected;

    internal bool IsFightBehavior => _behavior.IsFight;

    internal bool CanSendDialogReply => IsConnected
        && State.IsInDialog
        && State.IsPositionTrusted
        && !State.IsWalking
        && !State.IsWalkHeld
        && !State.IsFighting;

    /// <summary>Gets the maps of the installed build's pack.</summary>
    public GameMaps Maps => _bot.Maps;

    /// <summary>Gets the paths through the world of the installed build's pack, and the cells each map is left from.</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no world graph or atlas.</exception>
    public WorldPathfinder World => _bot.World;

    /// <summary>Gets the banks of the installed build's pack, their insides, and what the host's bots saw of them (D79).</summary>
    /// <exception cref="KeyNotFoundException">The pack holds no bank catalog, world graph or atlas.</exception>
    public BankDirectory Banks => _bot.Banks;

    /// <summary>Gets the clock every delay goes through.</summary>
    public TimeProvider Time => _bot.Time;

    /// <summary>Gets when the server last ended a use of the character's own, such as a harvest, by the bot's clock, if it ever did: a fight beginning right after is a resource's protector's (D144).</summary>
    public DateTimeOffset? UseEndedAt => _bot.UseEndedAt;

    /// <summary>Gets when the server last began a fight of the character's, by the bot's clock, if it ever did: a resource's protector's begins right after a use ends (D144).</summary>
    public DateTimeOffset? FightBeganAt => _bot.FightBeganAt;

    /// <summary>Gets the character's life now, its regeneration since the server said it reckoned (D153); <see langword="null"/> while the server has not said it.</summary>
    public int? Life => _bot.LifeAt(Time.GetUtcNow());

    /// <summary>Gets how long behaviors wait now: the client's display time, the server's answers and the rest, which the user may raise (D151).</summary>
    public BotDelays Delays => _bot.Delays;

    /// <summary>Gets the maps the server refused the character a teleport from, which no journey teleports from again (D248).</summary>
    public IReadOnlySet<long> NoTeleportFrom => _bot.NoTeleportFrom;

    /// <summary>Gets the NPCs' ways the character was refused, which no journey takes again (D253).</summary>
    public IReadOnlySet<int> NoNpcWays => _bot.NoNpcWays;

    /// <summary>Remembers that the server refused a teleport from a map, whatever its flag says (D248).</summary>
    /// <param name="mapId">The map.</param>
    public void RefuseTeleportFrom(long mapId)
    {
        _bot.RefuseTeleportFrom(mapId);
    }

    /// <summary>Remembers that an NPC's dialog did not offer the character its way (D253).</summary>
    /// <param name="way">The way, by index in <see cref="WorldPathfinder.NpcWays"/>.</param>
    public void RefuseNpcWay(int way)
    {
        _bot.RefuseNpcWay(way);
    }

    /// <summary>Gets how many map changes a teleport counts as on a journey: as the running activity says, 1 without one (D167).</summary>
    public int TeleportChanges => _bot.Activity?.Activity.TeleportChanges ?? 1;

    /// <summary>Gets the bot's log.</summary>
    public ILogger Log => _bot.Log;

    internal BotContext(BotActor bot, RunningBehavior behavior)
    {
        _bot = bot;
        _behavior = behavior;
    }

    /// <summary>Expects a message, before sending the request it answers.</summary>
    /// <param name="match">Tells the message: the side that sent it, the message, and the state once it took the message in.</param>
    /// <returns>The expectation, to wait on once the request is sent.</returns>
    public GameExpectation Expect(Func<RelayDirection, IMessage, CharacterState, bool> match)
    {
        ArgumentNullException.ThrowIfNull(match);

        return _bot.Expect(_behavior, match);
    }

    /// <summary>Waits until the character's state holds a condition, tried now and after every message the bot sees, at most a delay by the bot's clock.</summary>
    /// <param name="condition">Tells the state. It must be quick, and must not wait.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>Whether the condition held before the delay passed.</returns>
    public async Task<bool> WaitUntilAsync(Func<CharacterState, bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(condition);

        using var delay = new CancellationTokenSource(timeout, _bot.Time);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, delay.Token);

        try
        {
            await _bot.WaitUntilAsync(bot => condition(bot.State), either.Token).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException) when (delay.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Has the state take the last cell of the walk the character is in, once the client did not confirm it: the server has the character there (D85).</summary>
    /// <returns>A task that completes once the state took it, so that the behavior reads it.</returns>
    public Task SettleWalkAsync()
    {
        return _bot.SettleWalkAsync();
    }

    /// <summary>Sends the game server a request through the game connection the behavior started on (D60), once the client drew the map.</summary>
    /// <param name="request">The readable request, whole.</param>
    /// <param name="cancellationToken">Stops waiting; a request not gone yet then never goes.</param>
    /// <returns>A task that completes once the request went.</returns>
    /// <exception cref="InvalidOperationException">The request is not a fight walk from a fight behavior, or is a raw map-transition request; use a guarded movement action.</exception>
    /// <remarks>
    /// The client follows what the server shows it only once it has drawn the map, its display time after the server
    /// placed the character on it, half a second measured (<see cref="BotDelays.Display"/>): a walk shown 1 or 50 ms after
    /// was never confirmed, and a door used before the client asked what is on the new map left it no longer confirming
    /// walks (D73, D77). A request sent sooner waits until then. Outside fights, raw <see cref="MapMovementRequest"/>,
    /// <see cref="MapChangeRequest"/>, <see cref="TeleportRequest"/>, <see cref="HavenBagEnterRequest"/>,
    /// <see cref="InteractiveUseRequest"/>, and <see cref="NpcActionRequest"/> requests are prohibited; use the guarded
    /// movement actions instead. Raw fight movement is allowed only from a fight behavior while
    /// <see cref="CharacterState.IsFighting"/> is true. Dialog replies are allowed only while the session's send gate
    /// sees a trusted, connected dialog state.
    /// </remarks>
    public async Task SendAsync(IMessage request, CancellationToken cancellationToken)
    {
        EnsureRawMovementSendAllowed(request);

        // Again once waited: the display time raised meanwhile, or a new map placed, holds the request longer.
        await WaitUntilDrawnAsync(cancellationToken).ConfigureAwait(false);

        EnsureRawMovementSendAllowed(request);
        await _bot.SendFromBehaviorAsync(_behavior, request, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<bool> SendGuardedAsync(IMessage request, Func<bool> canSend, CancellationToken cancellationToken, Action? beforeSend = null)
    {
        ArgumentNullException.ThrowIfNull(canSend);

        // Keep the request's source map while the client's display wait is in progress.
        var requestedMapId = State.MapId;

        await WaitUntilDrawnAsync(cancellationToken).ConfigureAwait(false);
        await _bot.ProcessedAsync().ConfigureAwait(false);

        if (!canSend())
        {
            MarkMovementRequested(null);
            return false;
        }

        beforeSend?.Invoke();

        if (AwaitsMovementAnswer(request))
            MarkMovementRequested(RequestedMovementMap(request, requestedMapId));

        // This bypasses the session gate; every caller must supply a current movement guard that this method cannot verify.
        try
        {
            await _behavior.Session.SendUnguardedAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            MarkMovementRequested(null);
            throw;
        }

        return true;
    }

    internal void MarkMovementRequested(long? mapId)
    {
        _bot.MarkMovementRequested(mapId);
    }

    /// <summary>Begins an action the journal writes once over, within the actions begun before it and not over (D112).</summary>
    internal JournalAction BeginAction()
    {
        return new JournalAction(this, _actions++, _bot.Time.GetUtcNow());
    }

    private async Task WaitUntilDrawnAsync(CancellationToken cancellationToken)
    {
        for (var wait = _bot.DrawnAt - _bot.Time.GetUtcNow(); wait > TimeSpan.Zero; wait = _bot.DrawnAt - _bot.Time.GetUtcNow())
            await Task.Delay(wait, _bot.Time, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureRawMovementSendAllowed(IMessage request)
    {
        if (request is MapMovementRequest)
        {
            if (State.IsFighting && IsFightBehavior)
                return;

            throw new InvalidOperationException("Automated movement requests must use the guarded movement actions.");
        }

        if (request is MapChangeRequest or TeleportRequest or HavenBagEnterRequest or InteractiveUseRequest or NpcActionRequest)
            throw new InvalidOperationException("Automated movement requests must use the guarded movement actions.");
    }

    private static bool AwaitsMovementAnswer(IMessage request)
    {
        return request is MapMovementRequest or MapChangeRequest or TeleportRequest or HavenBagEnterRequest;
    }

    private static long? RequestedMovementMap(IMessage request, long? currentMapId)
    {
        return request is MapMovementRequest movement ? movement.MapId : currentMapId;
    }

    internal void EndAction()
    {
        _actions--;
    }

    /// <summary>Writes an action once over to the bot's journal, timed by the bot's clock.</summary>
    internal void Record(JournalAction action, Func<DateTimeOffset, TimeSpan, JournalEntry> entry)
    {
        var now = _bot.Time.GetUtcNow();

        _bot.Journal.Add(entry(now, now - action.Started));
    }
}
