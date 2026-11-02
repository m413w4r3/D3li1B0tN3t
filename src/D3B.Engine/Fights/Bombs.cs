using D3B.Data.Spells;
using D3B.Engine.Criteria;
using D3B.Engine.Maps;

namespace D3B.Engine.Fights;

/// <summary>A Rogue's bombs, as the pack makes them: which bombs an explosion sets off, their combo, and the walls between them.</summary>
/// <remarks>
/// <para>
/// Measured on the 5 fights of a Roublard of level 45 in the 100 game connections of 3.7.12.12, 17 bombs and 12 explosions
/// (9f3): effect 1009 sets a bomb off, which casts its explosion spell at its grade, then the bombs its chain reaction
/// spell's 1009 reaches from its cell, those reached in turn after, depth first: a circle of 2 for any of its summoner's
/// bombs, a cross of 7 for those of its kind. Each bomb's combo is what its buffs of effect 1027 give, 20 a level past the
/// first; an explosion deals its damage raised by the combos of every bomb the chain sets off, summed
/// (<see cref="Effects.FightCharacteristic.BombComboBonus"/>), as the references read it (D3). Of the 6 Détonateurs, the
/// bombs set off as derived but once, when the fight ended with its last enemy before the third bomb.
/// </para>
/// <para>
/// A wall stands on every cell between two bombs of a kind and a summoner in a line, as near as the wall's hops allow, no
/// other bomb of that kind between them: the 12 wall cells the server marked (its action 401) and the 11 it took back (310)
/// as derived, 3 of each a wall it built again whole as a bomb out of it exploded. A fighter a wall comes onto, or that a
/// push leaves on one, takes its spell, cast by the bombs' summoner and raised by the bombs' combos halved: the server's 5
/// (its action 306), within a tenth or killed as derived; the half is measured on one hit of two bombs of unlike combos.
/// </para>
/// <para>Built once per pack and shared, from any thread.</para>
/// </remarks>
public sealed class Bombs
{
    /// <summary>The effect that sets a bomb off.</summary>
    public const int Detonation = 1009;

    /// <summary>The effect that gives a bomb its combo.</summary>
    public const int Combo = 1027;

    [ThreadStatic]
    private static List<(Combatant Fighter, CriterionAnswer Selected)>? _reached;

    private readonly SpellCatalog _spells;
    private readonly CastRules _rules;

    /// <summary>Initializes a new instance of the <see cref="Bombs"/> class.</summary>
    /// <param name="spells">The pack's spells, with its bombs and walls.</param>
    /// <param name="rules">The rules of a cast, which find the bombs a chain reaction reaches.</param>
    public Bombs(SpellCatalog spells, CastRules rules)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(rules);

