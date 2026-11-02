using System.Collections.Immutable;
using System.Diagnostics;
using D3B.Data;
using D3B.Data.Effects;
using D3B.Data.Maps;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Maps;

namespace D3B.Engine.Tactics;

/// <summary>The fight's AI: where to stand as the fight starts, and each turn the best sequence of walks and casts, searched on the rules and the effects.</summary>
/// <remarks>
/// <para>
/// Each turn, a beam search (D201): from the fight as it stands, every cast the fighter playing may make, from where it
/// stands or from a cell it walks to first, on the cells where the level's effects reach fighters, each imagined
/// (<see cref="Imagination"/>) and judged once the turn ends on the best cell it then walks to (<see cref="FightJudge"/>);
/// the best <see cref="TurnSearchLimits.Width"/> sequences go on to their next cast, up to <see cref="TurnSearchLimits.Depth"/>
/// casts or the search's time. The plan is the best sequence judged, its last walk among its actions; empty when ending
/// the turn now is best. The bot sends its first action, then searches again from what the server answered.
/// </para>
/// <para>
/// The best sequences found, their end walk among them, are then weighed once more against the enemies' reply (D203): the
/// fighter playing's summons that play strike first, right after it as the server orders them (D270), then each living
/// enemy plays its next turn in the imagined fight, as greedily as the search plays, walking where its spells reach the
/// fighter playing's side through the rules, line of sight and tackle included, and casting what lowers that side's worth
/// most, until nothing does. The plan is the sequence whose fight is best once they replied, the threat's guess left
/// for the search's own ranking. Each fighter's turn in the reply begins and ends with the marks on its cell striking it,
/// the fighter playing's own next turn beginning so too (9f4).
/// </para>
/// <para>Built once per pack and shared, from any thread.</para>
/// </remarks>
public sealed class FightTactics
{
    private const double Better = 0.01;
    private const int Finalists = 16;
    private const int RepliesAnEnemy = 3;
    private const int ReplyCells = 12;
    private const int RootEnds = 3;
    private const int NextTurnCasts = 2;
    private const int NextTurnCells = 12;

    // Summons the judge knows before the search brings them take ids no server and no search gives.
    private const long FirstJudgedSummon = -200_000;

    private readonly SpellCatalog _spells;
    private readonly SpellStateCatalog _states;
    private readonly MonsterCatalog _monsters;
    private readonly EffectActions _actions;

    /// <summary>Gets the pack's spells and the weapons' blows, which the AI casts as spells' levels (D220).</summary>
    public WeaponBlows Blows { get; }

    /// <summary>Gets the rules of a cast.</summary>
    public CastRules Rules { get; }

    /// <summary>Gets how fighters walk.</summary>
    public FightMoves Moves { get; }

    /// <summary>Gets what casts are foreseen to do.</summary>
    public SpellEffects Effects { get; }

    /// <summary>Gets the Rogue's bombs, their explosions and walls.</summary>
    public Bombs Bombs { get; }

    /// <summary>Gets the marks spells set on cells, glyphs and traps.</summary>
    public Glyphs Glyphs { get; }

    /// <summary>Gets what actions do to an imagined fight.</summary>
    public Imagination Imagination { get; }

    /// <summary>Gets the levels each monster casts at its grade, a summon's the search brings among them.</summary>
    public MonsterSpells MonsterSpells { get; }

