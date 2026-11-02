using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Inventory;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using D3B.Protocol.Game;

namespace D3B.Bot.Fighting;

/// <summary>Fighting: the groups of monsters the settings allow found map after map through a zone and attacked, the AI playing each fight (D236, D244).</summary>
/// <remarks>
/// <para>
/// Each step is chosen from the state (D87). Healed first under its share of life, on a map of the zone the character walks
/// onto the cell of the nearest group the settings allow and the AI judges it beats, and attacks it, as the client does,
/// measured (9g): the attack from the group's own cell, which a group leaves by walking 1 to 4 cells, so that the walk is
/// made again; a group the server answers nothing for is left for the rest of the visit. A map with no such group is gone
/// through, and the step travels to the nearest map of the zone still to go through, isolated maps last when the settings
/// take them (D149, D245), stopping on the first map of the zone on its way that holds such a group; once every map was, the
/// tour starts again, the groups having come back.
/// </para>
/// <para>
/// A fight is the activity's own: the AI plays it (<see cref="BotActivity.FightsItsFights"/>), then the run answers it. A
/// fight lost resumes once the character regained the share of life it heals under, pauses or stops, as the settings say;
/// three in a row stop it (D198). Full at its share of pods, the character banks its loot, or all it does not wear.
/// </para>
/// </remarks>
public sealed class FightActivity : BotActivity
{
    private const int MostLostInARow = 3;
    private const int MostFailedJourneys = 3;
    private const int MostBankings = 3;
    private const int MostWalksToAGroup = 3;

    // Tenths of a second a life point takes, when the server did not say: as measured in every capture (D153).
    private const int RegainPoll = 5;

    private static readonly TimeSpan _busyDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _regainStep = TimeSpan.FromSeconds(10);

    // Measured (9g): groups come back to a map within minutes; a zone of one map waits for one rather than loop.
    private static readonly TimeSpan _respawnDelay = TimeSpan.FromSeconds(30);

    private readonly FightSettings _settings;
    private readonly WorldAtlas _atlas;
    private readonly WorldGraph _graph;
    private readonly GameTables _tables;
    private readonly GameMaps _maps;
    private readonly Lazy<FightTactics> _tactics;
    private readonly HashSet<long> _visited = [];
    private readonly HashSet<long> _unreachable = [];
    private readonly HashSet<long> _skipped = [];
    private readonly HashSet<int> _unusable = [];
    private readonly Dictionary<int, long> _loot = [];
    private HuntingGround? _ground;
    private long? _skippedOn;
    private ActivityReport? _report;
    private ActivityStatistics? _statistics;
    private bool _healing;
    private bool _regaining;
    private int _failedJourneys;
    private int _won;
    private int _lost;
    private int _lostInARow;
    private int _banks;
    private FightOdds? _attacked;
    private double _lostAt;

    /// <inheritdoc/>
    public override string Name => "Fighting";

    /// <inheritdoc/>
    public override ActivityReport? Report => Volatile.Read(ref _report);

    /// <inheritdoc/>
    public override ActivityStatistics? Statistics => Volatile.Read(ref _statistics);

    /// <inheritdoc/>
    public override int TeleportChanges => _settings.ZaapSaving;

    /// <inheritdoc/>
    public override bool FightsItsFights => true;

    /// <summary>Initializes a new instance of the <see cref="FightActivity"/> class.</summary>
    /// <param name="settings">Which groups to attack, where, the upkeep and the bank.</param>
    /// <param name="world">The installed build's world: the zone's maps.</param>
    /// <param name="tables">The installed build's tables: the monsters and spells.</param>
    /// <param name="maps">The installed build's maps, which the AI imagines a group's fight on.</param>
    public FightActivity(FightSettings settings, GameWorld world, GameTables tables, GameMaps maps)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(maps);

