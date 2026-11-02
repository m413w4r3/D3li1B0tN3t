using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Movement;
using D3B.Bot.Network;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Tactics;
using D3B.Engine.World;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Runtime;

/// <summary>Every bot of the host, one per character a game connection chose, fed by every game connection D3B takes (D9, D65).</summary>
/// <remarks>
/// <para>
/// An observer of the game (D62): a game connection becomes its character's once the server confirms the character the
/// client chose, its account being the one the connection phase identified (<see cref="GameSession.Account"/>). The
/// bot of that account's character is then made, or found when the client connected before, and gets every message the
/// connection carries from then on, posted to its inbox.
/// </para>
/// <para>
/// A connection whose account D3B does not know, such as one whose identification was not understood, is only watched:
/// no bot plays its character.
/// </para>
/// </remarks>
public sealed class Bots : IGameObserver, IAsyncDisposable
{
    private readonly GameMaps _maps;
    private readonly Lazy<WorldPathfinder> _world;
    private readonly Lazy<BankDirectory> _banks;
    private readonly Lazy<FightTactics> _tactics;
    private readonly GameTables _tables;
    private readonly TimeProvider _time;
    private readonly ILoggerFactory _logs;
    private readonly ILogger _log;
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<CharacterKey, BotActor> _bots = new();
    private readonly ConcurrentDictionary<GameSession, Binding> _sessions = new();
    private readonly ConcurrentDictionary<CharacterKey, FightPilot> _pilots = new();
    private volatile BotDelays _delays = BotDelays.Measured;
    private volatile bool _fightsAlone;

    /// <summary>Gets every bot, those whose game connection closed included.</summary>
    public IReadOnlyCollection<BotActor> All => [.. _bots.Values];

    /// <summary>Gets or sets how long every bot's behaviors wait: set, the next wait of each bot reads it, those running included (D151).</summary>
    public BotDelays Delays
    {
        get => _delays;
        set => _delays = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets or sets whether the AI plays every fight of the bots made from now on, whatever their activity says: the host's <c>Fights:Alone</c> (D198, D235, D244).</summary>
    public bool FightsAlone
    {
        get => _fightsAlone;
        set => _fightsAlone = value;
    }

    /// <summary>Raised when a character's first game connection makes its bot, from the relay's read loop: a handler must be quick.</summary>
    public event EventHandler<BotActor>? Added;

    /// <summary>Initializes a new instance of the <see cref="Bots"/> class.</summary>
    /// <param name="maps">The maps of the installed build's pack, which behaviors walk.</param>
    /// <param name="world">The world of that pack, which behaviors travel: its graph is read when a bot first travels.</param>
    /// <param name="tables">The game tables of that pack: its banks are read when a bot first banks.</param>
    /// <param name="time">The clock every bot's delays go through.</param>
    /// <param name="logs">The host's logs.</param>
    /// <remarks>Journeys go through the NPCs' ways measured in game (<see cref="NpcWays.Measured"/>, D253).</remarks>
    public Bots(GameMaps maps, GameWorld world, GameTables tables, TimeProvider time, ILoggerFactory logs)
        : this(maps, world, tables, time, logs, NpcWays.Measured)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Bots"/> class, whose journeys go through other NPCs' ways.</summary>
    /// <param name="maps">The maps of the installed build's pack, which behaviors walk.</param>
    /// <param name="world">The world of that pack, which behaviors travel: its graph is read when a bot first travels.</param>
    /// <param name="tables">The game tables of that pack: its banks are read when a bot first banks.</param>
    /// <param name="time">The clock every bot's delays go through.</param>
    /// <param name="logs">The host's logs.</param>
    /// <param name="npcWays">The NPCs' ways journeys may go through (D253).</param>
    public Bots(GameMaps maps, GameWorld world, GameTables tables, TimeProvider time, ILoggerFactory logs, IReadOnlyList<NpcWay> npcWays)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logs);
        ArgumentNullException.ThrowIfNull(npcWays);

        _maps = maps;
        _world = new Lazy<WorldPathfinder>(() => new WorldPathfinder(world.Graph, world.Atlas, npcWays), LazyThreadSafetyMode.PublicationOnly);

        // One per host, so that what a bot learns of the insides serves every other (D79).
        _banks = new Lazy<BankDirectory>(() => new BankDirectory(tables.Banks, world.Graph, world.Atlas), LazyThreadSafetyMode.ExecutionAndPublication);

        // One per host, built at the first fight: its catalogs are the pack's, shared by every bot's AI (D201).
        _tactics = new Lazy<FightTactics>(() => new FightTactics(tables), LazyThreadSafetyMode.ExecutionAndPublication);
        _tables = tables;
        _time = time;
        _logs = logs;
        _log = logs.CreateLogger<Bots>();
    }

    /// <summary>Finds the bot of a character.</summary>
    /// <param name="key">The character.</param>
    /// <param name="bot">Its bot, when a game connection chose it.</param>
    /// <returns>Whether the character has a bot.</returns>
    public bool TryGet(CharacterKey key, [NotNullWhen(true)] out BotActor? bot)
    {
        return _bots.TryGetValue(key, out bot);
    }

    /// <summary>Stops every bot.</summary>
    /// <returns>A task that completes once every bot's loop is over.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (var bot in _bots.Values)
            await bot.DisposeAsync().ConfigureAwait(false);

        foreach (var pilot in _pilots.Values)
            await pilot.Run.ConfigureAwait(false);
    }

    void IGameObserver.OnSessionStarted(GameSession session)
    {
        var binding = new Binding();

        _sessions[session] = binding;

        // Runs at once when the connection already closed.
        session.Closed.Register(() => Close(session));
    }

    void IGameObserver.OnFrame(GameSession session, RelayDirection direction, DecodedGameFrame frame)
    {
        if (!frame.IsRead || !_sessions.TryGetValue(session, out var binding))
            return;

        if (binding.Bot is { } bot)
            bot.See(session, direction, frame.Content);
        else if (direction is RelayDirection.FromServer && frame.Content is CharacterSelectionEvent { Success.Character: { } character })
            Bind(session, binding, character);
    }

    private void Bind(GameSession session, Binding binding, Character character)
    {
        if (session.Account is not { } account)
        {
            BotLog.AccountUnknown(_log, session.Id);

            return;
        }

        var key = new CharacterKey(account.Id, character.Id);
        var state = CharacterState.Chosen(key, character) with { SubscriptionEnd = account.SubscriptionEnd };
        BotActor? bot;
        var made = false;

        // A bot's loop starts with it: one is made per character, never two that race.
        lock (_lock)
        {
            if (!_bots.TryGetValue(key, out bot))
            {
                bot = new BotActor(state, _maps, _world, _banks, () => _delays, _time, _logs.CreateLogger<BotActor>()) { FightsAlone = _fightsAlone };
                _bots[key] = bot;
                _pilots[key] = new FightPilot(bot, _tactics, _tables);
                made = true;
            }
        }

        bot.Attach(session, state);
        binding.Bot = bot;

        // The connection may have closed while it was bound, after Close found no bot to tell.
        if (session.Closed.IsCancellationRequested)
            bot.Detach(session);

        if (made)
            Added?.Invoke(this, bot);
    }

    private void Close(GameSession session)
    {
        if (_sessions.TryRemove(session, out var binding))
            binding.Bot?.Detach(session);
    }

    private sealed class Binding
    {
        // Set from the server's read loop, read from both.
        private volatile BotActor? _bot;

        public BotActor? Bot
        {
            get => _bot;
            set => _bot = value;
        }
    }
}
