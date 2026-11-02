using System.Collections.Immutable;
using D3B.Data.Spells;

namespace D3B.Engine.Fights;

/// <summary>The marks spells set on cells, glyphs and traps, as the pack's effects make them.</summary>
/// <remarks>
/// <para>
/// An effect of 401, 402, 1091 or 400 sets a mark on the cells of its zone around the cell cast on, those a fighter may stand
/// on, for its duration, which casts the spell its dice name at their grade on the fighters there (client.md). Measured on 4
/// glyphs of monsters in the 101 game connections of 3.7.12.12 (9f4): as a fighter's turn began on one of 401, before any of
/// its turn's own effects, the server struck it with the glyph's spell, cast by its author on the glyph's middle, the fighter
/// alone taking its effects, 3 times on the character, as many on monsters of the author's side, which its mask spared;
/// the glyph went as its author's turn of the round its duration names began, after it struck. A Roublard's bombs' wall
/// struck a monster whose turn began on it the same way, 1 time.
/// </para>
/// <para>Built once per pack and shared, from any thread.</para>
/// </remarks>
public sealed class Glyphs
{
    /// <summary>The effect that sets a trap.</summary>
    public const int Trap = 400;

    /// <summary>The effect that sets a glyph struck as a turn begins.</summary>
    public const int TurnStart = 401;

    /// <summary>The effect that sets a glyph struck as a turn ends.</summary>
    public const int TurnEnd = 402;

    /// <summary>The effect that sets a glyph-aura.</summary>
    public const int Aura = 1091;

    // The rounds a mark of the whole fight lasts, past any fight's end.
    private const int WholeFight = 1_000;

    private readonly SpellCatalog _spells;

    /// <summary>Initializes a new instance of the <see cref="Glyphs"/> class.</summary>
    /// <param name="spells">The pack's spells.</param>
    public Glyphs(SpellCatalog spells)
    {
        ArgumentNullException.ThrowIfNull(spells);

        _spells = spells;
    }

    /// <summary>Gets when the mark an effect sets casts its spell.</summary>
    /// <param name="effectId">The effect's id.</param>
    /// <returns>When, or <see cref="CellMarkKind.Other"/> for an effect that sets no mark the engine foresees.</returns>
    public static CellMarkKind KindOf(int effectId)
    {
        return effectId switch
        {
            TurnStart => CellMarkKind.TurnStart,
            TurnEnd => CellMarkKind.TurnEnd,
            Aura => CellMarkKind.Aura,
            Trap => CellMarkKind.Trap,
            _ => CellMarkKind.Other,
        };
    }

    /// <summary>Tells whether a level sets any mark.</summary>
    /// <param name="level">The level.</param>
    /// <returns>Whether one of its effects does.</returns>
    public bool Sets(in SpellLevel level)
    {
        foreach (var effect in _spells.EffectsOf(level))
        {
            if (KindOf(effect.EffectId) is not CellMarkKind.Other && (effect.Properties & SpellEffectProperties.ForClientOnly) is 0)
                return true;
        }

        return false;
    }

    /// <summary>Finds when a mark the server set casts its spell, from the spell that set it.</summary>
    /// <param name="setterId">The spell that set the mark.</param>
    /// <param name="spellId">The spell the mark casts.</param>
    /// <returns>When, from the first of the setter's effects that sets a mark casting that spell; <see cref="CellMarkKind.Other"/> when none does.</returns>
    public CellMarkKind KindOf(int setterId, int spellId)
    {
        if (!_spells.TryGetSpell(setterId, out var setter))
            return CellMarkKind.Other;

        foreach (var levelId in _spells.LevelIdsOf(setter))
        {
            if (!_spells.TryGetLevel(levelId, out var level))
                continue;

            foreach (var effect in _spells.EffectsOf(level))
            {
                if (effect.DiceNum == spellId && KindOf(effect.EffectId) is not CellMarkKind.Other and var kind)
                    return kind;
            }
        }

        return CellMarkKind.Other;
    }

    /// <summary>Adds the marks a level cast on a cell sets to a list.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight.</param>
    /// <param name="caster">The caster, the marks' author.</param>
    /// <param name="level">The level cast.</param>
    /// <param name="cell">The cell cast on, the marks' middle.</param>
    /// <param name="round">The round it is cast in.</param>
    /// <param name="firstId">The id the first mark takes, the next ones following.</param>
    /// <param name="marks">The list the marks are added to.</param>
    public void Set(FightMap map, IReadOnlyList<Combatant> fighters, Combatant caster, in SpellLevel level, int cell, int round, int firstId, List<CellMark> marks)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(marks);

        if (!map.IsWalkable(cell))
            return;

        var occupied = CastRules.OccupiedBy(fighters);
        var id = firstId;

        foreach (var effect in _spells.EffectsOf(level))
        {
            var kind = KindOf(effect.EffectId);

            // An effect for the client alone draws the mark the server sets with the other effect of the level (client.md).
            if (kind is CellMarkKind.Other || (effect.Properties & SpellEffectProperties.ForClientOnly) is not 0)
                continue;

            var cells = ImmutableArray.CreateBuilder<int>();

            foreach (var covered in ZoneCells.Of(effect.Zone, _spells.CellsOf(effect.Zone), map, occupied, caster.Cell, cell))
            {
                if (map.IsWalkable(covered))
                    cells.Add(covered);
            }

            // Measured (9f4): a glyph of duration 2 set in round 2 went as its author's turn of round 4 began.
            var rounds = effect.Duration < 0 ? WholeFight : Math.Max(1, (int)effect.Duration);

            marks.Add(new CellMark(id++, caster.Id, caster.Team, kind, effect.DiceNum, Math.Max(1, effect.DiceSide), cell, cells.DrainToImmutable(), round + rounds));
        }
    }

    /// <summary>Finds the level a mark casts.</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="level">The level, its highest when the spell has fewer grades.</param>
    /// <returns>Whether the pack holds the spell.</returns>
    public bool TryGetLevel(CellMark mark, out SpellLevel level)
    {
        ArgumentNullException.ThrowIfNull(mark);

        level = default;

        return _spells.TryGetSpell(mark.SpellId, out var spell) && spell.LevelCount > 0 && _spells.TryGetLevel(mark.SpellId, Math.Clamp(mark.Grade, 1, (int)spell.LevelCount), out level);
    }
}