        _settings = settings;
        _atlas = world.Atlas;
        _graph = world.Graph;
        _tables = tables;
        _maps = maps;
        _tactics = new Lazy<FightTactics>(() => new FightTactics(tables));
        _report = new ActivityReport { Doing = GameText.Of(FightTexts.Text("DoingStart")) };
    }

    private static ActivityException Failure(string message, string key)
    {
        return new ActivityException(message, GameText.Of(FightTexts.Text(key)));
    }

    /// <inheritdoc/>
    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = context.State;

        if (state.IsBusy || state.MapId is not { } mapId)
        {
            await context.WaitUntilAsync(waited => !waited.IsBusy && waited.MapId is not null, _busyDelay, cancellationToken).ConfigureAwait(false);

            return true;
        }

        if (await HealAsync(context, cancellationToken).ConfigureAwait(false))
            return true;

        var ground = GroundOf(context);

        if (ground.Maps.ContainsKey(mapId))
        {
            if (GroupTo(context) is { } group)
            {
                await AttackAsync(context, group, cancellationToken).ConfigureAwait(false);

                return true;
            }

            _visited.Add(mapId);
        }

        await GoOnAsync(context, ground, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public override bool IsFull(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.MaxWeight > 0 && state.Weight * 100L >= (long)_settings.FullAt * state.MaxWeight;
    }

    /// <inheritdoc/>
    public override async Task<bool> UnloadAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        Publish(GameText.Of(FightTexts.Text("DoingBank")));

        for (var attempt = 0; attempt < MostBankings; attempt++)
        {
            switch (await context.BankAsync(stack => _settings.Deposit is FightDeposit.AllButWorn || _loot.ContainsKey(stack.ItemId), 0, cancellationToken).ConfigureAwait(false))
            {
                case BankOutcome.Done:
                    _banks++;
                    Tally();

                    // What the settings leave in the inventory may weigh more than the share the character banks at (D129).
                    if (IsFull(context.State))
                    {
                        var state = context.State;

                        throw new ActivityException(
                            $"The character is still full once banked, {state.Weight} of {state.MaxWeight} pods.",
                            GameText.Of(FightTexts.Text("FailStillFull"), GameTextValue.Number(state.Weight), GameTextValue.Number(state.MaxWeight), GameTextValue.Number(_settings.FullAt)));
                    }

                    Publish(GameText.Of(FightTexts.Text("DoingReturn")));

                    return true;
                case BankOutcome.NoBank:
                    return false;
                case BankOutcome.Busy:
                    await context.WaitUntilAsync(state => !state.IsBusy, _busyDelay, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public override InterruptionResponse OnInterrupted(Interruption reason)
    {
        if (reason is Interruption.Fight)
            Publish(GameText.Of(FightTexts.Text("DoingFight")));

        // Its fights are its own, answered once over; the player's hand and a new connection resume it as harvesting does.
        return InterruptionResponse.Resume;
    }

    /// <inheritdoc/>
    public override InterruptionResponse OnFightOver(FightEnd? ending, InterruptionResponse response)
    {
        var attacked = _attacked;

        _attacked = null;

        if (ending is { } end)
        {
            foreach (var (item, quantity) in end.Items)
                _loot[item] = _loot.GetValueOrDefault(item) + quantity;
        }

        if (ending is not { Outcome: FightOutcome.Lost })
        {
            if (ending is not null)
                _won++;

            _lostInARow = 0;
            Tally();

            return response;
        }

        _lost++;
        _lostInARow++;
        _regaining = true;

        // Measured (D266): two groups of 4 judged winnable, each to take the character's life in under half the turns it
        // needed, lost one after the other. A group no less risky than one it lost to is left for the run (D267).
        if (attacked is { } odds && GroupJudge.Share(odds) is var share && double.IsFinite(share))
            _lostAt = Math.Max(_lostAt, share);

        Tally();

        if (_lostInARow >= MostLostInARow)
            return InterruptionResponse.Stop;

        return _settings.AfterLostFight switch
        {
            FightAfterLoss.Pause => InterruptionResponse.Pause,
            FightAfterLoss.Stop => InterruptionResponse.Stop,
            _ => InterruptionResponse.Resume,
        };
    }

    /// <inheritdoc/>
    public override async Task RecoverAsync(BotContext context, Interruption reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await Recovery.SettleAsync(context, cancellationToken).ConfigureAwait(false);

        if (_regaining)
            await RegainAsync(context, cancellationToken).ConfigureAwait(false);

        _regaining = false;
    }

    private HuntingGround GroundOf(BotContext context)
    {
        if (_ground is { } ground)
            return ground;

        var state = context.State;

        ground = HuntingGround.Of(_settings, _atlas, _graph, _tables.Monsters, state.Level ?? 1, state.SubscriptionEnd is { } end && end > context.Time.GetUtcNow());

        if (ground.Maps.Count is 0)
            throw Failure("No sub-area holds monsters to fight at the character's level.", "FailNowhere");

        _ground = ground;

        if (ground.SubAreas is [var subArea])
            Publish(GameText.Of(FightTexts.Text("DoingPlace"), GameTextValue.SubArea(subArea)));

        return ground;
    }

    private MapMonsterGroup? GroupTo(BotContext context)
    {
        var state = context.State;

        if (state.Level is not { } level || state.Cell is not { } here || state.MapId is not { } mapId)
            return null;

        // A group's identifier is its map's: the groups left are those of the map the character stands on.
        if (_skippedOn != mapId)
        {
            _skipped.Clear();
            _skippedOn = mapId;
        }

        FightMap? map = null;

        // The nearest first: the shortest walk, before the group walks on.
        foreach (var group in state.MonsterGroups.Values.OrderBy(group => group.Cell is { } cell ? Engine.Maps.MapGeometry.Distance(here, cell) : int.MaxValue))
        {
            if (_skipped.Contains(group.ActorId) || !GroupJudge.Allows(_settings, group, level, _tables.Monsters))
                continue;

            if (!_settings.OnlyWinnable)
                return group;

            if (map is null && _maps.TryGet(mapId, out var data))
                map = FightMap.Of(data);

            if (map is not null && GroupJudge.Beats(group, state, map, _tables, _tactics.Value, _lostAt))
                return group;

            _skipped.Add(group.ActorId);
        }

        return null;
    }

    private async Task AttackAsync(BotContext context, MapMonsterGroup group, CancellationToken cancellationToken)
    {
        Publish(GameText.Of(FightTexts.Text("DoingAttack"), GameTextValue.Number(group.Monsters.Length)));

        for (var walk = 0; walk < MostWalksToAGroup; walk++)
        {
            // Measured (9g): a group walks 1 to 4 cells at a time; the server answered nothing to an attack from 3 cells away.
            if (!context.State.MonsterGroups.TryGetValue(group.ActorId, out var now) || now.Cell is not { } cell)
                return;

            if (context.State.Cell != cell && await context.WalkAsync(cell, cancellationToken).ConfigureAwait(false) is not WalkOutcome.Arrived)
                break;

            if (!context.State.MonsterGroups.TryGetValue(group.ActorId, out now) || now.Cell != context.State.Cell)
                continue;

            var fought = context.Expect((side, _, state) => state.Fight is not null);

            _attacked = _settings.OnlyWinnable && _maps.TryGet(context.State.MapId ?? 0, out var data)
                ? GroupJudge.OddsOf(now, context.State, FightMap.Of(data), _tables, _tactics.Value)
                : null;

            await context.SendAsync(new AttackMonsterRequest { MonsterGroupId = group.ActorId }, cancellationToken).ConfigureAwait(false);

            // A fight beginning interrupts this step; a refusal is no answer at all, measured.
            if (await fought.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false) is not null)
                return;

            break;
        }

        _attacked = null;
        _skipped.Add(group.ActorId);
    }

    private async Task GoOnAsync(BotContext context, HuntingGround ground, CancellationToken cancellationToken)
    {
        var remaining = ground.Maps.Keys.Where(map => !_visited.Contains(map) && !_unreachable.Contains(map)).ToHashSet();

        if (remaining.Count is 0)
        {
            var here = context.State.MapId;

            _visited.Clear();
            _skipped.Clear();

            // A level gained since moves the place the activity chose at the next tour (D166).
            if (_settings.Place is FightPlace.Automatic)
                _ground = null;

            // A zone the character stands on alone: the next tour would start where it is, the groups not back yet.
            if (here is { } mapId && GroundOf(context).Maps.Keys.All(map => map == mapId || _unreachable.Contains(map)))
                await context.WaitUntilAsync(state => state.MonsterGroups.Count > 0, _respawnDelay, cancellationToken).ConfigureAwait(false);

            return;
        }

        var inZone = context.State.MapId is { } current && ground.Maps.ContainsKey(current);

        // Isolated maps last, once the others were gone through (D149).
        var near = remaining.Where(map => !ground.Isolated.Contains(map)).ToHashSet();
        var sought = near.Count > 0 ? near : remaining;

        Publish(GameText.Of(FightTexts.Text(inZone ? "DoingTravel" : "DoingReturn")));

        // Measured (D256, 9g5): a journey crossed maps of the zone holding groups the activity attacks; it stops on the first.
        bool Attackable(CharacterState state)
        {
            return state.MapId is { } map && ground.Maps.ContainsKey(map) && GroupTo(context) is not null;
        }

        switch (await context.TravelAsync(sought, Attackable, cancellationToken).ConfigureAwait(false))
        {
            case TravelOutcome.Arrived:
                _failedJourneys = 0;
                break;
            case TravelOutcome.Busy:
                await context.WaitUntilAsync(state => !state.IsBusy, _busyDelay, cancellationToken).ConfigureAwait(false);
                break;
            default:
                if (++_failedJourneys >= MostFailedJourneys)
                {
                    foreach (var map in sought)
                        _unreachable.Add(map);

                    _failedJourneys = 0;

                    if (ground.Maps.Keys.All(_unreachable.Contains))
                        throw Failure("No map of the zone can be reached.", "FailUnreachable");
                }

                break;
        }
    }

    private async Task<bool> HealAsync(BotContext context, CancellationToken cancellationToken)
    {
        var state = context.State;

        if (!_settings.Heal || context.Life is not { } life || state.MaxLife is not { } most || most <= 0)
            return false;

        // Once under the share, the character eats until its life is whole (D148), or regenerates up to the share (9g5).
        if (life >= most || (!_healing && life * 100L >= (long)_settings.HealBelow * most))
        {
            _healing = false;

            return false;
        }

        _healing = true;

        if (Food.StackToEat(state, _settings.Food, _tables.Items, _unusable) is { } food)
        {
            Publish(GameText.Of(FightTexts.Text("DoingEat"), GameTextValue.Item(food.ItemId)));

            // In one request: measured, the server eats only as much of the stack as the life lacks (D156).
            switch (await context.UseObjectsAsync(food.Uid, food.Quantity, cancellationToken).ConfigureAwait(false))
            {
                case ObjectUseOutcome.Used:
                    break;
                case ObjectUseOutcome.Refused:
                    // Measured: the server refuses food at full life, which it knows better than the reckoning.
                    _healing = false;
                    break;
                default:
                    _unusable.Add(food.Uid);
                    break;
            }

            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

            return true;
        }

        // Nothing left to eat: measured, the life regenerates by itself a point each interval the server gave (D153). The
        // owner's choice (9g5): nearly whole, the character waited its whole life back without looking at it again; it waits
        // for the share it heals under alone, as after a lost fight (D198), looking again at least each 10 seconds.
        var share = (((long)_settings.HealBelow * most) + 99) / 100;

        if (life >= share)
        {
            _healing = false;

            return false;
        }

        var point = TimeSpan.FromMilliseconds(100 * (state.LifeRegenInterval ?? RegainPoll));
        var regenerating = TimeSpan.FromTicks(point.Ticks * (share - life));

        Publish(GameText.Of(FightTexts.Text("DoingRegenerate"), GameTextValue.Number((long)Math.Ceiling(regenerating.TotalSeconds))));
        await context.WaitUntilAsync(_ => false, regenerating < _regainStep ? regenerating : _regainStep, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async Task RegainAsync(BotContext context, CancellationToken cancellationToken)
    {
        // Food the profile carries heals at the next step (D148); else the life regenerates, a point each interval (D153).
        var state = context.State;

        if (_settings.Heal && Food.StackToEat(state, _settings.Food, _tables.Items, _unusable) is not null)
            return;

        for (var life = context.Life; life is { } now && state.MaxLife is { } most && now * 100L < (long)_settings.HealBelow * most; life = context.Life)
        {
            Publish(GameText.Of(FightTexts.Text("DoingRegain"), GameTextValue.Number(now), GameTextValue.Number(most)));

            var interval = TimeSpan.FromMilliseconds(100 * (state.LifeRegenInterval ?? RegainPoll));
            var missing = (((long)_settings.HealBelow * most) + 99) / 100 - now;

            await Task.Delay(TimeSpan.FromTicks(Math.Min(interval.Ticks * Math.Max(1, missing), _regainStep.Ticks)), context.Time, cancellationToken).ConfigureAwait(false);
            state = context.State;
        }
    }

    private void Publish(GameText doing)
    {
        Volatile.Write(ref _report, new ActivityReport { Doing = doing, Counts = CountsOf() });
    }

    private ImmutableArray<ActivityCount> CountsOf()
    {
        return
        [
            new ActivityCount(GameText.Counted(FightTexts.Text("CountWonOne"), FightTexts.Text("CountWonMany"), _won)),
            new ActivityCount(GameText.Counted(FightTexts.Text("CountLostOne"), FightTexts.Text("CountLostMany"), _lost)),
            .. _loot.OrderByDescending(pair => pair.Value).Select(pair => new ActivityCount(GameText.Of(FightTexts.Text("CountLoot"), GameTextValue.Number(pair.Value), GameTextValue.Item(pair.Key)), pair.Key)),
        ];
    }

    private void Tally()
    {
        Volatile.Write(ref _statistics, new ActivityStatistics { Items = _loot.ToImmutableDictionary(), Banks = _banks });

        if (Report is { } report)
            Volatile.Write(ref _report, report with { Counts = CountsOf() });
    }
}
