using D3B.Data.Spells;
using D3B.Engine.Criteria;
using D3B.Engine.Fights;
using D3B.Engine.Maps;

namespace D3B.Engine.Effects;

/// <summary>What a cast is foreseen to do: each of its level's effects, on each fighter its zone and mask reach, through the formulas.</summary>
/// <remarks>
/// <para>
/// Every effect as data (D177): the level's effects, or its critical effects on a critical hit, each read from the pack,
/// its kind from <see cref="EffectActions"/>, the fighters it reaches from <see cref="CastRules"/>, its value from
/// <see cref="EffectFormulas"/>. Nothing is drawn: an effect's roll stays a range, and an effect of a random group is
/// foreseen with its group marked, so that a search weighs what may happen.
/// </para>
/// <para>
/// Measured on the 49 fights captured by <c>scripts/fights.cs</c> (D195), action by action against what the server
/// announced. Not foreseen here: what the effects cast in turn, which the AI's imagination casts after the effects before
/// them (D227); triggered buffs as they trigger, and the effects of <see cref="EffectKind.Other"/>.
/// </para>
/// <para>
/// Built once per pack and shared, from any thread. A search foresees thousands of casts a turn (D200): the fighters' cells
/// are reckoned once a cast, each effect's zone once, and the steps of its decrease once for all its targets, with nothing
/// allocated but what the caller's list grows by.
/// </para>
/// </remarks>
public sealed class SpellEffects
{
    private const string AtOnce = "I";

    // Zones this large reach the whole map: their damage does not decrease (D195).
    private const int MaxDecreasingSize = 50;

    [ThreadStatic]
    private static List<(Combatant Fighter, CriterionAnswer Selected)>? _reached;

    private readonly SpellCatalog _spells;
    private readonly CastRules _rules;
    private readonly EffectActions _actions;
    private readonly WeaponBlows? _blows;

    /// <summary>Initializes a new instance of the <see cref="SpellEffects"/> class.</summary>
    /// <param name="spells">The pack's spells.</param>
    /// <param name="rules">The rules of a cast, which find the fighters an effect reaches.</param>
    /// <param name="actions">What each effect does.</param>
    public SpellEffects(SpellCatalog spells, CastRules rules, EffectActions actions)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(actions);