        _spells = spells;
        _rules = rules;
    }

    /// <summary>Gets a bomb's combo: what its buffs of effect 1027 give, in percent.</summary>
    /// <param name="bomb">The bomb.</param>
    /// <returns>The combo, 0 for a bomb that has none or a fighter that is no bomb.</returns>
    public static int ComboOf(Combatant bomb)
    {
        ArgumentNullException.ThrowIfNull(bomb);

        var combo = 0;

        foreach (var held in bomb.Buffs)
        {
            if (held.EffectId is Combo)
                combo += held.Value;
        }

        return combo;
    }

    private static void Between(FightMap map, IReadOnlyList<Combatant> fighters, Combatant bomb, Combatant partner, in SpellBombWall wall, long summoner, List<BombWallCell> walls)
    {
        var from = MapGeometry.PointOf(bomb.Cell);
        var to = MapGeometry.PointOf(partner.Cell);

        if (from.X != to.X && from.Y != to.Y)
            return;

        var hops = Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);

        if (hops < wall.MinHop || hops > wall.MaxHop)
            return;

        var (x, y) = (Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
        var start = walls.Count;

        for (var step = 1; step < hops; step++)
        {
            if (!MapGeometry.TryGetCell(new MapPoint(from.X + (x * step), from.Y + (y * step)), out var cell) || !map.IsWalkable(cell) || HoldsBombOf(fighters, cell, bomb.MonsterId))
            {
                walls.RemoveRange(start, walls.Count - start);

                return;
            }

            if (!walls.Exists(known => known.Cell == cell))
                walls.Add(new BombWallCell(cell, summoner, wall.SpellId, bomb.Grade ?? 1, (ComboOf(bomb) + ComboOf(partner)) / 2));
        }
    }

    private static bool HoldsBombOf(IReadOnlyList<Combatant> fighters, int cell, int? monster)
    {
        for (var index = 0; index < fighters.Count; index++)
        {
            if (fighters[index].IsAlive && fighters[index].Cell == cell && fighters[index].MonsterId == monster)
                return true;
        }

        return false;
    }

    /// <summary>Tells whether a fighter is a bomb, and which.</summary>
    /// <param name="fighter">The fighter.</param>
    /// <param name="bomb">The bomb, when it is one.</param>
    /// <returns>Whether it is.</returns>
    public bool IsBomb(Combatant fighter, out SpellBomb bomb)
    {
        ArgumentNullException.ThrowIfNull(fighter);

        bomb = default;

        return fighter.MonsterId is { } monster && _spells.TryGetBomb(monster, out bomb);
    }

    /// <summary>Tells whether any living fighter is a bomb.</summary>
    /// <param name="fighters">The fighters.</param>
    /// <returns>Whether one is.</returns>
    public bool Any(IReadOnlyList<Combatant> fighters)
    {
        ArgumentNullException.ThrowIfNull(fighters);

        for (var index = 0; index < fighters.Count; index++)
        {
            if (fighters[index].IsAlive && fighters[index].IsSummon && IsBomb(fighters[index], out _))
                return true;
        }

        return false;
    }

    /// <summary>Adds the bombs a bomb set off sets off with it to a list, in the order they explode: itself first, then depth first.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight.</param>
    /// <param name="first">The index of the bomb set off, among the fighters.</param>
    /// <param name="chain">The list the bombs' indices are added to.</param>
    public void Chain(FightMap map, IReadOnlyList<Combatant> fighters, int first, List<int> chain)
    {
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(chain);

        if (first < 0 || first >= fighters.Count || !fighters[first].IsAlive || !IsBomb(fighters[first], out _) || chain.Contains(first))
            return;

        chain.Add(first);

        var bomb = fighters[first];

        IsBomb(bomb, out var kind);

        if (!TryGetLevel(kind.ChainReactionSpellId, bomb.Grade ?? 1, out var level))
            return;

        var occupied = CastRules.OccupiedBy(fighters);
        var reached = _reached ??= [];
        var next = new List<long>();

        foreach (var effect in _spells.EffectsOf(level))
        {
            if (effect.EffectId is not Detonation)
                continue;

            reached.Clear();
            _rules.Reached(map, fighters, occupied, bomb, effect, bomb.Cell, reached);

            foreach (var (fighter, selected) in reached)
            {
                if (selected is CriterionAnswer.True && fighter.Id != bomb.Id && fighter.IsAlive && IsBomb(fighter, out _) && !next.Contains(fighter.Id))
                    next.Add(fighter.Id);
            }
        }

        reached.Clear();

        foreach (var id in next)
        {
            for (var index = 0; index < fighters.Count; index++)
            {
                if (fighters[index].Id == id)
                    Chain(map, fighters, index, chain);
            }
        }
    }

    /// <summary>Adds the cells of the walls between the living bombs to a list.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight.</param>
    /// <param name="walls">The list the walls' cells are added to, each once.</param>
    public void Walls(FightMap map, IReadOnlyList<Combatant> fighters, List<BombWallCell> walls)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(walls);

        for (var one = 0; one < fighters.Count; one++)
        {
            var bomb = fighters[one];

            if (!bomb.IsAlive || bomb.Cell is Combatant.NoCell || bomb.SummonerId is not { } summoner || !IsBomb(bomb, out var kind)
                || !_spells.TryGetBombWall(kind.WallId, out var wall) || !wall.Linear)
            {
                continue;
            }

            for (var other = one + 1; other < fighters.Count; other++)
            {
                var partner = fighters[other];

                if (partner.IsAlive && partner.Cell is not Combatant.NoCell && partner.MonsterId == bomb.MonsterId && partner.SummonerId == summoner)
                    Between(map, fighters, bomb, partner, wall, summoner, walls);
            }
        }
    }

    /// <summary>Finds the level of a bomb's spell at a grade, its highest when the spell has fewer.</summary>
    /// <param name="spellId">The spell, an explosion, a chain reaction, an instant spell or a wall's.</param>
    /// <param name="grade">The bomb's grade.</param>
    /// <param name="level">The level, when found.</param>
    /// <returns>Whether the pack holds the spell.</returns>
    public bool TryGetLevel(int spellId, int grade, out SpellLevel level)
    {
        level = default;

        return _spells.TryGetSpell(spellId, out var spell) && spell.LevelCount > 0 && _spells.TryGetLevel(spellId, Math.Clamp(grade, 1, (int)spell.LevelCount), out level);
    }
}
