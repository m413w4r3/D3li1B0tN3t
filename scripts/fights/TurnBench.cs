using System.Collections.Immutable;
using System.Diagnostics;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Maps;
using D3B.Engine.Tactics;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Scripts.Fights;

/// <summary>Replays every turn the characters played in the captures through the fight's AI, and sets what it foresaw against what happened.</summary>
/// <remarks>
/// <para>
/// At each turn's start, the AI plans from the fight as the bot would imagine it (<see cref="ImaginedFights"/>), and the turn
/// the player or the AI played is imagined too, from the casts and walks the server announced: both are judged by the same
/// numbers, the damage dealt, the kills and the heals foreseen, then the life the enemies' reply takes (D206).
/// </para>
/// <para>
/// At each turn's end, what the judge reckons is set against what came: the threat against the life the character lost
/// before its next turn, the enemies' reply as the search plays it against the same, and the approach against the damage
/// it dealt in its next turn. It prints counts and sums, never a value of one fight.
/// </para>
/// <para>
/// A summon's turn the client played is replayed the same, the AI's plan against the player's (D271); the summons planned
/// and played are counted by breed and spell, and what the summons deal in the reply (D270).
/// </para>
/// </remarks>
internal sealed class TurnBench
{
    private readonly GameTables _tables;
    private readonly GameMaps _maps;
    private readonly FightTactics _tactics;
    private readonly EffectActions _actions;
    private readonly TacticWeights _weights;
    private readonly TurnSearchLimits _limits;
    private readonly Dictionary<long, FightMap> _fightMaps = [];
    private readonly List<double> _searches = [];
    private readonly Dictionary<string, double> _sums = new(StringComparer.Ordinal);

    public Tally Tally { get; } = new();

    public TurnBench(GameTables tables, GameMaps maps, TacticWeights weights, TurnSearchLimits limits)
    {
        _tables = tables;
        _maps = maps;
        _tactics = new FightTactics(tables);
        _actions = new EffectActions(tables.Effects);
        _weights = weights;
        _limits = limits;
    }

    private static double Effective(Combatant fighter)
    {
        return fighter.IsAlive ? fighter.Life + Math.Max(0, fighter.Characteristic(FightCharacteristic.Shield)) : 0;
    }

    private static (double Dealt, int Kills, double Healed) Outcome(ImaginedFight before, ImaginedFight after)
    {
        var team = before.Actor.Team;
        double dealt = 0, healed = 0;
        var kills = 0;

        foreach (var fighter in before.Fighters)
        {
            if (!fighter.IsAlive || after.Find(fighter.Id) is not { } then)
                continue;

            var change = Effective(then) - Effective(fighter);

            if (fighter.Team != team)
            {
                dealt += Math.Max(0, -change);
                kills += then.IsAlive ? 0 : 1;
            }
            else
            {
                healed += Math.Max(0, change);
            }
        }

        return (dealt, kills, healed);
    }

