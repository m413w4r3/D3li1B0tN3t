using D3B.Data.Spells;
using D3B.Engine.Fights;

namespace D3B.Engine.Effects;

/// <summary>The formulas of damage, steals, heals, shields and a push's damage, pure, each step rounded down as measured.</summary>
/// <remarks>
/// <para>
/// The references' order (D181), each step measured on the 49 fights captured by <c>scripts/fights.cs</c> (D195): the roll
/// between the effect's dice; the caster's characteristic of the element and its power raising it, in percent; its fixed
/// bonuses; the zone's decrease away from its centre; the target's fixed resistance, then its resistance in percent; the
/// multipliers of damage dealt and received. Every step that divides rounds down, as the references do.
/// </para>
/// <para>Pure: the formulas read fighters and change nothing, so that an AI's search calls them on fighters it imagines.</para>
/// </remarks>
public static class EffectFormulas
{
    private const int Percent = 100;

    /// <summary>Gets the chance of a cast being a critical hit, which the server draws and never tells.</summary>
    /// <param name="level">The level cast.</param>
    /// <param name="caster">The caster.</param>
    /// <returns>The chance, from 0 to 1: none for a level that has no chance of one.</returns>
    /// <remarks>
    /// Measured on the 595 casts captured of levels with a chance (D207): the level's chance plus the caster's characteristic 18,
    /// in percent, within 0 and 100, foresaw 43 critical hits, and the server drew 40; none of the 217 casts of monsters whose
    /// characteristic 18 is below the level's chance, and none of a level without a chance, was one.
    /// </remarks>
    public static double CriticalChance(in SpellLevel level, Combatant caster)
    {
        ArgumentNullException.ThrowIfNull(caster);

        if (level.CriticalHitProbability <= 0)
            return 0;

        return Math.Clamp(level.CriticalHitProbability + caster.Characteristic(FightCharacteristic.CriticalHits), 0, Percent) / (double)Percent;
    }

    /// <summary>Gets a fighter's characteristic as the formulas read it: a multiplier of damage or heals the server left out is 100.</summary>
    /// <param name="fighter">The fighter.</param>
    /// <param name="characteristic">The characteristic's id.</param>
    /// <returns>Its value.</returns>
    /// <remarks>
    /// The server gives each multiplier as 100 when nothing changes it, so that one it left out changes nothing, and a boost
    /// of one adds to 100 (D209).
    /// </remarks>
    public static int ValueOf(Combatant fighter, int characteristic)
    {
        ArgumentNullException.ThrowIfNull(fighter);

        if (fighter.Characteristics.TryGetValue(characteristic, out var value))
            return value;

        return characteristic is FightCharacteristic.DealtDamageMultiplier or FightCharacteristic.DealtDamageMultiplierDistance
            or FightCharacteristic.ReceivedDamageMultiplierDistance or FightCharacteristic.DealtDamageMultiplierSpells
            or FightCharacteristic.ReceivedDamageMultiplierMelee or FightCharacteristic.DealtDamageMultiplierMelee
            or FightCharacteristic.ReceivedDamageMultiplierSpells or FightCharacteristic.DealtHealMultiplier
            or FightCharacteristic.DealtDamageMultiplierWeapons or FightCharacteristic.ReceivedDamageMultiplierWeapons
            ? Percent
            : 0;
    }

    /// <summary>Gets the characteristic that raises an element's damage.</summary>
    /// <param name="element">The element, resolved.</param>
    /// <returns>The characteristic's id, or 0 for none.</returns>
    public static int CharacteristicOf(EffectElement element)
    {
        return element switch
        {
            EffectElement.Neutral or EffectElement.Earth => FightCharacteristic.Strength,
            EffectElement.Fire => FightCharacteristic.Intelligence,
            EffectElement.Water => FightCharacteristic.Chance,
            EffectElement.Air => FightCharacteristic.Agility,
            _ => 0,
        };
    }

    /// <summary>Gets the element a caster's best or worst element is, by the characteristic raising it most or least.</summary>
    /// <param name="element">The effect's element.</param>
    /// <param name="caster">The caster.</param>
    /// <returns>The element itself, or the caster's best or worst, earth first on a tie.</returns>
    public static EffectElement Resolve(EffectElement element, Combatant caster)
    {
        ArgumentNullException.ThrowIfNull(caster);

        if (element is not (EffectElement.Best or EffectElement.Worst))
            return element;

        var found = EffectElement.Earth;

        foreach (var candidate in (ReadOnlySpan<EffectElement>)[EffectElement.Fire, EffectElement.Water, EffectElement.Air])
        {
            var value = caster.Characteristic(CharacteristicOf(candidate));
            var held = caster.Characteristic(CharacteristicOf(found));

            if (element is EffectElement.Best ? value > held : value < held)
                found = candidate;
        }

        return found;
    }