    /// <summary>Initializes a new instance of the <see cref="FightTactics"/> class.</summary>
    /// <param name="tables">The pack's game tables.</param>
    public FightTactics(GameTables tables)
        : this(new WeaponBlows((tables ?? throw new ArgumentNullException(nameof(tables))).Spells, tables.Weapons, tables.Effects), tables.States, tables.Effects, tables.Monsters)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FightTactics"/> class, from the pack's catalogs, with no weapon's blow.</summary>
    /// <param name="spells">The spells.</param>
    /// <param name="states">The states.</param>
    /// <param name="effects">The effects.</param>
    /// <param name="monsters">The monsters, for summons.</param>
    public FightTactics(SpellCatalog spells, SpellStateCatalog states, EffectCatalog effects, MonsterCatalog monsters)
        : this(new WeaponBlows(spells), states, effects, monsters)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FightTactics"/> class, from the pack's catalogs and the weapons' blows.</summary>
    /// <param name="blows">The spells and the weapons' blows.</param>
    /// <param name="states">The states.</param>
    /// <param name="effects">The effects.</param>
    /// <param name="monsters">The monsters, for summons.</param>
    public FightTactics(WeaponBlows blows, SpellStateCatalog states, EffectCatalog effects, MonsterCatalog monsters)
    {
        ArgumentNullException.ThrowIfNull(blows);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(monsters);

        Blows = blows;
        _spells = blows.Spells;
        _states = states;
        _monsters = monsters;
        _actions = new EffectActions(effects);
        Rules = new CastRules(_spells, states);
        Moves = new FightMoves(states);
        Effects = new SpellEffects(blows, Rules, _actions);
        Bombs = new Bombs(_spells, Rules);
        Glyphs = new Glyphs(_spells);
        Imagination = new Imagination(_spells, monsters, _actions, Effects, Bombs, Glyphs, Rules);
        MonsterSpells = new MonsterSpells(_spells, monsters);
    }

    private static long Signature(ImaginedFight fight)
    {
        var hash = new HashCode();

        foreach (var fighter in fight.Fighters)
        {
            hash.Add(fighter.Cell);
            hash.Add(fighter.Life);
            hash.Add(fighter.ActionPoints);
            hash.Add(fighter.MovementPoints);
        }

        return hash.ToHashCode();
    }

    /// <summary>Gets the best of a search's ranked sequences, and beside them the best of each set of states the fighter playing holds that they leave out.</summary>
    /// <typeparam name="T">A sequence.</typeparam>
    /// <param name="ranked">The sequences, the best first.</param>
    /// <param name="fighterOf">The fighter playing, as a sequence leaves it.</param>
    /// <param name="most">How many of the best are kept.</param>
    /// <returns>The <paramref name="most"/> best, then the best sequence of each other set of states, in their rank.</returns>
    internal static List<T> WithEachStates<T>(IReadOnlyList<T> ranked, Func<T, Combatant> fighterOf, int most)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        ArgumentNullException.ThrowIfNull(fighterOf);

        // Measured on a Zobal's 36 turns (D255): a mask deals nothing as it is put on, and the spells it opens count only a
        // cast later, so that the best sequences crowded out every one starting with it and the AI put it on last, for the
        // next turn. Kept beside them, the plans were judged 29,149 against 26,260, and a search six times as wide 30,981.
        var kept = ranked.Take(most).ToList();
        var states = kept.Select(item => StatesOf(fighterOf(item))).ToHashSet();

        foreach (var item in ranked.Skip(most))
        {
            if (states.Add(StatesOf(fighterOf(item))))
                kept.Add(item);
        }

