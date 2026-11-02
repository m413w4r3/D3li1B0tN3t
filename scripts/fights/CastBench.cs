using System.Collections.Immutable;
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
using FightBuff = D3B.Protocol.Game.FightBuff;

namespace D3B.Scripts.Fights;

/// <summary>Replays fights through the bot's fight state, and compares each cast a fighter chose with what the server announced.</summary>
/// <remarks>
/// A cast a fighter chose is the first of its sequence of a spell (client.md): what follows until that sequence closes is
/// the cast's, the casts its effects trigger included, which the counts tell apart. D3B foresees the cast from the fight as
/// it stood before it, through <see cref="SpellEffects"/>; the server's actions are grouped by target and kind and set
/// against it. Each summon is set against the fighter D3B.Engine imagines from its grade and its summoner (D269), and the
/// character's casts are foreseen again with the heals and critical damage the server gives out of fights only (D272).
/// </remarks>
internal sealed class CastBench
{
    private const int CriticalHits = 18;

    // What the server gives the character out of fights and never in one (D263): heals and the critical hits' damage.
    private static readonly int[] _outOfFights = [FightCharacteristic.HealBonus, FightCharacteristic.CriticalDamageBonus];

    private readonly GameTables _tables;
    private readonly GameMaps _maps;
    private readonly WeaponBlows _blows;
    private readonly CastRules _rules;
    private readonly SpellEffects _effects;
    private readonly EffectActions _actions;
    private readonly Bombs _bombs;
    private readonly Imagination _imagination;
    private readonly Glyphs _glyphs;
    private readonly TextWriter? _details;
    private readonly Dictionary<long, FightMap> _fightMaps = [];
    private readonly Dictionary<(long Fighter, int Spell), int> _turnsEnded = [];
    private Starting? _starting;

    public Tally Tally { get; } = new();

    public CastBench(GameTables tables, GameMaps maps, TextWriter? details)
    {
        _tables = tables;
        _maps = maps;
        _blows = new WeaponBlows(tables.Spells, tables.Weapons, tables.Effects);
        _rules = new CastRules(_blows.Spells, tables.States);
        _actions = new EffectActions(tables.Effects);
        _effects = new SpellEffects(_blows, _rules, _actions);
        _bombs = new Bombs(_blows.Spells, _rules);
        _glyphs = new Glyphs(_blows.Spells);
        _imagination = new Imagination(_blows.Spells, tables.Monsters, _actions, _effects, _bombs, _glyphs, _rules);
        _details = details;
    }


    private static string Outcome(LifeRange foreseen, int dealt, bool capped)
    {
        if (foreseen.Contains(dealt))
            return "within";

        if (capped && dealt <= foreseen.Max)
            return "within, capped by the target's life";

        return dealt < foreseen.Min ? "below" : "above";
    }

    private static int Steps(int from, int to)
    {
        // A slide moves along one of the map's eight directions: a step across, between two cells' corners, is one cell
        // of distance 1, a step along a row or column of the screen two (MapGeometry, D186).
        var a = MapGeometry.PointOf(from);
        var b = MapGeometry.PointOf(to);

        return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    public void Replay(List<IMessage> messages)
    {
        var characters = CapturedFights.Characters(messages);
        var open = new List<Cast>();
        var levels = new Dictionary<(long Fighter, int Spell), SpellLevel>();
        FightState? fight = null;
        CharacterState? character = null;
        long self = 0, mapId = 0;

        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];

            // The character's state, for the weapon it wears (D218).
            if (message is CharacterSelectionEvent { Success.Character: { } chosen })
                character = CharacterState.Chosen(new CharacterKey(0, chosen.Id), chosen);
            else if (character is not null)
                character = CharacterEvents.Apply(character, RelayDirection.FromServer, message);

            if (message is MapCurrentEvent current)
                mapId = current.MapId;

            if (message is FightStartingEvent)
            {
                fight = new FightState();
                self = characters.GetValueOrDefault(index);
                open.Clear();
                levels.Clear();
                Tally.Count("fights: replayed");
            }

            if (fight is null)
                continue;

            Before(fight, message, mapId, open, levels, character);
            Starts(fight, message, mapId);

            var before = fight;

            FightEvents.TryApply(fight, self, message, out fight);

            if (message is GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Summons } summoning && summoning.Summons.Fighters is { } summoned)
                Summoned(before, fight, summoned);

            if (message is SequenceEndEvent)
            {
                foreach (var ended in open.Where(cast => fight.Sequences.Length < cast.Depth).ToList())
                {
                    Settle(ended, fight);
                    open.Remove(ended);
                }
            }