        _spells = spells;
        _rules = rules;
        _actions = actions;
    }

    /// <summary>Initializes a new instance of the <see cref="SpellEffects"/> class that foresees the weapons' blows too.</summary>
    /// <param name="blows">The pack's spells and the weapons' blows.</param>
    /// <param name="rules">The rules of a cast, built on the blows' spells.</param>
    /// <param name="actions">What each effect does.</param>
    public SpellEffects(WeaponBlows blows, CastRules rules, EffectActions actions)
        : this((blows ?? throw new ArgumentNullException(nameof(blows))).Spells, rules, actions)
    {
        _blows = blows;
    }

    private static (int Base, int Damage) Boosts(Combatant caster, int spellId)
    {
        // A spell's buffs on its caster raise its base damage, before the characteristics, and its damage after (D195).
        int baseBonus = 0, damage = 0;

        foreach (var modifier in caster.Modifiers)
        {
            if (modifier.SpellId != spellId)
                continue;

            if (modifier.Kind is SpellModifierKind.BaseDamage)
                baseBonus += modifier.Value;
            else if (modifier.Kind is SpellModifierKind.Damage)
                damage += modifier.Value;
        }

        return (baseBonus, damage);
    }

    private static LifeRange Value(in SpellEffect effect, EffectKind kind, EffectElement element, Combatant caster, Combatant target, int away, bool critical, int baseBonus, int damageBonus, int? weaponBonus)
    {
        var roll = EffectFormulas.Roll(effect);

        switch (kind)
        {
            case EffectKind.Damage or EffectKind.Steal:
                {
                    var boosted = roll + new LifeRange(baseBonus, baseBonus);
                    var dealt = (weaponBonus is { } bonus ? EffectFormulas.DealtByWeapon(boosted, caster, element, critical, bonus) : EffectFormulas.Dealt(boosted, caster, element, critical)) + new LifeRange(damageBonus, damageBonus);
                    var melee = caster.Cell is not Combatant.NoCell && target.Cell is not Combatant.NoCell && MapGeometry.Distance(caster.Cell, target.Cell) is 1;

                    return EffectFormulas.Received(EffectFormulas.Decreased(dealt, effect.Zone, away), caster, target, element, melee, weaponBonus is not null);
                }

            case EffectKind.Heal:
                return EffectFormulas.Decreased(EffectFormulas.Healed(roll, caster, element), effect.Zone, away);
            case EffectKind.PercentHeal:
                return new LifeRange(target.MaxLife * roll.Min / 100, target.MaxLife * roll.Max / 100);
            case EffectKind.LifeShield:
                return new LifeRange(caster.MaxLife * roll.Min / 100, caster.MaxLife * roll.Max / 100);
            case EffectKind.LevelShield:
                return new LifeRange(LevelShare(caster.Level, roll.Min), LevelShare(caster.Level, roll.Max));
            default:
                return roll;
        }
    }

    /// <summary>Gets what each effect of a cast is foreseen to do to each fighter it reaches.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight, the caster among them, as they stand before the cast.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="level">The level cast, of the pack's spells.</param>
    /// <param name="cell">The cell cast on.</param>
    /// <param name="critical">Whether the cast is a critical hit: its level's critical effects apply instead.</param>
    /// <returns>The effects foreseen, in the level's order, each on each fighter it reaches.</returns>
    public IReadOnlyList<ForeseenEffect> Foresee(FightMap map, IReadOnlyList<Combatant> fighters, Combatant caster, in SpellLevel level, int cell, bool critical)
    {
        var foreseen = new List<ForeseenEffect>();

        Foresee(map, fighters, caster, level, cell, critical, foreseen);

        return foreseen;
    }

    /// <summary>Adds what each effect of a cast is foreseen to do to each fighter it reaches to a list, as the other overload gives them.</summary>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">Every fighter of the fight, the caster among them, as they stand before the cast.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="level">The level cast, of the pack's spells.</param>
    /// <param name="cell">The cell cast on.</param>
    /// <param name="critical">Whether the cast is a critical hit: its level's critical effects apply instead.</param>
    /// <param name="foreseen">The list the effects foreseen are added to, in the level's order.</param>
    /// <param name="whole">Whether a zone's effects reach every fighter whole, its decrease left out: a mark's spell on the fighter it strikes (9f4).</param>
    public void Foresee(FightMap map, IReadOnlyList<Combatant> fighters, Combatant caster, in SpellLevel level, int cell, bool critical, List<ForeseenEffect> foreseen, bool whole = false)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(fighters);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(foreseen);

        var effects = critical && level.CriticalEffectCount > 0 ? _spells.CriticalEffectsOf(level) : _spells.EffectsOf(level);
        var (baseBonus, damageBonus) = Boosts(caster, level.SpellId);

        // A weapon's blow deals a weapon's damage, its critical hit adding the weapon's bonus (D220).
        int? weaponBonus = WeaponBlows.IsBlow(level) ? _blows?.CriticalBonusOf(level) ?? 0 : null;
        var occupied = CastRules.OccupiedBy(fighters);
        var reached = _reached ??= [];
        Span<int> steps = fighters.Count <= 64 ? stackalloc int[fighters.Count] : new int[fighters.Count];

        foreach (var effect in effects)
        {
            // Measured live (D203): an effect for the client only is what it previews, never what the server does; a
            // Xelor's Refraction showed its circle of damage, which the server dealt on none of 16 casts.
            if ((effect.Properties & SpellEffectProperties.ForClientOnly) is not 0)
                continue;

            var action = _actions.Of(effect.EffectId);
            var immediate = effect.Delay is 0 && _spells.TriggersOf(effect) == AtOnce;
            var element = EffectFormulas.Resolve(action.Element, caster);

            reached.Clear();
            _rules.Reached(map, fighters, occupied, caster, effect, cell, reached);

            if (reached.Count is 0)
                continue;

            if (whole)
                steps[..reached.Count].Clear();
            else
                Steps(effect, map, occupied, caster, cell, reached, steps);

            for (var index = 0; index < reached.Count; index++)
            {
                var (target, selected) = reached[index];
                var value = Value(effect, action.Kind, element, caster, target, steps[index], critical, baseBonus, damageBonus, weaponBonus);

                var grade = action.Kind is EffectKind.CastSpell ? Math.Max(1, effect.DiceSide) : 0;

                foreseen.Add(new ForeseenEffect(target.Id, effect.EffectId, action, element, value, selected is CriterionAnswer.True, effect.Random > 0, immediate, effect.Duration, grade));
            }
        }

        reached.Clear();
    }

    private static int LevelShare(int level, int value)
    {
        return (int)Math.Round(level * value / 100.0, MidpointRounding.AwayFromZero);
    }

    private void Steps(in SpellEffect effect, FightMap map, in CellSet occupied, Combatant caster, int cell, List<(Combatant Fighter, CriterionAnswer Selected)> reached, Span<int> steps)
    {
        var zone = effect.Zone;
        var counted = zone.DamageDecreaseStepPercent > 0 && zone.Size <= MaxDecreasingSize && zone.Shape is not (SpellZoneShape.Custom or SpellZoneShape.LineFromCaster);
        var left = 0;

        for (var index = 0; index < reached.Count; index++)
        {
            var target = reached[index].Fighter;

            // The centre's own fighter, and one the zone does not count steps for, are no step away.
            steps[index] = counted && target.Cell is not Combatant.NoCell && target.Cell != cell ? -1 : 0;
            left += steps[index] < 0 ? 1 : 0;
        }

        if (left is 0)
            return;

        // Measured (D195): a target's steps away from the centre are counted as its zone counts its size, a square's
        // diagonal one step and a circle's two: the least size of the zone's shape that reaches it, past the zone's least.
        // Each size is reckoned once for every target the effect reaches.
        var from = caster.Cell is Combatant.NoCell ? cell : caster.Cell;
        var listed = _spells.CellsOf(zone);

        for (var size = (int)zone.MinSize; size <= zone.Size && left > 0; size++)
        {
            var cells = ZoneCells.Of(zone with { Size = (byte)size }, listed, map, occupied, from, cell);

            for (var index = 0; index < reached.Count; index++)
            {
                if (steps[index] < 0 && cells.Contains(reached[index].Fighter.Cell))
                {
                    steps[index] = size - zone.MinSize;
                    left--;
                }
            }
        }

        for (var index = 0; index < reached.Count; index++)
        {
            if (steps[index] < 0)
                steps[index] = Math.Max(0, MapGeometry.Distance(cell, reached[index].Fighter.Cell) - zone.MinSize);
        }
    }
}