    /// <summary>Gets an effect's roll: between its dice, or its value when it has none.</summary>
    /// <param name="effect">The effect, of the pack's spells.</param>
    /// <returns>The least and the most it rolls.</returns>
    public static LifeRange Roll(in SpellEffect effect)
    {
        if (effect.DiceNum is 0 && effect.DiceSide is 0)
            return new LifeRange(effect.Value, effect.Value);

        return new LifeRange(effect.DiceNum, Math.Max(effect.DiceNum, effect.DiceSide));
    }

    /// <summary>Gets the damage a caster deals before its target and the zone take any off.</summary>
    /// <param name="roll">The effect's roll, its base damage modifiers added.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="element">The element, resolved.</param>
    /// <param name="critical">Whether the cast is a critical hit.</param>
    /// <returns>The damage: the roll raised by the element's characteristic and power, in percent, then the fixed bonuses.</returns>
    public static LifeRange Dealt(LifeRange roll, Combatant caster, EffectElement element, bool critical)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var raise = Math.Max(0, Percent + caster.Characteristic(CharacteristicOf(element)) + caster.Characteristic(FightCharacteristic.Power));
        var bonus = caster.Characteristic(FightCharacteristic.AllDamageBonus) + caster.Characteristic(ElementalId(FightCharacteristic.EarthDamageBonus, element))
            + (critical ? caster.Characteristic(FightCharacteristic.CriticalDamageBonus) : 0);

        // Measured (9f3): an explosion's damage, raised and its bonuses added, is raised again by the combo of the bombs set off.
        var combo = Math.Max(0, Percent + caster.Characteristic(FightCharacteristic.BombComboBonus));