            if (message is FightEndEvent)
            {
                SettleStart();
                fight = null;
            }
        }
    }

    private void Starts(FightState fight, IMessage message, long mapId)
    {
        // The marks on a fighter's cell as its turn begins, a glyph or a bombs' wall (9f4): which struck it and the life it
        // lost before its turn's own effects, against what D3B.Engine imagines of them.
        switch (message)
        {
            case FightTurnEvent turn when fight.Phase is FightPhase.Fighting && fight.Fighters.TryGetValue(turn.FighterId, out var fighter) && fighter.IsAlive && fighter.Cell is not null:
            {
                SettleStart();

                var fighters = FightCombatants.Of(fight, _tables.Monsters);
                var imagined = new ImaginedFight
                {
                    Map = MapOf(mapId),
                    Fighters = [.. fighters],
                    ActorId = turn.FighterId,
                    Round = turn.Round,
                    Marks = ImaginedFights.MarksOf(fight, _glyphs),
                };
                var before = imagined.Actor;
                var after = _imagination.TurnStarted(imagined, turn.FighterId, turn.Round).Find(turn.FighterId);
                var covering = imagined.Marks.Count(mark => mark.Kind is CellMarkKind.TurnStart && mark.Covers(before.Cell));
                var walls = new List<BombWallCell>();

                _bombs.Walls(imagined.Map, imagined.Fighters, walls);

                if (!_bombs.IsBomb(before, out _) && walls.Exists(wall => wall.Cell == before.Cell))
                    covering++;

                _starting = new Starting(turn.FighterId, covering, Effective(before) - (after is null ? 0 : Effective(after)));

                break;
            }

            case GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.MarkStruck } struck when _starting is { } starting && struck.MarkStruck.FighterId == starting.FighterId:
                if (struck.ActionId is 307)
                    starting.Struck++;
                else
                    starting.Entered++;

                break;

            case GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.LifeLost } lost when _starting is { } starting && lost.LifeLost.TargetId == starting.FighterId:
                starting.Lost += lost.LifeLost.Loss + (lost.LifeLost.HasShieldLoss ? lost.LifeLost.ShieldLoss : 0);

                break;

            case SequenceStartEvent { Type: not SequenceType.Value2 and not SequenceType.Triggered }:
            case FightTurnEndEvent:
                SettleStart();

                break;
        }
    }

    private static double Effective(Combatant fighter)
    {
        return fighter.IsAlive ? fighter.Life + Math.Max(0, fighter.Characteristic(FightCharacteristic.Shield)) : 0;
    }

    private void SettleStart()
    {
        if (_starting is not { } starting)
            return;

        _starting = null;

        if (starting.Covering is 0 && starting.Struck is 0)
            return;

        var marks = starting.Struck == starting.Covering ? "as many as imagined" : starting.Struck > starting.Covering ? "more than imagined" : "fewer than imagined";
        var outcome = Math.Abs(starting.Lost - starting.Imagined) <= Math.Max(2, starting.Lost / 10.0) ? "within a tenth" : starting.Imagined < starting.Lost ? "less imagined" : "more imagined";

        Tally.Count($"marks at a turn's start: struck {starting.Struck}, imagined {starting.Covering}, {marks}");
        Tally.Count($"marks at a turn's start: life, {outcome}");
        Tally.Count("marks at a turn's start: life announced", starting.Lost);
        Tally.Count("marks at a turn's start: life imagined", (int)Math.Round(starting.Imagined));

        if (outcome is not "within a tenth" || marks is not "as many as imagined")
            _details?.WriteLine($"marks at a turn's start: struck {starting.Struck} imagined {starting.Covering}, lost {starting.Lost} against {starting.Imagined:F0} imagined");
    }

    private void Summoned(FightState before, FightState after, SummonedFighters summoned)
    {
        // Each summon as D3B.Engine imagines it from its monster's grade and its summoner (Summons, 9f2), against the
        // characteristics the server gave it.
        foreach (var summon in summoned.Summons)
        {
            if (summon.Positions.FirstOrDefault()?.Position is not { } position || !after.Fighters.TryGetValue(position.Id, out var given)
                || given is not { MonsterId: { } monsterId, Grade: { } grade, SummonerId: { } summonerId } || !before.Fighters.TryGetValue(summonerId, out var summoner)
                || !_tables.Monsters.TryGetMonster(monsterId, out var monster) || !_tables.Monsters.TryGetGrade(monsterId, grade, out var found))
            {
                Tally.Count("summons: not compared");

                continue;
            }

            var server = FightCombatants.Of(given, _tables.Monsters);
            var imagined = Summons.Of(FightCombatants.Of(summoner, _tables.Monsters), monster, found, server.Id, server.Cell);
            var by = summoner.BreedId is not null ? "a character" : "a monster";

            Tally.Count($"summons: by {by}, {((monster.Properties & Data.Monsters.MonsterProperties.CanPlay) is 0 ? "not playing" : "playing")}");
            Tally.Count($"summons: life {(imagined.MaxLife == server.MaxLife ? "as foreseen" : "otherwise")}");
            Tally.Count($"summons: AP {(imagined.ActionPoints == server.ActionPoints ? "as foreseen" : "otherwise")}");
            Tally.Count($"summons: MP {(imagined.MovementPoints == server.MovementPoints ? "as foreseen" : "otherwise")}");

            foreach (var id in server.Characteristics.Keys.Union(imagined.Characteristics.Keys).Where(id => id is not (0 or 1 or 23)))
            {
                // What the server gives every fighter alike, the damage and heals multipliers and erosion, D3B never reads off a grade.
                if (id is FightCharacteristic.Erosion or 93 or >= 100)
                    continue;

                Tally.Count($"summons: characteristics {(imagined.Characteristic(id) == server.Characteristic(id) ? "as foreseen" : $"otherwise, {id}")}");
            }
        }
    }

    private void Before(FightState fight, IMessage message, long mapId, List<Cast> open, Dictionary<(long, int), SpellLevel> levels, CharacterState? character)
    {
        // Where the state holds each fighter, set against where the server says it moves from: a slide's first cell and a
        // walk's, after every move the state follows.
        if (message is GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Slide } slid)
            Tally.Count($"positions: a slide from {(fight.Fighters.TryGetValue(slid.Slide.TargetId, out var slider) && slider.Cell == slid.Slide.FromCell ? "the cell the state holds" : "another cell")}");

        if (message is MapMovementEvent walked && walked.Cells.Count > 0 && fight.Phase is FightPhase.Fighting)
            Tally.Count($"positions: a walk from {(fight.Fighters.TryGetValue(walked.ActorId, out var walker) && walker.Cell == walked.Cells[0] ? "the cell the state holds" : "another cell")}");

        switch (message)
        {
            case GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Cast } action when action.Cast.Spell is not null
                || fight.Sequences is [.., { Type: SequenceType.Weapon, HasCast: false }]:
            {
                var struck = action.Cast.Spell is null;
                var chosen = fight.Sequences is [.., { Type: SequenceType.Spell or SequenceType.Weapon, HasCast: false } innermost] && innermost.AuthorId == action.SourceId;

                if (!chosen)
                {
                    foreach (var cast in open)
                        cast.TriggeredCasts++;

                    break;
                }

                SpellLevel level;

                if (struck)
                {
                    // A weapon's blow names no spell nor level (D218): the worn weapon's blow, when D3B strikes that weapon's.
                    var weapon = action.SourceId == character?.Key.CharacterId ? character.Weapon : null;
                    var kind = weapon is null ? "no weapon worn" : _tables.Weapons.TryGetWeapon(weapon.ItemId, out var held) ? $"a weapon of type {held.TypeId}" : "an item that is no weapon";

                    Tally.Count($"weapon's blows: struck with {kind}");

                    if (weapon is null || !_blows.TryGetBlow(weapon.ItemId, out level))
                        break;
                }
                else if (!_blows.Spells.TryGetLevel(action.Cast.Spell!.LevelId, out level))
                {
                    Tally.Count("casts: level or caster unknown");

                    break;
                }

                if (!fight.Fighters.TryGetValue(action.SourceId, out var caster))
                {
                    Tally.Count("casts: level or caster unknown");

                    break;
                }

                levels[(action.SourceId, level.SpellId)] = level;
                _turnsEnded[(action.SourceId, level.SpellId)] = 0;
                var map = MapOf(mapId);
                var fighters = FightCombatants.Of(fight, _tables.Monsters);
                var combatant = fighters.First(fighter => fighter.Id == action.SourceId);
                var refusal = _rules.Check(map, fighters, combatant, level, action.Cast.CellId, caster.Casts, fight.Round);

                Tally.Count($"rules of the casts chosen: {(refusal is CastRefusal.None ? "accepted" : $"refused, {refusal}")}");

                if (refusal is not CastRefusal.None)
                    _details?.WriteLine($"refused {refusal}: spell {level.SpellId} level {level.Id}, states {string.Join(",", combatant.States)}");

                if (struck)
                {
                    Tally.Count($"weapon's blows: the rules {(refusal is CastRefusal.None ? "accept" : $"refuse, {refusal}")}");
                    Tally.Count($"weapon's blows: {(action.Cast.Critical is FightCastCritical.Critical ? "critical" : "normal")}");
                }

                Tally.Count($"casts chosen: {(action.Cast.Critical is FightCastCritical.Critical ? "critical" : "normal")}");

                // The chance of a critical hit (D207): the level's, plus the caster's characteristic 18, by bands of ten.
                if (level.CriticalHitProbability > 0)
                {
                    var chance = Math.Clamp(level.CriticalHitProbability + combatant.Characteristic(CriticalHits), 0, 100);
                    var band = Math.Min(chance / 10 * 10, 90);

                    Tally.Count($"critical hits by the chance foreseen: {band,2}-{band + 9}%, casts");
                    Tally.Count($"critical hits by the chance foreseen: {band,2}-{band + 9}%, critical", action.Cast.Critical is FightCastCritical.Critical ? 1 : 0);
                    Tally.Count($"critical hits by the chance foreseen: {band,2}-{band + 9}%, chance foreseen summed", chance);
                    Tally.Count($"critical hits by the caster's characteristic 18: {Math.Sign(combatant.Characteristic(CriticalHits)),2}, casts");
                    Tally.Count($"critical hits by the caster's characteristic 18: {Math.Sign(combatant.Characteristic(CriticalHits)),2}, critical", action.Cast.Critical is FightCastCritical.Critical ? 1 : 0);
                    Tally.Count($"critical hits by the caster's characteristic 18: {Math.Sign(combatant.Characteristic(CriticalHits)),2}, the level's chance summed", level.CriticalHitProbability);
                    Tally.Count($"critical hits by the caster's characteristic 18: {Math.Sign(combatant.Characteristic(CriticalHits)),2}, the characteristic summed", combatant.Characteristic(CriticalHits));
                }
                open.Add(new Cast
                {
                    Caster = combatant,
                    OutOfFights = action.SourceId == character?.Key.CharacterId ? OutOfFights(combatant, character) : null,
                    Fighters = fighters,
                    Map = map,
                    Level = level,
                    Cell = action.Cast.CellId,
                    Critical = action.Cast.Critical is FightCastCritical.Critical,
                    Depth = fight.Sequences.Length,
                    Round = fight.Round,
                    Turn = fight.PlayingTurn,
                    Lives = fight.Fighters.Values.ToDictionary(fighter => fighter.Id, fighter => (fighter.Life, fighter.MaxLife)),
                    Held = fight.Fighters.Values.ToDictionary(fighter => fighter.Id, fighter => fighter.Buffs),
                });

                break;
            }

            case GameActionFightEvent action when open.Count > 0:
            {
                foreach (var cast in open)
                    cast.Actions.Add(action);

                break;
            }

            case FightEffectsEvent { Given.Buff: { } buff } given when open.Count > 0:
            {
                foreach (var cast in open)
                    cast.Buffs.Add((given.Given.ActionId, buff));

                break;
            }

            case FightTurnEndEvent ended:
            {
                foreach (var key in _turnsEnded.Keys.Where(key => key.Fighter == ended.FighterId).ToList())
                    _turnsEnded[key]++;

                break;
            }

            case RefreshCooldownEvent cooldowns when fight.Fighters.TryGetValue(cooldowns.FighterId, out var fighter):
            {
                foreach (var cooldown in cooldowns.Cooldowns)
                {
                    if (!levels.TryGetValue((fighter.Id, cooldown.SpellId), out var level))
                    {
                        Tally.Count($"cooldowns: of a spell not cast yet, {(cooldown.Rounds is 0 ? "0" : "more")}");

                        continue;
                    }

                    // Measured (D193): the server counts a spell's interval down as each of its caster's turns ends.
                    var ended = _turnsEnded.GetValueOrDefault((fighter.Id, cooldown.SpellId));
                    var expected = level.MinCastInterval > 0 && fighter.Casts.LastRounds.ContainsKey(cooldown.SpellId) ? Math.Max(0, level.MinCastInterval - ended) : 0;

                    Tally.Count($"cooldowns: {(cooldown.Rounds == expected ? "as the turns ended since the cast say" : $"off by {cooldown.Rounds - expected}")}");
                }

                break;
            }
        }
    }

    private static Combatant? OutOfFights(Combatant caster, CharacterState character)
    {
        // The character as the AI would see it with what the server gives out of fights only, when it changes something.
        var characteristics = caster.Characteristics;

        foreach (var id in _outOfFights)
        {
            if (caster.Characteristic(id) is 0 && character.Characteristics.GetValueOrDefault(id) is not 0 and var value)
                characteristics = characteristics.SetItem(id, value);
        }

        return ReferenceEquals(characteristics, caster.Characteristics) ? null : caster with { Characteristics = characteristics };
    }

    private FightMap MapOf(long mapId)
    {
        if (!_fightMaps.TryGetValue(mapId, out var map))
            _fightMaps[mapId] = map = FightMap.Of(_maps.Get(mapId));

        return map;
    }

    private void Settle(Cast cast, FightState after)
    {
        var foreseen = _effects.Foresee(cast.Map, cast.Fighters, cast.Caster, cast.Level, cast.Cell, cast.Critical);
        var alone = cast.TriggeredCasts is 0 ? "alone" : "with the casts it triggered";

        if (WeaponBlows.IsBlow(cast.Level))
        {
            var spent = -cast.Actions.Where(action => action.ActionId is 102 && action.ActionCase is GameActionFightEvent.ActionOneofCase.Points && action.Points.TargetId == cast.Caster.Id).Sum(action => action.Points.Delta);

            Tally.Count($"weapon's blows: AP spent {(spent == cast.Level.ApCost ? "as the weapon costs" : $"off by {spent - cast.Level.ApCost}")}");
            alone = "a weapon's blow";
        }

        States(cast, after);
        Damage(cast, foreseen, alone);
        Heals(cast, foreseen, alone);

        // The character's casts, foreseen from the characteristics the fight gives, then with heals and the critical hits'
        // damage from out of fights (D263, 9f2).
        if (cast.OutOfFights is { } fuller && cast.TriggeredCasts is 0)
        {
            var completed = _effects.Foresee(cast.Map, cast.Fighters, fuller, cast.Level, cast.Cell, cast.Critical);
            var kind = cast.Critical ? "critical" : "normal";

            Damage(cast, foreseen, $"the character's {kind}, as in fights", main: false);
            Damage(cast, completed, $"the character's {kind}, out of fights", main: false);
            Heals(cast, foreseen, $"the character's {kind}, as in fights", main: false);
            Heals(cast, completed, $"the character's {kind}, out of fights", main: false);
        }
        Pushes(cast, foreseen);
        Points(cast, foreseen, alone);
        Buffs(cast, foreseen);
        Moves(cast, foreseen);
    }

    private void Damage(Cast cast, IReadOnlyList<ForeseenEffect> foreseen, string alone, bool main = true)
    {
        var dealt = new Dictionary<(long Target, EffectElement Element), (int Total, bool Capped)>();

        foreach (var action in cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.LifeLost && action.LifeLost.Element >= 0))
        {
            var lost = action.LifeLost;
            var key = (lost.TargetId, (EffectElement)lost.Element);
            var held = dealt.GetValueOrDefault(key);
            var life = cast.Lives.TryGetValue(lost.TargetId, out var before) ? before.Life : int.MaxValue;
            var total = held.Total + lost.Loss + (lost.HasShieldLoss ? lost.ShieldLoss : 0);

            dealt[key] = (total, held.Capped || total >= life);
        }

        var expected = new Dictionary<(long Target, EffectElement Element), LifeRange>();

        foreach (var effect in foreseen.Where(effect => effect.IsImmediate && effect.Action.Kind is EffectKind.Damage or EffectKind.Steal or EffectKind.FixedDamage))
        {
            var key = (effect.TargetId, effect.Element);
            var held = expected.GetValueOrDefault(key);
            var surely = effect.IsCertain && !effect.IsRandom;

            expected[key] = new LifeRange(held.Min + (surely ? effect.Value.Min : 0), held.Max + effect.Value.Max);
        }

        foreach (var key in dealt.Keys.Union(expected.Keys))
        {
            var (total, capped) = dealt.GetValueOrDefault(key);

            if (!expected.TryGetValue(key, out var range))
            {
                Tally.Count($"damage, {alone}: dealt, not foreseen");
                _details?.WriteLine($"damage not foreseen: spell {cast.Level.SpellId} level {cast.Level.Id} element {key.Element}");

                continue;
            }

            if (!dealt.ContainsKey(key))
            {
                Tally.Count($"damage, {alone}: {(range.Min > 0 ? "foreseen, not dealt" : "maybe, not dealt")}");

                continue;
            }

            var outcome = Outcome(range, total, capped);

            Tally.Count($"damage, {alone}: {outcome}");

            if (cast.TriggeredCasts is 0 && main)
                Tally.Count($"damage alone, by element: {key.Element}, {outcome}");

            if (outcome is "below" or "above" && main)
                _details?.WriteLine($"damage {outcome}: spell {cast.Level.SpellId} level {cast.Level.Id}{(cast.Critical ? " critical" : "")} element {key.Element}: {total} against {range.Min}..{range.Max}, {alone}; {Why(cast, foreseen, key.Target, key.Element)}");
        }
    }

    private string Why(Cast cast, IReadOnlyList<ForeseenEffect> foreseen, long targetId, EffectElement element)
    {
        var caster = cast.Caster;
        var target = cast.Fighters.First(fighter => fighter.Id == targetId);
        var stat = EffectFormulas.CharacteristicOf(element);
        var offset = element is EffectElement.Neutral ? 4 : (int)element - 1;
        var rolls = string.Join(" ", _blows.Spells.EffectsOf(cast.Level).ToArray().Where(effect => foreseen.Any(found => found.TargetId == targetId && found.EffectId == effect.EffectId && found.Element == element))
            .Select(effect => $"e{effect.EffectId}:{effect.DiceNum}-{effect.DiceSide}/{(char)effect.Zone.Shape}{effect.Zone.Size},{effect.Zone.MinSize},{effect.Zone.DamageDecreaseStepPercent}x{effect.Zone.MaxDamageDecreaseApplyCount} r{effect.Random} g{effect.Group} t{_blows.Spells.TriggersOf(effect)}"));
        var changed = string.Join(",", caster.Characteristics.Where(pair => pair.Key is >= 100 and <= 160 && pair.Value is not 100 and not 0).Select(pair => $"{pair.Key}={pair.Value}"));
        var targetChanged = string.Join(",", target.Characteristics.Where(pair => pair.Key is >= 100 and <= 160 && pair.Value is not 100 and not 0).Select(pair => $"{pair.Key}={pair.Value}"));

        return $"caster {(caster.IsCharacter ? "character" : caster.IsSummon ? "summon" : "monster")} lvl {caster.Level} c={caster.Characteristic(stat)} p={caster.Characteristic(25)} all={caster.Characteristic(16)} el={caster.Characteristic(88 + offset)} crit={caster.Characteristic(86)} [{changed}] mods {caster.Modifiers.Length}; "
            + $"all caster [{string.Join(",", caster.Characteristics.Where(pair => pair.Value != 0).Select(pair => $"{pair.Key}={pair.Value}"))}] losses [{string.Join(",", cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.LifeLost && action.LifeLost.TargetId == targetId && action.LifeLost.Element == (int)element).Select(action => $"{action.ActionId}:{action.LifeLost.Loss}+{action.LifeLost.ShieldLoss}"))}] "
            + $"caster buffs [{string.Join(",", cast.Held.GetValueOrDefault(caster.Id).Select(buff => $"{buff.ActionId}/{buff.Kind}:{buff.Value}"))}] target buffs [{string.Join(",", cast.Held.GetValueOrDefault(targetId).Select(buff => $"{buff.ActionId}/{buff.Kind}:{buff.Value}"))}] "
            + $"target {(target.IsCharacter ? "character" : target.IsSummon ? "summon" : "monster")} res%={target.Characteristic(33 + offset)} fixed={target.Characteristic(54 + offset)} [{targetChanged}] away {(target.Cell < 0 ? -1 : MapGeometry.Distance(cast.Cell, target.Cell))} melee {(caster.Cell >= 0 && target.Cell >= 0 ? MapGeometry.Distance(caster.Cell, target.Cell) : -1)}; {rolls}";
    }

    private void Heals(Cast cast, IReadOnlyList<ForeseenEffect> foreseen, string alone, bool main = true)
    {
        var healed = new Dictionary<long, (int Total, bool Capped)>();

        foreach (var action in cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.LifeGained))
        {
            var gained = action.LifeGained;
            var held = healed.GetValueOrDefault(gained.TargetId);
            var missing = cast.Lives.TryGetValue(gained.TargetId, out var before) ? before.MaxLife - before.Life : int.MaxValue;
            var total = held.Total + gained.Amount;

            healed[gained.TargetId] = (total, held.Capped || total >= missing);
        }

        var expected = new Dictionary<long, LifeRange>();

        foreach (var effect in foreseen.Where(effect => effect.IsImmediate && effect.Action.Kind is EffectKind.Heal or EffectKind.FixedHeal or EffectKind.PercentHeal))
        {
            var held = expected.GetValueOrDefault(effect.TargetId);
            var surely = effect.IsCertain && !effect.IsRandom;

            expected[effect.TargetId] = new LifeRange(held.Min + (surely ? effect.Value.Min : 0), held.Max + effect.Value.Max);
        }

        // A steal heals its caster half the life it took, rounded down, of each target (D195).
        foreach (var effect in foreseen.Where(effect => effect.IsImmediate && effect.Action.Kind is EffectKind.Steal).DistinctBy(effect => (effect.TargetId, effect.Element)))
        {
            var taken = cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.LifeLost && action.LifeLost.TargetId == effect.TargetId && action.LifeLost.Element == (int)effect.Element).Sum(action => action.LifeLost.Loss);
            var held = expected.GetValueOrDefault(cast.Caster.Id);

            expected[cast.Caster.Id] = new LifeRange(held.Min + (taken / 2), held.Max + (taken / 2));

            if (main)
                Tally.Count($"steals: foreseen, {(taken > 0 ? "life taken" : "no life taken")}");
        }

        foreach (var target in healed.Keys.Union(expected.Keys))
        {
            var (total, capped) = healed.GetValueOrDefault(target);

            if (!expected.TryGetValue(target, out var range))
            {
                Tally.Count($"heals, {alone}: healed, not foreseen");

                continue;
            }

            if (!healed.ContainsKey(target))
            {
                var whole = cast.Lives.TryGetValue(target, out var lives) && lives.Life >= lives.MaxLife;

                Tally.Count($"heals, {alone}: {(whole ? "foreseen, the target whole" : range.Min > 0 ? "foreseen, not healed" : "maybe, not healed")}");

                if (!whole && range.Min > 0)
                    _details?.WriteLine($"heal not announced: spell {cast.Level.SpellId} level {cast.Level.Id}, {(target == cast.Caster.Id ? "the caster" : "another")}");

                continue;
            }

            var outcome = Outcome(range, total, capped);

            Tally.Count($"heals, {alone}: {outcome}");

            if (outcome is "below" or "above")
                _details?.WriteLine($"heal {outcome}: spell {cast.Level.SpellId} level {cast.Level.Id}: {total} against {range.Min}..{range.Max}, {alone}");
        }
    }

    private void Pushes(Cast cast, IReadOnlyList<ForeseenEffect> foreseen)
    {
        ImaginedFight? imagined = null;

        foreach (var push in foreseen.Where(effect => effect.IsImmediate && effect.IsCertain && effect.Action.Kind is EffectKind.Push or EffectKind.Pull))
        {
            // Where the target ends, as the server moved it and as D3B imagines the whole cast, blocks and walls counted (9f4).
            imagined ??= _imagination.Cast(new ImaginedFight { Map = cast.Map, Fighters = [.. cast.Fighters], ActorId = cast.Caster.Id, Round = cast.Round, History = CastHistory.Empty }, cast.Level, cast.Cell);

            var moves = cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Slide && action.Slide.TargetId == push.TargetId).ToList();
            var from = cast.Fighters.FirstOrDefault(fighter => fighter.Id == push.TargetId)?.Cell ?? Combatant.NoCell;
            var landed = moves.Count > 0 ? moves[^1].Slide.ToCell : from;
            var thought = imagined.Find(push.TargetId) is { IsAlive: true } there ? there.Cell : Combatant.NoCell;

            if (from is not Combatant.NoCell && landed is not Combatant.NoCell && thought is not Combatant.NoCell)
            {
                var moved = Steps(from, landed);
                var foreseenSteps = Steps(from, thought);
                var against = landed == thought ? "on the cell imagined" : moved < foreseenSteps ? "short of the cell imagined" : moved > foreseenSteps ? "beyond the cell imagined" : "on another cell as far";

                Tally.Count($"pushes and pulls: {(push.Action.Kind is EffectKind.Push ? "push" : "pull")}, {against}");

                if (landed != thought)
                    _details?.WriteLine($"push or pull off: spell {cast.Level.SpellId} effect {push.EffectId}, {push.Value.Min} cells, moved {moved} imagined {foreseenSteps}, slides {moves.Count}, target {(cast.Fighters.First(fighter => fighter.Id == push.TargetId).IsCharacter ? "character" : "monster")}, next cell {Next(cast, from, landed)}, {Geometry(cast, from, landed, thought)}");
            }

            var slides = cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Slide && action.Slide.TargetId == push.TargetId).ToList();
            var steps = slides.Sum(slide => Steps(slide.Slide.FromCell, slide.Slide.ToCell));
            var cells = push.Value.Min;
            var kind = push.Action.Kind is EffectKind.Push ? "push" : "pull";
            var died = cast.Actions.Any(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Death && action.Death.TargetId == push.TargetId);

            if (died && slides.Count is 0)
            {
                Tally.Count($"pushes and pulls: {kind}, the target dead first");

                continue;
            }

            Tally.Count($"pushes and pulls: {kind}, {(steps == cells ? "as far as foreseen" : steps < cells ? "stopped short" : "farther than foreseen")}");

            var damage = cast.Actions.Where(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.LifeLost && action.LifeLost.TargetId == push.TargetId && action.LifeLost.Element < 0).Sum(action => action.LifeLost.Loss + (action.LifeLost.HasShieldLoss ? action.LifeLost.ShieldLoss : 0));

            if (push.Action.Kind is not EffectKind.Push || push.EffectId is not 5)
                continue;

            var target = cast.Fighters.FirstOrDefault(fighter => fighter.Id == push.TargetId);
            var level = cast.Caster.SummonerId is { } summoner && cast.Fighters.FirstOrDefault(fighter => fighter.Id == summoner) is { } owner ? owner.Level : cast.Caster.Level;

            if (target is null)
                continue;

            var formula = steps < cells ? EffectFormulas.PushDamage(cells - steps, cast.Caster, level, target) : 0;
            var capped = cast.Lives.TryGetValue(target.Id, out var before) && damage >= before.Life;

            Tally.Count($"push damage: {(formula == damage ? (damage is 0 ? "none, as foreseen" : "as foreseen") : capped && damage <= formula ? "as foreseen, capped by the target's life" : damage < formula ? "below" : "above")}");

            if (formula != damage && !capped)
                _details?.WriteLine($"push damage: spell {cast.Level.SpellId}, {cells} cells, {steps} moved: {damage} against {formula}; target {(target.IsCharacter ? "character" : "monster")} lvl {target.Level} props {target.MonsterProperties} states {string.Join(",", target.States)} res85 {target.Characteristic(85)} caster lvl {level} bonus84 {cast.Caster.Characteristic(84)} shares {string.Join(",", target.ReceivedDamageShares)} slides {cast.Actions.Count(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Slide)} hits80 {cast.Actions.Count(action => action.ActionId is 80)}");
        }
    }

    private static string Geometry(Cast cast, int from, int landed, int thought)
    {
        // The target's place against the caster and the cell cast on, and where it went and was imagined to, by coordinates.
        var at = MapGeometry.PointOf(from);
        string Off(int cell)
        {
            var point = MapGeometry.PointOf(cell);

            return $"({point.X - at.X},{point.Y - at.Y})";
        }

        return $"caster {Off(cast.Caster.Cell)} cast on {Off(cast.Cell)} went {Off(landed)} imagined {Off(thought)}";
    }

    private static string Next(Cast cast, int from, int landed)
    {
        // What stands on the cell past the one the server stopped on, along the slide.
        var a = MapGeometry.PointOf(from);
        var b = MapGeometry.PointOf(landed);

        if (a == b)
            return "none, not moved";

        if (!MapGeometry.TryGetCell(new MapPoint(b.X + Math.Sign(b.X - a.X), b.Y + Math.Sign(b.Y - a.Y)), out var next))
            return "off the map";

        return !cast.Map.IsWalkable(next) ? "not walkable" : cast.Fighters.Any(fighter => fighter.IsAlive && fighter.Cell == next) ? "a fighter" : "free";
    }

    private void Points(Cast cast, IReadOnlyList<ForeseenEffect> foreseen, string alone)
    {
        foreach (var (characteristic, lostId, dodgedId, name) in new[] { (1, 101, 308, "AP"), (23, 127, 309, "MP") })
        {
            var lost = cast.Actions.Where(action => action.ActionId == lostId && action.ActionCase is GameActionFightEvent.ActionOneofCase.Points).GroupBy(action => action.Points.TargetId).ToDictionary(group => group.Key, group => -group.Sum(action => action.Points.Delta));
            var dodged = cast.Actions.Where(action => action.ActionId == dodgedId && action.ActionCase is GameActionFightEvent.ActionOneofCase.PointsDodged).GroupBy(action => action.PointsDodged.TargetId).ToDictionary(group => group.Key, group => group.Sum(action => action.PointsDodged.Amount));
            var expected = foreseen.Where(effect => effect.IsImmediate && effect.Action.Kind is EffectKind.Deboost && effect.Action.Characteristic == characteristic)
                .GroupBy(effect => effect.TargetId).ToDictionary(group => group.Key, group => new LifeRange(group.Sum(effect => effect.Value.Min), group.Sum(effect => effect.Value.Max)));

            foreach (var target in lost.Keys.Union(dodged.Keys).Union(expected.Keys))
            {
                var taken = lost.GetValueOrDefault(target);
                var escaped = dodged.GetValueOrDefault(target);

                if (!expected.TryGetValue(target, out var range))
                {
                    Tally.Count($"{name} removed, {alone}: removed, not foreseen");

                    continue;
                }

                Tally.Count($"{name} removed, {alone}: {(range.Contains(taken + escaped) ? (escaped > 0 ? "within, some dodged" : "within") : taken + escaped < range.Min ? "below" : "above")}");
            }
        }
    }

    private bool CastsSpells(in SpellLevel level)
    {
        foreach (var effect in _blows.Spells.EffectsOf(level))
        {
            if (_actions.Of(effect.EffectId).Kind is EffectKind.CastSpell && effect.Delay is 0 && (effect.Properties & SpellEffectProperties.ForClientOnly) is 0 && _blows.Spells.TriggersOf(effect) == "I")
                return true;
        }

        return false;
    }

    private void States(Cast cast, FightState after)
    {
        // The states each fighter holds once the cast's sequence ended, against those the AI imagines the cast leaves, set and
        // removed by its effects and the spells they cast at once (D229); and the life the cast took, imagined and announced.
        var imagined = _imagination.Cast(new ImaginedFight { Map = cast.Map, Fighters = [.. cast.Fighters], ActorId = cast.Caster.Id, Round = cast.Round, History = CastHistory.Empty }, cast.Level, cast.Cell);
        var casts = CastsSpells(cast.Level) ? "casting spells at once" : "casting none";
        int lostImagined = 0, lostAnnounced = 0;

        foreach (var before in cast.Fighters)
        {
            if (!after.Fighters.TryGetValue(before.Id, out var now) || imagined.Find(before.Id) is not { } thought)
                continue;

            // A bomb dies of its explosion with its life whole in the fight's state, which the imagined fight empties (9f3).
            if (!_bombs.IsBomb(before, out _))
            {
                lostImagined += Math.Max(0, before.Life - thought.Life);
                lostAnnounced += Math.Max(0, cast.Lives[before.Id].Life - now.Life);
            }

            // A fighter that died lost its buffs with its life.
            if (!now.IsAlive)
                continue;

            var was = before.States.ToHashSet();
            var held = now.States.ToHashSet();
            var foreseen = thought.States.ToHashSet();
            var who = before.Id == cast.Caster.Id ? "the caster" : "another fighter";

            foreach (var state in was.Union(held).Union(foreseen))
            {
                var outcome = (was.Contains(state), held.Contains(state), foreseen.Contains(state)) switch
                {
                    (false, true, true) => "set as foreseen",
                    (false, true, false) => "set, not foreseen",
                    (false, false, true) => "foreseen set, not set",
                    (true, false, false) => "removed as foreseen",
                    (true, false, true) => "removed, not foreseen",
                    (true, true, false) => "foreseen removed, kept",
                    _ => null,
                };

                if (outcome is null)
                    continue;

                Tally.Count($"states a cast changed, {who}, {casts}: {outcome}");

                if (outcome is not ("set as foreseen" or "removed as foreseen"))
                    _details?.WriteLine($"state {state} {outcome}: spell {cast.Level.SpellId} level {cast.Level.Id}, {who}, {casts}");
            }
        }

        Bombs(cast, after, imagined);
        Tally.Count($"life a cast took, {casts}: casts");
        Tally.Count($"life a cast took, {casts}: imagined", lostImagined);
        Tally.Count($"life a cast took, {casts}: announced", lostAnnounced);
    }

    private void Bombs(Cast cast, FightState after, ImaginedFight imagined)
    {
        // With bombs on the field (9f3): the bombs each cast set off, imagined and announced, and the life every other fighter
        // lost, imagined from the explosions, their combo and the walls, and announced.
        if (!_bombs.Any(cast.Fighters) && !cast.Fighters.Any(fighter => fighter.Id == cast.Caster.Id && fighter.BreedId is 13))
            return;

        var exploded = cast.Fighters.Where(fighter => fighter.IsAlive && _bombs.IsBomb(fighter, out _)).ToList();
        var announced = exploded.Where(bomb => !after.Fighters.TryGetValue(bomb.Id, out var now) || !now.IsAlive).Select(bomb => bomb.Id).ToHashSet();
        var thought = exploded.Where(bomb => imagined.Find(bomb.Id) is not { IsAlive: true }).Select(bomb => bomb.Id).ToHashSet();

        if (announced.Count > 0 || thought.Count > 0)
        {
            var by = _bombs.IsBomb(cast.Caster, out _) ? "a bomb's explosion, alone" : $"spell {cast.Level.SpellId}";

            Tally.Count($"bombs: the bombs a cast set off, {by}: {(announced.SetEquals(thought) ? "as imagined" : announced.IsSupersetOf(thought) ? "more than imagined" : "otherwise")}");

            if (!announced.SetEquals(thought))
                _details?.WriteLine($"bombs set off: {by}, announced [{string.Join(",", exploded.Where(bomb => announced.Contains(bomb.Id)).Select(bomb => $"{bomb.MonsterId}@{bomb.Cell}"))}] imagined [{string.Join(",", exploded.Where(bomb => thought.Contains(bomb.Id)).Select(bomb => $"{bomb.MonsterId}@{bomb.Cell}"))}] of [{string.Join(",", exploded.Select(bomb => $"{bomb.MonsterId}@{bomb.Cell}"))}], cast on {cast.Cell}");
        }

        var walls = new List<BombWallCell>();

        _bombs.Walls(cast.Map, cast.Fighters, walls);
        Tally.Count($"bombs: casts with {(walls.Count > 0 ? "walls" : "no wall")}, {(announced.Count > 0 ? "setting bombs off" : "setting none off")}");

        foreach (var before in cast.Fighters)
        {
            if (!before.IsAlive || _bombs.IsBomb(before, out _) || !after.Fighters.TryGetValue(before.Id, out var now) || imagined.Find(before.Id) is not { } foreseen)
                continue;

            var lost = Math.Max(0, cast.Lives[before.Id].Life - now.Life);
            var thoughtLost = Math.Max(0, before.Life - foreseen.Life);

            if (lost is 0 && thoughtLost is 0)
                continue;

            var outcome = !now.IsAlive && thoughtLost >= lost ? "killed, as imagined"
                : Math.Abs(lost - thoughtLost) <= Math.Max(2, lost / 10) ? "within a tenth"
                : thoughtLost < lost ? "less imagined" : "more imagined";
            var what = _bombs.IsBomb(cast.Caster, out _) ? "a bomb's explosion, alone" : announced.Count > 0 ? $"spell {cast.Level.SpellId} setting bombs off" : walls.Count > 0 ? "with walls" : "no wall, none set off";

            Tally.Count($"bombs: a fighter's loss, {what}: {outcome}");
            Tally.Count($"bombs: a fighter's loss, {what}: announced", lost);
            Tally.Count($"bombs: a fighter's loss, {what}: imagined", thoughtLost);

            if (outcome is "less imagined" or "more imagined")
                _details?.WriteLine($"bombs: spell {cast.Level.SpellId} level {cast.Level.Id}, {what}, lost {lost} against {thoughtLost} imagined, {(before.Id == cast.Caster.Id ? "the caster" : before.Team == cast.Caster.Team ? "an ally" : "an enemy")}, set off {announced.Count} imagined {thought.Count}, walls {walls.Count} [{string.Join(",", walls.Select(wall => $"{wall.Cell}+{wall.Combo}"))}], from {before.Cell} to {(now.IsAlive ? now.Cell : -1)} imagined {foreseen.Cell}");
        }
    }

    private void Buffs(Cast cast, IReadOnlyList<ForeseenEffect> foreseen)
    {
        // A mask naming the caster in the zone only, c, with the caster outside it: the server buffs it or not (D226).
        foreach (var effect in _blows.Spells.EffectsOf(cast.Level))
        {
            var mask = _blows.Spells.TargetMaskOf(effect).Split(',');

            if (!mask.Contains("c") || mask.Contains("C") || _actions.Of(effect.EffectId).Kind is not (EffectKind.Boost or EffectKind.Deboost or EffectKind.StateSet or EffectKind.Shield or EffectKind.LifeShield or EffectKind.LevelShield))
                continue;

            var zone = ZoneCells.Of(effect.Zone, _blows.Spells.CellsOf(effect.Zone), cast.Map, CastRules.OccupiedBy(cast.Fighters), cast.Caster.Cell, cast.Cell);

            if (zone.Contains(cast.Caster.Cell))
                continue;

            var buffed = cast.Buffs.Any(pair => pair.Buff.TargetId == cast.Caster.Id && pair.Buff.EffectUid == effect.Uid);

            Tally.Count($"a mask naming the caster in its zone only, the caster outside: {(buffed ? "buffed" : "not buffed")}");
        }

        // The client's previews of buffs on the caster, which the server gives through a spell the cast triggers (D209).
        foreach (var effect in _blows.Spells.EffectsOf(cast.Level))
        {
            var kind = _actions.Of(effect.EffectId).Kind;

            if ((effect.Properties & SpellEffectProperties.ForClientOnly) is 0 || kind is not (EffectKind.Boost or EffectKind.Deboost or EffectKind.StateSet)
                || !_rules.MaskOf(effect).NamesTheCaster)
            {
                continue;
            }

            var characteristic = _actions.Of(effect.EffectId).Characteristic;
            var given = cast.Buffs.Where(pair => pair.Buff.TargetId == cast.Caster.Id && (pair.ActionId == effect.EffectId || (characteristic > 0 && _actions.Of(pair.ActionId).Characteristic == characteristic))).Select(pair => pair.Buff).ToList();

            if (given.Count is 0)
                _details?.WriteLine($"buff previewed, not given: spell {cast.Level.SpellId} effect {effect.EffectId} characteristic {characteristic}; given on the caster: {string.Join(", ", cast.Buffs.Where(pair => pair.Buff.TargetId == cast.Caster.Id).Select(pair => $"{pair.ActionId}/{_actions.Of(pair.ActionId).Characteristic}={pair.Buff.Value}"))}");
            var roll = EffectFormulas.Roll(effect);
            var outcome = given.Count is 0 ? "not given"
                : kind is EffectKind.StateSet ? given.Any(buff => buff.Parameter == effect.Value) ? "given as previewed" : "given, another state"
                : given.Any(buff => roll.Contains(Math.Abs(buff.Value))) ? "given as previewed" : "given, another value";

            Tally.Count($"buffs previewed for the client on the caster: {kind}, {outcome}");
        }

        var own = new HashSet<int>();

        foreach (var effect in _blows.Spells.EffectsOf(cast.Level))
            own.Add(effect.Uid);

        foreach (var effect in _blows.Spells.CriticalEffectsOf(cast.Level))
            own.Add(effect.Uid);

        foreach (var (actionId, buff) in cast.Buffs)
        {
            if (!own.Contains(buff.EffectUid))
            {
                Tally.Count("buffs: of an effect the cast triggered");

                continue;
            }

            var matching = foreseen.Where(effect => effect.TargetId == buff.TargetId && effect.EffectId == actionId).ToList();

            if (matching.Count is 0)
            {
                Tally.Count("buffs: of the level's effects, target not foreseen");
                _details?.WriteLine($"buff target not foreseen: spell {cast.Level.SpellId} level {cast.Level.Id} effect {actionId}, mask {_blows.Spells.TargetMaskOf(SourceOf(cast.Level, buff.EffectUid))}, target {(buff.TargetId == cast.Caster.Id ? "the caster" : "another")}");

                continue;
            }

            var effect = matching[0];
            var kind = effect.Action.Kind;
            var source = SourceOf(cast.Level, buff.EffectUid);
            var outcome = buff.Kind switch
            {
                FightBuffKind.State => buff.Parameter == source.Value ? "state as foreseen" : source.Value is 0 ? "state the effect leaves to the server" : "another state",
                FightBuffKind.SpellModifier => buff.Value == source.DiceNum && buff.Parameter == source.Value ? "spell modifier as foreseen" : "another spell modifier",
                FightBuffKind.Triggered => "triggered effect, kept",
                _ when kind is EffectKind.Boost or EffectKind.Deboost or EffectKind.Shield or EffectKind.LifeShield or EffectKind.LevelShield => matching.Any(found => found.Value.Contains(buff.Value)) ? $"{Name(kind)} within" : $"{Name(kind)} outside",
                _ => $"{kind}, kept",
            };

            Tally.Count($"buffs: {outcome}");

            if (outcome.EndsWith("outside", StringComparison.Ordinal) || outcome.StartsWith("another", StringComparison.Ordinal))
                _details?.WriteLine($"buff {outcome}: spell {cast.Level.SpellId} level {cast.Level.Id} effect {actionId}: value {buff.Value} parameter {buff.Parameter} against {effect.Value.Min}..{effect.Value.Max}, the effect's value {source.Value}");

            var rounds = source.Duration;
            var end = buff.End is { Round: >= 0 } at ? at.Round : -1;

            Tally.Count($"buffs' ends: {(rounds < 0 || end < 0 ? (rounds < 0 && end < 0 ? "the whole fight, as its effect lasts" : rounds <= 0 && end < 0 ? "the whole fight, its effect lasting 0" : "other") : end == cast.Round + rounds && buff.End.Turn == cast.Turn ? "the cast's round and its effect's duration, at the caster's turn" : "rounds otherwise")}");
        }

        static string Name(EffectKind kind)
        {
            return kind switch
            {
                EffectKind.Boost => "boost",
                EffectKind.Deboost => "deboost",
                _ => "shield",
            };
        }
    }

    private SpellEffect SourceOf(in SpellLevel level, int uid)
    {
        foreach (var effect in _blows.Spells.EffectsOf(level))
        {
            if (effect.Uid == uid)
                return effect;
        }

        foreach (var effect in _blows.Spells.CriticalEffectsOf(level))
        {
            if (effect.Uid == uid)
                return effect;
        }

        return default;
    }

    private void Moves(Cast cast, IReadOnlyList<ForeseenEffect> foreseen)
    {
        foreach (var effect in foreseen.Where(effect => effect.IsImmediate && effect.Action.Kind is EffectKind.Teleport or EffectKind.Swap or EffectKind.Summon))
        {
            var happened = effect.Action.Kind switch
            {
                EffectKind.Teleport => cast.Actions.Any(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Teleport && action.Teleport.TargetId == effect.TargetId),
                EffectKind.Swap => cast.Actions.Any(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Swap),
                _ => cast.Actions.Any(action => action.ActionCase is GameActionFightEvent.ActionOneofCase.Summons && action.Summons.Fighters is { } summons
                    && summons.Summons.Any(summon => summon.Entity?.Monster is { } monster && monster.MonsterId == SourceOf(cast.Level, EffectUidOf(cast.Level, effect.EffectId)).DiceNum)),
            };

            Tally.Count($"teleports, swaps and summons: {effect.Action.Kind}, {(happened ? "as foreseen" : "not announced")}");
        }
    }

    private int EffectUidOf(in SpellLevel level, int effectId)
    {
        foreach (var effect in _blows.Spells.EffectsOf(level))
        {
            if (effect.EffectId == effectId)
                return effect.Uid;
        }

        return 0;
    }

    /// <summary>A cast a fighter chose, the fight as it stood before it, and what the server announced until its sequence closed.</summary>
    private sealed class Starting
    {
        public Starting(long fighterId, int covering, double imagined)
        {
            FighterId = fighterId;
            Covering = covering;
            Imagined = imagined;
        }

        public long FighterId { get; }

        public int Covering { get; }

        public double Imagined { get; }

        public int Struck { get; set; }

        public int Entered { get; set; }

        public int Lost { get; set; }
    }

    private sealed class Cast
    {
        public required Combatant Caster { get; init; }

        public Combatant? OutOfFights { get; init; }

        public required IReadOnlyList<Combatant> Fighters { get; init; }

        public required FightMap Map { get; init; }

        public required SpellLevel Level { get; init; }

        public required int Cell { get; init; }

        public required bool Critical { get; init; }

        public required int Depth { get; init; }

        public required int Round { get; init; }

        public required int Turn { get; init; }

        public required Dictionary<long, (int Life, int MaxLife)> Lives { get; init; }

        public required Dictionary<long, ImmutableArray<Bot.Fights.FightBuff>> Held { get; init; }

        public List<GameActionFightEvent> Actions { get; } = [];

        public List<(int ActionId, FightBuff Buff)> Buffs { get; } = [];

        public int TriggeredCasts { get; set; }
    }
}