    public void Replay(List<IMessage> messages)
    {
        CharacterState? state = null;
        Turn? turn = null;
        Turn? summonTurn = null;
        Ending? ending = null;

        foreach (var message in messages)
        {
            if (message is CharacterSelectionEvent { Success.Character: { } chosen })
            {
                state = CharacterState.Chosen(new CharacterKey(0, chosen.Id), chosen);
                turn = null;
                ending = null;
            }

            if (state is null)
                continue;

            var self = state.Key.CharacterId;

            if (turn is not null && state.Fight is { } before)
                Observe(before, self, message, turn, state.Weapon is { } weapon && _tactics.Blows.TryGetBlow(weapon.ItemId, out var blow) ? blow : null);

            if (summonTurn is not null && state.Fight is { } summonBefore)
                Observe(summonBefore, summonTurn.Start.ActorId, message, summonTurn, null);

            // Whom the enemies' blows took life from between two of the character's turns, as the server announced it.
            if (ending is not null && message is GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.LifeLost } lost && state.Fight is { } between
                && between.Fighters.TryGetValue(self, out var own) && between.Fighters.TryGetValue(lost.LifeLost.TargetId, out var hit) && hit.Team == own.Team
                && between.Fighters.TryGetValue(lost.SourceId, out var source) && source.Team != own.Team)
            {
                Add($"enemies' blows between turns: life, on {(hit.Id == self ? "the character" : hit.SummonerId is not null ? "a summon" : "another ally")}", lost.LifeLost.Loss + (lost.LifeLost.HasShieldLoss ? lost.LifeLost.ShieldLoss : 0));
            }

            state = CharacterEvents.Apply(state, RelayDirection.FromServer, message);

            switch (message)
            {
                // A summon the client controls plays its turn as the character does (9f2), the turns between left alone.
                case FightTurnStartPlayingEvent when state.Fight is { IsOwnTurn: true, PlayingId: { } summon } && summon != self && Imagined(state, null) is { } summoned:
                    summonTurn = new Turn(summoned.Fight, summoned.Knowledge, (summoned.Fight.Actor.ActionPoints, summoned.Fight.Actor.MovementPoints));
                    Plan(summonTurn);

                    break;

                case FightTurnEndEvent ended when summonTurn is not null && ended.FighterId == summonTurn.Start.ActorId:
                    FinishSummon(summonTurn);
                    summonTurn = null;

                    break;

                case FightTurnStartPlayingEvent when state.Fight is { IsOwnTurn: true } && Imagined(state, null) is { } start:
                    turn = new Turn(start.Fight, start.Knowledge, (start.Fight.Actor.ActionPoints, start.Fight.Actor.MovementPoints));

                    if (ending is not null)
                    {
                        Settle(ending, start.Fight);
                        turn.Foreseen = ending.NextTurn;
                        turn.Searched = ending.Searched;
                    }

                    ending = null;
                    Plan(turn);

                    break;

                case FightTurnEndEvent ended when ended.FighterId == self && turn is not null:
                    ending = Finish(turn, state);
                    turn = null;

                    break;

                case FightEndEvent:
                    if (ending is not null && state.Fight is { } over)
                        Settle(ending, null, over.Fighters.GetValueOrDefault(self));

                    turn = null;
                    summonTurn = null;
                    ending = null;

                    break;
            }
        }
    }

    public void Write(TextWriter writer)
    {
        Tally.Write(writer);
        writer.WriteLine("sums");

        foreach (var (key, sum) in _sums.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            writer.WriteLine($"  {sum,12:F0} {key}");

        if (_searches.Count > 0)
        {
            _searches.Sort();
            writer.WriteLine($"searches: {_searches.Count}, halfway {_searches[_searches.Count / 2]:F0} ms, 90% {_searches[(int)(_searches.Count * 0.9)]:F0} ms, at most {_searches[^1]:F0} ms");
        }
    }

    private void Add(string key, double value)
    {
        _sums[key] = _sums.GetValueOrDefault(key) + value;
    }

    private (ImaginedFight Fight, FightKnowledge Knowledge)? Imagined(CharacterState state, (int, int)? turnPoints)
    {
        if (state.MapId is not { } mapId || !_maps.TryGet(mapId, out var data))
            return null;

        if (!_fightMaps.TryGetValue(mapId, out var map))
            _fightMaps[mapId] = map = FightMap.Of(data);

        return ImaginedFights.Of(state, map, _tables, _tactics.Blows, turnPoints);
    }

    private void Plan(Turn turn)
    {
        var clock = Stopwatch.StartNew();
        var plan = _tactics.Plan(turn.Start, turn.Knowledge, _weights, _limits);

        _searches.Add(clock.Elapsed.TotalMilliseconds);
        turn.Planned = plan;

        var casts = plan.Actions.Count(action => action.Kind is TacticActionKind.Cast);

        Tally.Count($"{(turn.Start.Actor.IsSummon ? "summon plans" : "plans")}: {(plan.IsEmpty ? "nothing" : casts is 0 ? "walks only" : "casts")}");
    }

    private static void Observe(FightState fight, long self, IMessage message, Turn turn, SpellLevel? blow)
    {
        switch (message)
        {
            case GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Cast, Cast.Spell: { } spell } action when action.SourceId == self:
                // A cast the character chose is the first of its sequence of a spell (client.md); the others its effects cast.
                if (fight.Sequences is [.., { Type: SequenceType.Spell, HasCast: false } innermost] && innermost.AuthorId == self)
                    turn.Played.Add(TacticAction.Cast(spell.SpellId, spell.LevelId, action.Cast.CellId));

                break;

            case GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Cast, Cast.Spell: null } struck when struck.SourceId == self && blow is { } level:
                // A weapon's blow names no spell (D218): it is the worn weapon's, when D3B strikes that weapon's.
                if (fight.Sequences is [.., { Type: SequenceType.Weapon, HasCast: false } inner] && inner.AuthorId == self)
                    turn.Played.Add(TacticAction.Cast(WeaponBlows.SpellId, level.Id, struck.Cast.CellId));

                break;

            case SequenceStartEvent { Type: SequenceType.Weapon } weapon when weapon.AuthorId == self:
                turn.WeaponBlows++;

                break;

            case MapMovementEvent walked when walked.ActorId == self && walked.Cells.Count > 1:
                turn.Played.Add(TacticAction.Walk(walked.Cells[^1]));

                break;
        }
    }

    private string Does(int levelId)
    {
        // What a cast mostly does, the first of these its level's effects do.
        if (!_tactics.Blows.Spells.TryGetLevel(levelId, out var level))
            return "level unknown";

        var kinds = _tactics.Blows.Spells.EffectsOf(level).ToArray().Select(effect => _actions.Of(effect.EffectId).Kind).ToHashSet();

        return kinds.Overlaps([EffectKind.Damage, EffectKind.Steal, EffectKind.FixedDamage]) ? "damage"
            : kinds.Contains(EffectKind.Detonate) ? "detonate"
            : kinds.Overlaps([EffectKind.Heal, EffectKind.FixedHeal, EffectKind.PercentHeal]) ? "heal"
            : kinds.Overlaps([EffectKind.Shield, EffectKind.LifeShield, EffectKind.LevelShield]) ? "shield"
            : kinds.Contains(EffectKind.Summon) ? "summon"
            : kinds.Overlaps([EffectKind.Push, EffectKind.Pull, EffectKind.Teleport, EffectKind.Swap]) ? "move"
            : kinds.Contains(EffectKind.Boost) ? "boost"
            : kinds.Contains(EffectKind.Deboost) ? "deboost"
            : kinds.Overlaps([EffectKind.StateSet, EffectKind.StateRemoved]) ? "state"
            : "other";
    }

    private static EffectElement BestElement(Combatant caster)
    {
        // The element its characteristics raise most: strength earth's, intelligence fire's, chance water's, agility air's.
        (EffectElement Element, int Characteristic)[] elements =
        [
            (EffectElement.Earth, FightCharacteristic.Strength),
            (EffectElement.Fire, FightCharacteristic.Intelligence),
            (EffectElement.Water, FightCharacteristic.Chance),
            (EffectElement.Air, FightCharacteristic.Agility),
        ];

        return elements.MaxBy(pair => caster.Characteristic(pair.Characteristic)).Element;
    }

    private EffectElement? ElementOf(in SpellLevel level, Combatant caster)
    {
        foreach (var effect in _tactics.Blows.Spells.EffectsOf(level))
        {
            var action = _actions.Of(effect.EffectId);

            if (action.Kind is EffectKind.Damage or EffectKind.Steal && (effect.Properties & SpellEffectProperties.ForClientOnly) is 0)
            {
                var element = EffectFormulas.Resolve(action.Element, caster);

                return element is EffectElement.Neutral ? EffectElement.Earth : element;
            }
        }

        return null;
    }

    private void Elements(Turn turn, IEnumerable<TacticAction> actions, string whose)
    {
        // Whether each blow is of the element the caster's characteristics raise most, and when not, whether the engine
        // foresaw a blow of that element, from where the caster stood on the same cell, dealing more.
        var fight = turn.Start;

        foreach (var action in actions)
        {
            var actor = fight.Actor;

            if (action.Kind is TacticActionKind.Cast && _tactics.Blows.Spells.TryGetLevel(action.LevelId, out var chosen) && ElementOf(chosen, actor) is { } element)
            {
                var best = BestElement(actor);
                var dealt = Dealt(fight, _tactics.Imagination.Cast(fight, chosen, action.Cell));
                var outcome = "in the element its characteristics raise most";

                if (element != best)
                {
                    var alternatives = turn.Knowledge.SpellsOf(actor.Id)
                        .Where(level => level.Id != chosen.Id && ElementOf(level, actor) == best && actor.ActionPoints >= CastRules.Modified(level, actor).ApCost
                            && _tactics.Rules.Check(fight.Map, fight.Fighters, actor, level, action.Cell, fight.History, fight.Round) is CastRefusal.None)
                        .Select(level => Dealt(fight, _tactics.Imagination.Cast(fight, level, action.Cell)) / Math.Max(1, (int)CastRules.Modified(level, actor).ApCost))
                        .ToList();
                    var perPoint = dealt / Math.Max(1, (int)CastRules.Modified(chosen, actor).ApCost);

                    outcome = alternatives.Count is 0 ? "in another, none of that element castable there"
                        : alternatives.Max() > perPoint + 0.5 ? "in another, one of that element foreseen dealing more a point"
                        : "in another, those of that element foreseen dealing no more a point";
                }

                Tally.Count($"blows {whose}: {outcome}");
            }

            fight = Act(fight, [action]);
        }
    }

    private ImaginedFight Act(ImaginedFight fight, IEnumerable<TacticAction> actions)
    {
        foreach (var action in actions)
        {
            var actor = fight.Actor;

            if (!actor.IsAlive)
                break;

            if (action.Kind is TacticActionKind.Walk)
            {
                var reachable = _tactics.Moves.Reachable(fight.Map, fight.Fighters, actor);
                var left = reachable.TryGetValue(action.Cell, out var kept) ? kept : (Math.Max(0, actor.MovementPoints - MapGeometry.Distance(actor.Cell, action.Cell)), actor.ActionPoints);

                fight = _tactics.Imagination.Walked(fight, action.Cell, left);
            }
            else if (_tactics.Blows.Spells.TryGetLevel(action.LevelId, out var level))
            {
                fight = _tactics.Imagination.Cast(fight, level, action.Cell);
            }
        }

        return fight;
    }

    private static bool Idle(ImaginedFight before, ImaginedFight after)
    {
        if (after.Fighters.Length != before.Fighters.Length || after.Buffs.Length != before.Buffs.Length)
            return false;

        for (var index = 0; index < before.Fighters.Length; index++)
        {
            var was = before.Fighters[index];
            var now = after.Fighters[index];

            if ((now.Id == before.ActorId ? now with { ActionPoints = was.ActionPoints } : now) != was)
                return false;
        }

        return true;
    }

    private void States(ImaginedFight fight, IEnumerable<TacticAction> actions, string whose)
    {
        // The casts asking a state, those changing the character's states, as a Zobal's masks do, and those the engine
        // foresees doing nothing but spend AP, as a Zobal's shield cast on an enemy did (D226, D229). A change of the
        // character's states is told by its place among the turn's casts, and by the casts after it that ask a state it
        // set: a mask put on first to cast its spells, or last for the next turn (D250).
        var casts = actions.Count(action => action.Kind is TacticActionKind.Cast);
        var index = -1;
        var set = new HashSet<int>();

        foreach (var action in actions)
        {
            if (!fight.Actor.IsAlive)
                break;

            if (action.Kind is TacticActionKind.Walk || !_tactics.Blows.Spells.TryGetLevel(action.LevelId, out var level))
            {
                fight = Act(fight, [action]);

                continue;
            }

            var after = _tactics.Imagination.Cast(fight, level, action.Cell);

            var breed = fight.Actor.BreedId is { } id ? $"breed {id}" : "breed unknown";

            index++;

            if (level.StatesCriterion is not SpellLevel.NoCriterion)
            {
                Tally.Count($"states: casts {whose}, of a level asking a state, {breed}");
                Tally.Count($"states: casts {whose} asking a state, by spell, {breed}, spell {level.SpellId}");

                if (set.Count > 0 && _tactics.Rules.StatesAllow(level, after.Actor) && !_tactics.Rules.StatesAllow(level, fight.Actor with { States = [.. fight.Actor.States.Except(set)] }))
                    Tally.Count($"states: casts {whose}, of a level asking a state the turn's change set, {breed}");
            }

            if (!after.Actor.States.ToHashSet().SetEquals(fight.Actor.States))
            {
                Tally.Count($"states: casts {whose}, changing the character's states, {breed}");
                Tally.Count($"states: changes {whose} of the character's states, {(casts is 1 ? "the turn's only cast" : index is 0 ? "the turn's first cast" : index == casts - 1 ? "the turn's last cast" : "between casts")}, {breed}");
                set.UnionWith(after.Actor.States.Except(fight.Actor.States));
            }

            if (Idle(fight, after))
                Tally.Count($"states: casts {whose}, foreseen to do nothing but spend AP, {breed}");

            fight = after;
        }
    }

    private void FinishSummon(Turn turn)
    {
        // A controlled summon's turn, the AI's plan against the turn the player played, both imagined from its start.
        var played = Act(turn.Start, turn.Played);
        var planned = turn.Planned is { } plan ? Act(turn.Start, plan.Actions) : turn.Start;
        var monster = turn.Start.Actor.MonsterId is { } id ? $"monster {id}" : "monster unknown";

        Tally.Count($"summon turns: replayed, {monster}");
        Tally.Count("summon turns: casts played", turn.Played.Count(action => action.Kind is TacticActionKind.Cast));
        Tally.Count("summon turns: casts planned", turn.Planned?.Actions.Count(action => action.Kind is TacticActionKind.Cast) ?? 0);
        Add("summon turns: damage dealt, played", Dealt(turn.Start, played));
        Add("summon turns: damage dealt, planned", Dealt(turn.Start, planned));
    }

    private Ending Finish(Turn turn, CharacterState state)
    {
        var judge = _tactics.JudgeOf(turn.Start, turn.Knowledge, _weights);
        var played = Act(turn.Start, turn.Played);
        var planned = turn.Planned is { } plan ? Act(turn.Start, plan.Actions) : turn.Start;

        Tally.Count("turns: replayed");

        foreach (var action in turn.Played.Where(action => action.Kind is TacticActionKind.Cast))
        {
            Tally.Count($"casts played, by what they do: {Does(action.LevelId)}");

            if (Does(action.LevelId) is "boost" or "state" && _tactics.Blows.Spells.TryGetLevel(action.LevelId, out var buffing))
            {
                foreach (var effect in _tactics.Blows.Spells.EffectsOf(buffing))
                {
                    var does = _actions.Of(effect.EffectId);

                    if (does.Kind is EffectKind.Boost or EffectKind.Deboost or EffectKind.StateSet)
                        Tally.Count($"buffs played: {does.Kind} {(does.Kind is EffectKind.StateSet ? $"state {effect.DiceNum}" : $"characteristic {does.Characteristic}")}, {effect.Duration} rounds, mask {_tactics.Blows.Spells.TargetMaskOf(effect)}, ap {buffing.ApCost}");
                }
            }
        }

        foreach (var action in turn.Planned?.Actions.Where(action => action.Kind is TacticActionKind.Cast) ?? [])
        {
            Tally.Count($"casts planned, by what they do: {Does(action.LevelId)}");

            if (Does(action.LevelId) is "summon")
                Tally.Count($"summons planned: breed {turn.Start.Actor.BreedId}, spell {action.SpellId}");
        }

        foreach (var action in turn.Played.Where(action => action.Kind is TacticActionKind.Cast && Does(action.LevelId) is "summon"))
            Tally.Count($"summons played: breed {turn.Start.Actor.BreedId}, spell {action.SpellId}");

        // A Rogue's turns (9f3): the bombs it summoned and set off, played and planned.
        if (turn.Start.Actor.BreedId is 13)
        {
            Tally.Count("bombs: a Roublard's turns");

            foreach (var (actions, what) in new (IReadOnlyList<TacticAction> Actions, string What)[] { (turn.Played, "played"), (turn.Planned?.Actions ?? [], "planned") })
            {
                var casts = actions.Where(action => action.Kind is TacticActionKind.Cast).ToList();

                Tally.Count($"bombs: summoned, {what}", casts.Count(action => _tactics.Blows.Spells.TryGetLevel(action.LevelId, out var level) && _tactics.Blows.Spells.EffectsOf(level).ToArray().Any(effect => effect.EffectId is 1008)));
                Tally.Count($"bombs: set off, {what}", casts.Count(action => Does(action.LevelId) is "detonate"));
            }

            // Its walls as the turn ends, and the enemies on them, whom they strike again as their turns begin (9f4).
            foreach (var (end, what) in new (ImaginedFight End, string What)[] { (played, "played"), (planned, "planned") })
            {
                var walls = new List<BombWallCell>();

                _tactics.Bombs.Walls(end.Map, end.Fighters, walls);
                Tally.Count($"bombs: wall cells as the turn ends, {what}", walls.Count);
                Tally.Count($"bombs: enemies on a wall as the turn ends, {what}", end.Fighters.Count(fighter => fighter.IsAlive && fighter.Team != end.Actor.Team && walls.Exists(wall => wall.Cell == fighter.Cell)));
            }
        }

        // The glyphs as the turn ends, and the enemies in those that strike as their turns begin (9f4).
        foreach (var (end, what) in new (ImaginedFight End, string What)[] { (played, "played"), (planned, "planned") })
        {
            var own = end.Marks.Where(mark => mark.AuthorId == turn.Start.ActorId && !turn.Start.Marks.Contains(mark)).ToList();

            if (own.Count is 0)
                continue;

            Tally.Count($"glyphs: set, breed {turn.Start.Actor.BreedId}, {what}", own.Count);
            Tally.Count($"glyphs: enemies in one striking as their turn begins, {what}", end.Fighters.Count(fighter => fighter.IsAlive && fighter.Team != end.Actor.Team && own.Exists(mark => mark.Kind is CellMarkKind.TurnStart && mark.Covers(fighter.Cell))));
        }

        States(turn.Start, turn.Played, "played");

        if (turn.Planned is { } planning)
            States(turn.Start, planning.Actions, "planned");

        // The weapon's blow, weighed as a spell's (D217): those the turns played, those the plans hold, and the turns whose
        // character could strike its sword's.
        if (turn.WeaponBlows > 0)
            Tally.Count("turns: with a weapon's blow", turn.WeaponBlows);

        if (turn.Knowledge.SpellsOf(turn.Start.ActorId).Any(level => WeaponBlows.IsBlow(level)))
        {
            Tally.Count("weapon's blow: turns of a character with a sword D3B strikes");
            Tally.Count("weapon's blow: blows played", turn.Played.Count(action => action.Kind is TacticActionKind.Cast && action.SpellId is WeaponBlows.SpellId));
            Tally.Count("weapon's blow: blows planned", turn.Planned?.Actions.Count(action => action.Kind is TacticActionKind.Cast && action.SpellId is WeaponBlows.SpellId) ?? 0);
        }

        Compare(turn, judge, played, planned);
        Elements(turn, turn.Played, "played");

        if (turn.Planned is { } elementsPlanned)
            Elements(turn, elementsPlanned.Actions, "planned");

        // What the judge reckons from where the turn really ended, against what came before the character's next turn.
        var ended = Imagined(state, turn.Points) is { } real ? real.Fight : played;

        Add("played: damage dealt, as the server announced", Dealt(turn.Start, ended));
        Add("played: kills, as the server announced", turn.Start.Fighters.Count(enemy => enemy.IsAlive && enemy.Team != turn.Start.Actor.Team && ended.Find(enemy.Id) is { IsAlive: false }));

        if (turn.Foreseen is { } foreseen)
        {
            Add("approach: foreseen as the turn before ended", foreseen);
            Add("approach: dealt", Dealt(turn.Start, ended));
            Tally.Count($"approach: {Band(foreseen, Dealt(turn.Start, ended))}");
        }

        if (turn.Searched is { } searched)
        {
            Add("next turn searched: foreseen as the turn before ended", searched);
            Tally.Count($"next turn searched: {Band(searched, Dealt(turn.Start, ended))}");
        }

        var endJudge = _tactics.JudgeOf(ended, turn.Knowledge, _weights);
        var self = ended.ActorId;
        var replied = _tactics.Replies(ended, turn.Knowledge, endJudge);

        foreach (var ally in ended.Fighters.Where(fighter => fighter.IsAlive && fighter.Team == ended.Actor.Team))
        {
            if (replied.Find(ally.Id) is { } then)
                Add($"reply's blows: life, on {(ally.Id == self ? "the character" : ally.IsSummon ? "a summon" : "another ally")}", Math.Max(0, Effective(ally) - Effective(then)));
        }
        var later = turn.Knowledge with { Cooldowns = turn.Knowledge.Cooldowns.Where(pair => pair.Value > 1).ToImmutableDictionary(pair => pair.Key, pair => pair.Value - 1) };
        var (next, _) = _tactics.NextTurn(replied, later, endJudge, _limits);

        return new Ending(
            Effective(ended.Actor),
            endJudge.Threat(ended, self),
            Math.Max(0, Effective(ended.Actor) - (replied.Find(self) is { } after ? Effective(after) : 0)),
            endJudge.NextTurnDamage(ended),
            Dealt(replied, next));
    }

    private static int Kills(ImaginedFight start, ImaginedFight end)
    {
        return start.Fighters.Count(enemy => enemy.IsAlive && enemy.Team != start.Actor.Team && end.Find(enemy.Id) is { IsAlive: false });
    }

    private static double Dealt(ImaginedFight start, ImaginedFight end)
    {
        var dealt = 0.0;

        foreach (var enemy in start.Fighters)
        {
            if (enemy.IsAlive && enemy.Team != start.Actor.Team && end.Find(enemy.Id) is { } then)
                dealt += Math.Max(0, Effective(enemy) - Effective(then));
        }

        return dealt;
    }

    private void Compare(Turn turn, FightJudge judge, ImaginedFight played, ImaginedFight planned)
    {
        var self = turn.Start.ActorId;
        var (playedDealt, playedKills, playedHealed) = Outcome(turn.Start, played);
        var (plannedDealt, plannedKills, plannedHealed) = Outcome(turn.Start, planned);
        var playedReply = _tactics.Replies(played, turn.Knowledge, judge);
        var plannedReply = _tactics.Replies(planned, turn.Knowledge, judge);
        var playedLost = Effective(played.Actor) - (playedReply.Find(self) is { } p ? Effective(p) : 0);
        var plannedLost = Effective(planned.Actor) - (plannedReply.Find(self) is { } q ? Effective(q) : 0);

        Add("played: damage foreseen", playedDealt);
        Add("played: kills foreseen", playedKills);
        Add("played: heals foreseen", playedHealed);
        Add("played: life the reply takes", Math.Max(0, playedLost));
        Add("planned: damage foreseen", plannedDealt);
        Add("planned: kills foreseen", plannedKills);
        Add("planned: heals foreseen", plannedHealed);
        Add("planned: life the reply takes", Math.Max(0, plannedLost));

        // What the character's summons deal in the reply, right after it and before the enemies (9f2).
        Add("played: damage its summons foresee in the reply", Dealt(played, playedReply));
        Add("planned: damage its summons foresee in the reply", Dealt(planned, plannedReply));

        // Over two turns: this turn's damage, then the next turn's as the search plays it from where the reply left the fight.
        var later = turn.Knowledge with { Cooldowns = turn.Knowledge.Cooldowns.Where(pair => pair.Value > 1).ToImmutableDictionary(pair => pair.Key, pair => pair.Value - 1) };
        var (playedNext, _) = _tactics.NextTurn(playedReply, later, judge, _limits);
        var (plannedNext, _) = _tactics.NextTurn(plannedReply, later, judge, _limits);

        Add("over two turns: played, damage foreseen", playedDealt + Dealt(playedReply, playedNext));
        Add("over two turns: planned, damage foreseen", plannedDealt + Dealt(plannedReply, plannedNext));
        Add("over two turns: played, kills foreseen", playedKills + Kills(playedReply, playedNext));
        Add("over two turns: planned, kills foreseen", plannedKills + Kills(plannedReply, plannedNext));
        Add("judged: played", judge.JudgeReplied(playedReply));
        Add("judged: planned", judge.JudgeReplied(plannedReply));

        Tally.Count($"against the turn played: damage {(plannedDealt > playedDealt + 0.5 ? "more" : plannedDealt < playedDealt - 0.5 ? "less" : "as much")}");
        Tally.Count($"against the turn played: kills {(plannedKills > playedKills ? "more" : plannedKills < playedKills ? "fewer" : "as many")}");
        Tally.Count($"against the turn played: life the reply takes {(plannedLost < playedLost - 0.5 ? "less" : plannedLost > playedLost + 0.5 ? "more" : "as much")}");
    }

    private void Settle(Ending ending, ImaginedFight? next, Fighter? over = null)
    {
        // The life the character lost from its turn's end to its next turn's start, or to the fight's end.
        var now = next is not null ? Effective(next.Actor) : over is { IsAlive: true } ? over.Life : 0;
        var lost = Math.Max(0, ending.Life - now);

        Tally.Count($"threat: {Band(ending.Threat, lost)}");
        Tally.Count($"reply: {Band(ending.Replied, lost)}");
        Add("threat: life lost", lost);
        Add("threat: foreseen", ending.Threat);
        Add("threat: off by", Math.Abs(ending.Threat - lost));
        Add("reply: foreseen", ending.Replied);
        Add("reply: off by", Math.Abs(ending.Replied - lost));

    }

    private static string Band(double foreseen, double lost)
    {
        if (foreseen < 1 && lost < 1)
            return "none foreseen, none lost";

        if (foreseen < 1)
            return "none foreseen, some lost";

        if (lost < 1)
            return "some foreseen, none lost";

        var ratio = foreseen / lost;

        return ratio < 0.5 ? "foreseen under half" : ratio > 2 ? "foreseen over twice" : "foreseen within half to twice";
    }

    private sealed class Turn
    {
        public Turn(ImaginedFight start, FightKnowledge knowledge, (int, int) points)
        {
            Start = start;
            Knowledge = knowledge;
            Points = points;
        }

        public ImaginedFight Start { get; }

        public FightKnowledge Knowledge { get; }

        public (int, int) Points { get; }

        public TurnPlan? Planned { get; set; }

        public List<TacticAction> Played { get; } = [];

        public int WeaponBlows { get; set; }

        public double? Foreseen { get; set; }

        public double? Searched { get; set; }
    }

    private sealed record Ending(double Life, double Threat, double Replied, double NextTurn, double Searched);
}