        return new LifeRange(((roll.Min * raise / Percent) + bonus) * combo / Percent, ((roll.Max * raise / Percent) + bonus) * combo / Percent);
    }

    /// <summary>Gets the damage a weapon's blow deals before its target and the zone take any off.</summary>
    /// <param name="roll">The effect's roll, its base damage modifiers added.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="element">The element, resolved.</param>
    /// <param name="critical">Whether the blow is a critical hit.</param>
    /// <param name="criticalBonus">What the weapon's critical hit adds, its <c>criticalHitBonus</c>.</param>
    /// <returns>The damage: the roll raised by the element's characteristic, power and weapons' power, in percent, then the
    /// fixed bonuses, the weapon's critical bonus among them on a critical hit, then the weapon mastery, in percent.</returns>
    /// <remarks>
    /// The references' order (D181): the damage calculation the Dofus 2 client ran, which Bubble ported, raises a weapon's
    /// blow by weapons' power where a spell's has none, adds the weapon's critical bonus to the fixed bonuses, and raises
    /// the whole by the weapon mastery. Measured on the characters captured (D218): weapons' power and mastery 0 on each, so
    /// that the critical bonus is left to measure.
    /// </remarks>
    public static LifeRange DealtByWeapon(LifeRange roll, Combatant caster, EffectElement element, bool critical, int criticalBonus)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var raise = Math.Max(0, Percent + caster.Characteristic(CharacteristicOf(element)) + caster.Characteristic(FightCharacteristic.Power) + caster.Characteristic(FightCharacteristic.WeaponPower));
        var bonus = caster.Characteristic(FightCharacteristic.AllDamageBonus) + caster.Characteristic(ElementalId(FightCharacteristic.EarthDamageBonus, element))
            + (critical ? caster.Characteristic(FightCharacteristic.CriticalDamageBonus) + criticalBonus : 0);
        var mastery = Math.Max(0, Percent + caster.Characteristic(FightCharacteristic.WeaponMastery));

        return new LifeRange(((roll.Min * raise / Percent) + bonus) * mastery / Percent, ((roll.Max * raise / Percent) + bonus) * mastery / Percent);
    }

    /// <summary>Gets damage lowered by a zone's decrease away from its centre.</summary>
    /// <param name="damage">The damage.</param>
    /// <param name="zone">The effect's zone.</param>
    /// <param name="away">The target's steps away from the zone's centre, as the zone counts them.</param>
    /// <returns>The damage less the zone's step for each step away, as many steps as the zone allows.</returns>
    public static LifeRange Decreased(LifeRange damage, in SpellZone zone, int away)
    {
        if (zone.DamageDecreaseStepPercent <= 0 || away <= 0)
            return damage;

        var steps = zone.MaxDamageDecreaseApplyCount > 0 ? Math.Min(away, zone.MaxDamageDecreaseApplyCount) : away;
        var kept = Math.Max(0, Percent - (steps * zone.DamageDecreaseStepPercent));

        return new LifeRange(damage.Min * kept / Percent, damage.Max * kept / Percent);
    }

    /// <summary>Gets the damage a target receives of what a caster dealt.</summary>
    /// <param name="dealt">The damage dealt, the zone's decrease taken off.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="target">The target.</param>
    /// <param name="element">The element, resolved.</param>
    /// <param name="melee">Whether the caster stands beside the target.</param>
    /// <returns>The damage less the target's fixed resistance, then its resistance in percent, then the multipliers.</returns>
    public static LifeRange Received(LifeRange dealt, Combatant caster, Combatant target, EffectElement element, bool melee)
    {
        return Received(dealt, caster, target, element, melee, weapon: false);
    }

    /// <summary>Gets the damage a target receives of what a caster dealt, by a spell or by a weapon's blow.</summary>
    /// <param name="dealt">The damage dealt, the zone's decrease taken off.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="target">The target.</param>
    /// <param name="element">The element, resolved.</param>
    /// <param name="melee">Whether the caster stands beside the target.</param>
    /// <param name="weapon">Whether a weapon's blow dealt it: the multipliers of weapons apply, where a spell's are those of spells (D220).</param>
    /// <returns>The damage less the target's fixed resistance, then its resistance in percent, then the multipliers.</returns>
    public static LifeRange Received(LifeRange dealt, Combatant caster, Combatant target, EffectElement element, bool melee, bool weapon)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(target);

        var reduction = target.Characteristic(ElementalId(FightCharacteristic.EarthReduction, element));
        var resisted = Percent - target.Characteristic(ElementalId(FightCharacteristic.EarthResistPercent, element));
        var dealtMultiplier = Multiplier(caster, FightCharacteristic.DealtDamageMultiplier);
        var spells = Multiplier(caster, weapon ? FightCharacteristic.DealtDamageMultiplierWeapons : FightCharacteristic.DealtDamageMultiplierSpells);
        var reach = Multiplier(caster, melee ? FightCharacteristic.DealtDamageMultiplierMelee : FightCharacteristic.DealtDamageMultiplierDistance);
        var receivedSpells = Multiplier(target, weapon ? FightCharacteristic.ReceivedDamageMultiplierWeapons : FightCharacteristic.ReceivedDamageMultiplierSpells);
        var receivedReach = Multiplier(target, melee ? FightCharacteristic.ReceivedDamageMultiplierMelee : FightCharacteristic.ReceivedDamageMultiplierDistance);

        return new LifeRange(Receive(dealt.Min), Receive(dealt.Max));

        int Receive(int value)
        {
            var received = Math.Max(0, (value - reduction) * resisted / Percent);

            received = received * dealtMultiplier / Percent;
            received = received * spells / Percent;
            received = received * reach / Percent;
            received = received * receivedSpells / Percent;
            received = received * receivedReach / Percent;

            // Measured (D195): a buff multiplying the damage its fighter receives, 85% in the captures, applies last.
            foreach (var share in target.ReceivedDamageShares)
                received = received * share / Percent;

            return Math.Max(0, received);
        }
    }

    /// <summary>Gets the heal a caster gives before its target's missing life caps it.</summary>
    /// <param name="roll">The effect's roll.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="element">The element, resolved.</param>
    /// <returns>The roll raised by the element's characteristic, in percent, then the heals bonus, then the heals multiplier.</returns>
    public static LifeRange Healed(LifeRange roll, Combatant caster, EffectElement element)
    {
        ArgumentNullException.ThrowIfNull(caster);

        var raise = Math.Max(0, Percent + caster.Characteristic(CharacteristicOf(element)));
        var bonus = caster.Characteristic(FightCharacteristic.HealBonus);
        var multiplier = Multiplier(caster, FightCharacteristic.DealtHealMultiplier);

        return new LifeRange(((roll.Min * raise / Percent) + bonus) * multiplier / Percent, ((roll.Max * raise / Percent) + bonus) * multiplier / Percent);
    }

    /// <summary>Gets the part of a loss of life lost for the rest of the fight.</summary>
    /// <param name="loss">The life lost.</param>
    /// <param name="target">The fighter losing it.</param>
    /// <returns>The loss times the target's erosion, in percent, rounded down.</returns>
    public static int Eroded(int loss, Combatant target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return loss * Math.Max(0, target.Characteristic(FightCharacteristic.Erosion)) / Percent;
    }

    /// <summary>Gets the damage a fighter takes when a push stops against an obstacle.</summary>
    /// <param name="cells">The cells the push could not move it.</param>
    /// <param name="caster">The caster pushing: its level, or its summoner's for a summon.</param>
    /// <param name="level">The level the formula reads.</param>
    /// <param name="target">The fighter pushed.</param>
    /// <returns>The damage.</returns>
    public static int PushDamage(int cells, Combatant caster, int level, Combatant target)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(target);

        var force = (level / 2) + 32 + caster.Characteristic(FightCharacteristic.PushDamageBonus) - target.Characteristic(FightCharacteristic.PushDamageReduction);

        return Math.Max(0, cells * force / 4);
    }

    private static int Multiplier(Combatant fighter, int characteristic)
    {
        return ValueOf(fighter, characteristic);
    }

    private static int ElementalId(int earth, EffectElement element)
    {
        // The elements' characteristics follow each other from earth's: earth, fire, water, air, then neutral.
        return element switch
        {
            EffectElement.Earth => earth,
            EffectElement.Fire => earth + 1,
            EffectElement.Water => earth + 2,
            EffectElement.Air => earth + 3,
            EffectElement.Neutral => earth + 4,
            _ => 0,
        };
    }
}