        return kept;
    }

    private static long StatesOf(Combatant fighter)
    {
        // Whatever order the states came in: a sum of each state's mix, and how many.
        var sum = 0L;

        foreach (var state in fighter.States)
            sum = unchecked(sum + ((long)state * -7046029254386353131L));

        return unchecked(sum + fighter.States.Length);
    }

    private static int Nearest(ImaginedFight fight, int team, int cell)
    {
        var nearest = int.MaxValue;

        foreach (var fighter in fight.Fighters)
        {
            if (fighter.IsAlive && fighter.Team != team && fighter.Cell is not Combatant.NoCell)
                nearest = Math.Min(nearest, MapGeometry.Distance(cell, fighter.Cell));
        }

        return nearest;
    }

    /// <summary>Gets a judge of a fight as it stands, which a search and a placement share.</summary>
    /// <param name="fight">The fight.</param>
    /// <param name="knowledge">What the AI knows of the fighters.</param>
    /// <param name="weights">The weights.</param>
    /// <returns>The judge.</returns>
    public FightJudge JudgeOf(ImaginedFight fight, FightKnowledge knowledge, TacticWeights weights)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(knowledge);

        return new FightJudge(fight, knowledge, weights, Blows, _actions, Rules, Moves, _states, SummonsOf(fight, knowledge), Bombs, Effects, Imagination);
    }

    /// <summary>Gets the levels a fighter casts: as the knowledge lists them, else, for a summon the search brought, its monster's at its grade.</summary>
    /// <param name="fight">The fight.</param>
    /// <param name="fighter">The fighter.</param>
    /// <param name="knowledge">What the AI knows of the fighters.</param>
    /// <returns>The levels; none when not known.</returns>
    public ImmutableArray<SpellLevel> SpellsOf(ImaginedFight fight, Combatant fighter, FightKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(fighter);
        ArgumentNullException.ThrowIfNull(knowledge);

        if (knowledge.Spells.TryGetValue(fighter.Id, out var known))
            return known;

        return fighter is { MonsterId: { } monster, Grade: { } grade } ? MonsterSpells.Of(monster, grade) : [];
    }

    private List<(Combatant Summon, ImmutableArray<SpellLevel> Levels)> SummonsOf(ImaginedFight fight, FightKnowledge knowledge)
    {
        // Each summon the fighter playing's levels may bring, once, as it would come now.
        var summons = new List<(Combatant, ImmutableArray<SpellLevel>)>();
        var seen = new HashSet<(int, int)>();
        var actor = fight.Actor;

        foreach (var level in knowledge.SpellsOf(actor.Id))
        {
            foreach (var effect in _spells.EffectsOf(level))
            {
                if (_actions.Of(effect.EffectId).Kind is not EffectKind.Summon || !_monsters.TryGetMonster(effect.DiceNum, out var monster))
                    continue;

                var gradeOf = Math.Clamp(effect.DiceSide, 1, Math.Max(1, (int)monster.GradeCount));

                if (!seen.Add((monster.Id, gradeOf)) || !_monsters.TryGetGrade(monster.Id, gradeOf, out var grade))
                    continue;

                summons.Add((Summons.Of(actor, monster, grade, FirstJudgedSummon - summons.Count, Combatant.NoCell), MonsterSpells.Of(monster.Id, gradeOf)));
            }
        }

        return summons;
    }

    /// <summary>Gets how the fight looks for the fighter playing's side as it starts.</summary>
    /// <param name="fight">The fight.</param>
    /// <param name="knowledge">What the AI knows of the fighters.</param>
    /// <returns>The odds.</returns>
    public FightOdds Odds(ImaginedFight fight, FightKnowledge knowledge)
    {
        return JudgeOf(fight, knowledge, TacticWeights.Default).Odds(fight, knowledge);
    }

    /// <summary>Gets how the fight looks for the fighter playing's side over its course, the enemies dying one after another (<see cref="FightJudge.OddsOverTheFight"/>, D249).</summary>
    /// <param name="fight">The fight.</param>
    /// <param name="knowledge">What the AI knows of the fighters.</param>
    /// <returns>The odds.</returns>
    public FightOdds OddsOverTheFight(ImaginedFight fight, FightKnowledge knowledge)
    {
        return JudgeOf(fight, knowledge, TacticWeights.Default).OddsOverTheFight(fight, knowledge);
    }

    /// <summary>Chooses where the fighter playing stands as the fight starts, among its team's placement cells.</summary>
    /// <param name="fight">The fight as placed now.</param>
    /// <param name="cells">The team's placement cells.</param>
    /// <param name="knowledge">What the AI knows of the fighters.</param>
    /// <param name="weights">The weights.</param>
    /// <returns>The cell judged best, the fighter's own on a tie; its own when no other is free.</returns>
    /// <remarks>Each free cell judged as a turn's end is (<see cref="FightJudge"/>): the enemies' threat there against the damage it may deal from there.</remarks>
    public int Place(ImaginedFight fight, IEnumerable<int> cells, FightKnowledge knowledge, TacticWeights weights)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(cells);

        var judge = JudgeOf(fight, knowledge, weights);
        var actor = fight.Actor;
        var best = actor.Cell;
        var bestScore = judge.Judge(fight);

        foreach (var cell in cells)
        {
            if (cell == actor.Cell || !fight.Map.IsWalkable(cell) || fight.On(cell) is not null)
                continue;

            var score = judge.Judge(fight.With(actor with { Cell = cell }));

            if (score > bestScore + Better)
            {
                best = cell;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>Searches the best sequence of walks and casts for the rest of the fighter playing's turn.</summary>
    /// <param name="fight">The fight as it stands.</param>
    /// <param name="knowledge">What the AI knows of the fighters.</param>
    /// <param name="weights">The weights.</param>
    /// <param name="limits">How far the search goes.</param>
    /// <param name="refused">Casts the server refused this turn, by spell and cell, never weighed again.</param>
    /// <returns>The plan.</returns>
    public TurnPlan Plan(ImaginedFight fight, FightKnowledge knowledge, TacticWeights weights, TurnSearchLimits limits, IReadOnlySet<(int SpellId, int Cell)>? refused = null)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(limits);

        var clock = Stopwatch.StartNew();
        var judge = JudgeOf(fight, knowledge, weights);
        var weighed = 1;
        var root = new Node(fight, [], judge.Judge(fight));
        var finalists = new List<Finalist>();
        List<Node> frontier = [root];

        // Ending the turn now, and the best walks: ended so, the turn is weighed against the enemies' reply too.
        Ends(root, judge, finalists, RootEnds, ref weighed);

        for (var depth = 0; depth < limits.Depth && frontier.Count > 0 && clock.Elapsed < limits.Budget; depth++)
        {
            var children = new List<Node>();

            foreach (var node in frontier)
            {
                foreach (var (from, level, cell) in Casts(node.Fight, knowledge, limits, refused))
                {
                    if (clock.Elapsed >= limits.Budget)
                        break;

                    var walked = from is { } step ? Imagination.Walked(node.Fight, step.Cell, step.Left) : node.Fight;
                    var cast = Imagination.Cast(walked, level, cell);
                    ImmutableArray<TacticAction> actions = from is { } walk
                        ? [.. node.Actions, TacticAction.Walk(walk.Cell), TacticAction.Cast(level.SpellId, level.Id, cell)]
                        : [.. node.Actions, TacticAction.Cast(level.SpellId, level.Id, cell)];

                    children.Add(new Node(cast, actions, judge.Judge(cast)));
                    weighed++;
                }
            }

            var seen = new HashSet<long>();
            var kept = new List<(Node Node, double Score)>();

            foreach (var child in WithEachStates(children.OrderByDescending(child => child.Score).ToList(), child => child.Fight.Actor, limits.Width * 2))
            {
                if (seen.Add(Signature(child.Fight)))
                    kept.Add((child, Ends(child, judge, finalists, 1, ref weighed)));
            }

            frontier = [.. WithEachStates(kept.OrderByDescending(pair => pair.Score).ToList(), pair => pair.Node.Fight.Actor, limits.Width).Select(pair => pair.Node)];
        }

        // The finalists in the order the search ranks them, each once the enemies replied and the fighter playing played its
        // next turn from there (D210); ending the turn now wins a tie.
        var standing = finalists[0];
        var best = standing;
        var bestScore = double.NegativeInfinity;
        var standingScore = 0.0;
        var later = knowledge with { Cooldowns = knowledge.Cooldowns.Where(pair => pair.Value > 1).ToImmutableDictionary(pair => pair.Key, pair => pair.Value - 1) };

        foreach (var finalist in finalists.OrderByDescending(finalist => finalist.Score).Take(Finalists).Append(standing).Distinct())
        {
            var replied = Replies(finalist.End, knowledge, judge);
            var score = judge.JudgeReplied(replied, NextTurn(replied, later, judge, limits).Gain);

            weighed++;

            if (finalist == standing)
                standingScore = score;

            if (score > bestScore + Better || (finalist == standing && score >= bestScore - Better))
            {
                best = finalist;
                bestScore = score;
            }
        }

        return new TurnPlan(best.Actions, bestScore, standingScore, weighed, clock.Elapsed);
    }

    /// <summary>Gets the fight once the fighter playing's summons, then the enemies, played their next turns after its, as greedily as the search imagines them.</summary>
    /// <param name="fight">The fight as the fighter playing's turn ends.</param>
    /// <param name="knowledge">What the AI knows of the fighters' spells.</param>
    /// <param name="judge">The judge, whose worth of the fighter playing's side each enemy lowers as much as it can.</param>
    /// <returns>The fight once each living enemy played, its fighter playing the same.</returns>
    public ImaginedFight Replies(ImaginedFight fight, FightKnowledge knowledge, FightJudge judge)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentNullException.ThrowIfNull(judge);

        var team = fight.Actor.Team;
        var replied = Imagination.TurnEnded(fight, fight.ActorId);

        // Measured (9f2): a summon plays right after its summoner, the latest summoned first, so before the enemies.
        foreach (var summon in fight.Fighters.Where(fighter => fighter.SummonerId == fight.ActorId && (fighter.MonsterProperties & MonsterProperties.CanPlay) is not 0).OrderBy(fighter => fighter.Id))
        {
            if (summon.IsAlive && summon.Cell is not Combatant.NoCell)
                replied = Played(replied, summon.Id, fight.Round, view => Reply(view, summon.Id, SpellsOf(view, summon, knowledge), other => -judge.Standing(other), strikesOnly: false));
        }

        // An enemy's turn in the reply is taken as the next round's for its marks' time, as most enemies play before the
        // fighter playing's next turn.
        foreach (var enemy in fight.Fighters)
        {
            if (enemy.IsAlive && enemy.Team != team && enemy.Cell is not Combatant.NoCell)
                replied = Played(replied, enemy.Id, fight.Round + 1, view => Reply(view, enemy.Id, knowledge.SpellsOf(enemy.Id), judge.AlliesWorth, strikesOnly: true));
        }

        // The fighter playing's next turn begins with the marks on its cell striking it, before it plays (9f4).
        replied = Imagination.TurnStarted(replied with { ActorId = fight.ActorId }, fight.ActorId, fight.Round + 1);

        return replied with { ActorId = fight.ActorId, History = fight.History };
    }

    /// <summary>Gets the fighter playing's next turn once the enemies replied, as greedily as their replies: what it adds to its side's worth.</summary>
    /// <param name="replied">The fight once the enemies replied.</param>
    /// <param name="knowledge">What the AI knows of the fighters, the cooldowns as they will be next turn.</param>
    /// <param name="judge">The judge, whose <see cref="FightJudge.Standing"/> the turn raises as much as it can.</param>
    /// <param name="limits">How far the search goes, for the cells it weighs.</param>
    /// <returns>The fight once the turn is played, and what it added; none when nothing raises it.</returns>
    /// <remarks>
    /// Each cast is the one, from where the fighter stands or a cell it walks to first, that raises the worth most, through the
    /// rules, the zones and the effects, as many fighters as it reaches; <see cref="NextTurnCasts"/> at most (D210).
    /// </remarks>
    public (ImaginedFight Fight, double Gain) NextTurn(ImaginedFight replied, FightKnowledge knowledge, FightJudge judge, TurnSearchLimits limits)
    {
        ArgumentNullException.ThrowIfNull(replied);
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentNullException.ThrowIfNull(judge);
        ArgumentNullException.ThrowIfNull(limits);

        var fight = judge.NextTurnStart(replied);
        var start = judge.Standing(fight);
        var worth = start;

        if (fight.Find(fight.ActorId) is not { IsAlive: true })
            return (fight, 0);

        for (var cast = 0; cast < NextTurnCasts; cast++)
        {
            ImaginedFight? found = null;
            var bestWorth = worth + Better;

            foreach (var (from, level, cell) in Casts(fight, knowledge, limits with { CellsPerLevel = Math.Min(limits.CellsPerLevel, NextTurnCells), WalksPerTarget = 1 }, null))
            {
                var walked = from is { } step ? Imagination.Walked(fight, step.Cell, step.Left) : fight;
                var after = Imagination.Cast(walked, level, cell);
                var raised = judge.Standing(after);

                if (raised > bestWorth)
                {
                    found = after;
                    bestWorth = raised;
                }
            }

            if (found is null)
                break;

            fight = found;
            worth = bestWorth;
        }

        return (fight, worth - start);
    }

    /// <summary>Gets the cells a walk of the fighter playing steps on to a cell, as <see cref="FightMoves.Path"/> gives them.</summary>
    /// <param name="fight">The fight.</param>
    /// <param name="cell">The cell walked to.</param>
    /// <returns>The path, its first cell where the fighter stands; empty when it does not reach the cell.</returns>
    public ImmutableArray<int> PathTo(ImaginedFight fight, int cell)
    {
        ArgumentNullException.ThrowIfNull(fight);

        return Moves.Path(fight.Map, fight.Fighters, fight.Actor, cell);
    }

    private double Ends(Node node, FightJudge judge, List<Finalist> finalists, int most, ref int weighed)
    {
        // Ending where the fighter stands comes first, so that a tie keeps it there and no walk is sent.
        var actor = node.Fight.Actor;
        List<Finalist> ends = [new Finalist(node.Actions, node.Fight, node.Score)];

        if (actor.MovementPoints > 0 && actor.Cell is not Combatant.NoCell)
        {
            foreach (var (cell, left) in Moves.Reachable(node.Fight.Map, node.Fight.Fighters, actor))
            {
                if (cell == actor.Cell)
                    continue;

                var walked = Imagination.Walked(node.Fight, cell, left);
                var score = judge.Judge(walked);

                weighed++;

                if (score > node.Score + Better)
                    ends.Add(new Finalist([.. node.Actions, TacticAction.Walk(cell)], walked, score));
            }
        }

        var kept = ends.Take(1).Concat(ends.Skip(1).OrderByDescending(end => end.Score).Take(most)).ToList();

        finalists.AddRange(kept);

        return kept.Max(end => end.Score);
    }

    private ImaginedFight Played(ImaginedFight fight, long fighterId, int round, Func<ImaginedFight, ImaginedFight> turn)
    {
        // A turn begins and ends with the marks on the fighter's cell striking it, which may kill it before it plays.
        var started = Imagination.TurnStarted(fight, fighterId, round);

        if (started.Find(fighterId) is not { IsAlive: true })
            return started;

        return Imagination.TurnEnded(turn(started), fighterId);
    }

    private ImaginedFight Reply(ImaginedFight fight, long fighterId, ImmutableArray<SpellLevel> levels, Func<ImaginedFight, double> worth, bool strikesOnly)
    {
        // An enemy lowers the worth of the fighter playing's side as much as it can, striking that side; one of that side's
        // summons lowers the worth's opposite, on any fighter, an ally it heals or boosts as an enemy it strikes.
        var view = fight with { ActorId = fighterId, History = CastHistory.Empty };

        if (levels.IsEmpty)
            return view;

        for (var cast = 0; cast < RepliesAnEnemy; cast++)
        {
            var enemy = view.Actor;

            if (!enemy.IsAlive || enemy.Cell is Combatant.NoCell)
                break;

            var mobile = !enemy.IsStatic && (enemy.MonsterId is null || (enemy.MonsterProperties & MonsterProperties.CanMove) is not 0);
            var reachable = mobile && enemy.MovementPoints > 0 ? Moves.Reachable(view.Map, view.Fighters, enemy) : new Dictionary<int, (int, int)> { [enemy.Cell] = (enemy.MovementPoints, enemy.ActionPoints) };
            var foes = view.Fighters.Where(fighter => fighter.IsAlive && fighter.Team != enemy.Team && fighter.Cell is not Combatant.NoCell).Select(fighter => fighter.Cell).ToList();
            var targets = strikesOnly ? foes : [.. view.Fighters.Where(fighter => fighter.IsAlive && fighter.Cell is not Combatant.NoCell).Select(fighter => fighter.Cell)];

            if (foes.Count is 0)
                break;

            // The cells nearest its foes, where its blows land, those keeping the most MP first.
            var cells = reachable.OrderBy(pair => foes.Min(foe => MapGeometry.Distance(pair.Key, foe))).ThenByDescending(pair => pair.Value.MovementPoints).Take(ReplyCells).ToList();
            ImaginedFight? best = null;
            var bestWorth = worth(view) - Better;

            foreach (var level in levels)
            {
                foreach (var (from, left) in cells)
                {
                    var moved = enemy with { Cell = from, MovementPoints = left.MovementPoints, ActionPoints = left.ActionPoints };

                    if (moved.ActionPoints < CastRules.Modified(level, moved).ApCost)
                        continue;

                    var there = view.With(moved);

                    foreach (var target in targets)
                    {
                        if (Rules.Check(there.Map, there.Fighters, moved, level, target, there.History, there.Round) is not CastRefusal.None)
                            continue;

                        var after = Imagination.Cast(there, level, target);
                        var lowered = worth(after);

                        if (lowered < bestWorth)
                        {
                            best = after;
                            bestWorth = lowered;
                        }
                    }
                }
            }

            if (best is null)
                break;

            view = best;
        }

        return view;
    }

    private IEnumerable<((int Cell, (int MovementPoints, int ActionPoints) Left)? From, SpellLevel Level, int Cell)> Casts(ImaginedFight fight, FightKnowledge knowledge, TurnSearchLimits limits, IReadOnlySet<(int SpellId, int Cell)>? refused)
    {
        var actor = fight.Actor;

        if (!actor.IsAlive || actor.Cell is Combatant.NoCell)
            yield break;

        var reachable = actor.MovementPoints > 0 ? Moves.Reachable(fight.Map, fight.Fighters, actor) : new Dictionary<int, (int, int)> { [actor.Cell] = (actor.MovementPoints, actor.ActionPoints) };
        var walks = reachable.Where(pair => pair.Key != actor.Cell).OrderByDescending(pair => pair.Value.MovementPoints).ThenBy(pair => MapGeometry.Distance(actor.Cell, pair.Key)).ToList();

        foreach (var level in knowledge.SpellsOf(actor.Id))
        {
            if (knowledge.Cooldowns.GetValueOrDefault(level.SpellId) > 0 || actor.ActionPoints < CastRules.Modified(level, actor).ApCost)
                continue;

            foreach (var cell in TargetCells(fight, level, limits.CellsPerLevel))
            {
                if (refused?.Contains((level.SpellId, cell)) is true)
                    continue;

                if (Rules.Check(fight.Map, fight.Fighters, actor, level, cell, fight.History, fight.Round) is CastRefusal.None)
                {
                    yield return (null, level, cell);

                    continue;
                }

                var found = 0;

                foreach (var (from, left) in walks)
                {
                    if (found >= limits.WalksPerTarget)
                        break;

                    var moved = actor with { Cell = from, MovementPoints = left.MovementPoints, ActionPoints = left.ActionPoints };
                    var modified = CastRules.Modified(level, moved);

                    if (!SpellRange.Reaches(modified, moved.RangeBonus, from, cell))
                        continue;

                    var there = fight.With(moved);

                    if (Rules.Check(there.Map, there.Fighters, moved, level, cell, there.History, there.Round) is not CastRefusal.None)
                        continue;

                    found++;

                    yield return ((from, left), level, cell);
                }
            }
        }
    }

    private List<int> TargetCells(ImaginedFight fight, in SpellLevel level, int most)
    {
        var actor = fight.Actor;
        var radius = 0;
        var free = (level.Properties & SpellLevelProperties.NeedFreeCell) is not 0;
        var bomb = false;

        foreach (var effect in _spells.EffectsOf(level))
        {
            var kind = _actions.Of(effect.EffectId).Kind;

            free |= kind is EffectKind.Summon or EffectKind.Teleport;
            bomb |= kind is EffectKind.Summon && _spells.TryGetBomb(effect.DiceNum, out _);
            radius = Math.Max(radius, effect.Zone.Shape switch
            {
                SpellZoneShape.Point or SpellZoneShape.None or SpellZoneShape.Custom => 0,
                SpellZoneShape.WholeMap or SpellZoneShape.WholeMapWithTheDead => 0,
                _ => Math.Min((int)effect.Zone.Size, 6),
            });
        }

        var cells = new HashSet<int>();

        foreach (var fighter in fight.Fighters)
        {
            if (!fighter.IsAlive || fighter.Cell is Combatant.NoCell)
                continue;

            // A bomb cast on an enemy casts the bomb's instant spell there (9f3).
            if (bomb && fighter.Team != actor.Team)
                cells.Add(fighter.Cell);

            // A free cell is sought beside a fighter, an enemy to strike or come near, an ally to stand by.
            var around = free ? Math.Max(1, radius) : radius;

            for (var cell = 0; cell < MapData.CellCount; cell++)
            {
                if (fight.Map.IsWalkable(cell) && MapGeometry.Distance(cell, fighter.Cell) <= around && (!free || fight.On(cell) is null))
                    cells.Add(cell);
            }
        }

        return [.. cells
            .OrderBy(cell => Nearest(fight, actor.Team, cell))
            .ThenBy(cell => MapGeometry.Distance(actor.Cell, cell))
            .Take(most)];
    }


    private sealed record Node(ImaginedFight Fight, ImmutableArray<TacticAction> Actions, double Score);

    private sealed record Finalist(ImmutableArray<TacticAction> Actions, ImaginedFight End, double Score);
}
