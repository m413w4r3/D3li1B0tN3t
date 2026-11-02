using System.Collections.Immutable;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;

namespace D3B.Engine.Tests.Effects;

/// <summary>The formulas as the bench measured them on the captured fights (D195), on fighters the tests build.</summary>
public sealed class EffectFormulasTests
{
    private static Combatant Fighter(params (int Id, int Value)[] characteristics)
    {
        return new Combatant { Id = 1, Characteristics = characteristics.ToImmutableSortedDictionary(pair => pair.Id, pair => pair.Value) };
    }

    private static SpellZone Zone(int step = 10, int steps = 4)
    {
        return new SpellZone { Shape = SpellZoneShape.Circle, Size = 2, DamageDecreaseStepPercent = (sbyte)step, MaxDamageDecreaseApplyCount = (sbyte)steps };
    }

    [Fact]
    public void TheRollIsBetweenTheDiceOrTheValueWhenThereAreNone()
    {
        Assert.Equal(new LifeRange(13, 15), EffectFormulas.Roll(new SpellEffect { DiceNum = 13, DiceSide = 15 }));
        Assert.Equal(new LifeRange(2, 2), EffectFormulas.Roll(new SpellEffect { DiceNum = 2 }));
        Assert.Equal(new LifeRange(4, 4), EffectFormulas.Roll(new SpellEffect { Value = 4 }));
    }

    [Fact]
    public void DamageDealtIsTheRollRaisedByTheElementsCharacteristicAndPowerThenTheBonuses()
    {
        var caster = Fighter((FightCharacteristic.Intelligence, 140), (FightCharacteristic.Power, 3), (FightCharacteristic.AllDamageBonus, 10), (89, 6), (FightCharacteristic.CriticalDamageBonus, 5));

        Assert.Equal(new LifeRange(47, 52), EffectFormulas.Dealt(new LifeRange(13, 15), caster, EffectElement.Fire, critical: false));
        Assert.Equal(new LifeRange(52, 57), EffectFormulas.Dealt(new LifeRange(13, 15), caster, EffectElement.Fire, critical: true));
        Assert.Equal(new LifeRange(23, 25), EffectFormulas.Dealt(new LifeRange(13, 15), caster, EffectElement.Earth, critical: false));
    }

    [Fact]
    public void AWeaponsBlowIsRaisedByWeaponsPowerThenItsMasteryItsCriticalHitAddingTheWeaponsBonus()
    {
        var caster = Fighter((FightCharacteristic.Strength, 100), (FightCharacteristic.WeaponPower, 20), (FightCharacteristic.AllDamageBonus, 4), (FightCharacteristic.CriticalDamageBonus, 2), (FightCharacteristic.WeaponMastery, 50));

        // 16 to 25 raised by 220%, 35 to 55, plus 4; then half as much again by the mastery.
        Assert.Equal(new LifeRange(58, 88), EffectFormulas.DealtByWeapon(new LifeRange(16, 25), caster, EffectElement.Earth, critical: false, criticalBonus: 10));
        Assert.Equal(new LifeRange(76, 106), EffectFormulas.DealtByWeapon(new LifeRange(16, 25), caster, EffectElement.Earth, critical: true, criticalBonus: 10));
        Assert.Equal(new LifeRange(36, 54), EffectFormulas.Dealt(new LifeRange(16, 25), caster, EffectElement.Earth, critical: false));
    }

    [Fact]
    public void AWeaponsBlowTakesTheWeaponsMultipliersAndNotTheSpells()
    {
        var caster = Fighter((FightCharacteristic.DealtDamageMultiplierWeapons, 150), (FightCharacteristic.DealtDamageMultiplierSpells, 200));
        var target = Fighter((FightCharacteristic.ReceivedDamageMultiplierWeapons, 50), (FightCharacteristic.ReceivedDamageMultiplierSpells, 300));

        Assert.Equal(new LifeRange(30, 37), EffectFormulas.Received(new LifeRange(40, 50), caster, target, EffectElement.Earth, melee: true, weapon: true));
        Assert.Equal(new LifeRange(240, 300), EffectFormulas.Received(new LifeRange(40, 50), caster, target, EffectElement.Earth, melee: true, weapon: false));
        Assert.Equal(new LifeRange(40, 50), EffectFormulas.Received(new LifeRange(40, 50), Fighter(), Fighter(), EffectElement.Earth, melee: true, weapon: true));
    }

    [Fact]
    public void AZoneTakesItsStepOffForEachStepAwayAsManyAsItAllows()
    {
        Assert.Equal(new LifeRange(50, 60), EffectFormulas.Decreased(new LifeRange(50, 60), Zone(), 0));
        Assert.Equal(new LifeRange(45, 54), EffectFormulas.Decreased(new LifeRange(50, 60), Zone(), 1));
        Assert.Equal(new LifeRange(30, 36), EffectFormulas.Decreased(new LifeRange(50, 60), Zone(), 7));
        Assert.Equal(new LifeRange(50, 60), EffectFormulas.Decreased(new LifeRange(50, 60), Zone(step: 0), 3));
    }

    [Fact]
    public void TheTargetTakesItsFixedResistanceOffThenItsPercentThenTheMultipliersAndItsShares()
    {
        var caster = Fighter((FightCharacteristic.DealtDamageMultiplier, 100));
        var target = Fighter((55, 2), (34, 25)) with { Id = 2 };

        Assert.Equal(new LifeRange(33, 37), EffectFormulas.Received(new LifeRange(47, 52), caster, target, EffectElement.Fire, melee: false));
        Assert.Equal(new LifeRange(28, 31), EffectFormulas.Received(new LifeRange(47, 52), caster, target with { ReceivedDamageShares = [85] }, EffectElement.Fire, melee: false));
        Assert.Equal(new LifeRange(0, 0), EffectFormulas.Received(new LifeRange(1, 2), caster, target, EffectElement.Fire, melee: true));
    }

    [Fact]
    public void AMultiplierTheServerLeftOutChangesNothing()
    {
        var target = Fighter() with { Id = 2 };

        Assert.Equal(new LifeRange(20, 24), EffectFormulas.Received(new LifeRange(20, 24), Fighter(), target, EffectElement.Water, melee: true));
        Assert.Equal(new LifeRange(10, 12), EffectFormulas.Received(new LifeRange(20, 24), Fighter((FightCharacteristic.DealtDamageMultiplierMelee, 50)), target, EffectElement.Water, melee: true));
    }

    [Fact]
    public void AHealIsTheRollRaisedByTheElementsCharacteristicThenTheHealsBonus()
    {
        var caster = Fighter((FightCharacteristic.Intelligence, 50), (FightCharacteristic.HealBonus, 4));

        Assert.Equal(new LifeRange(19, 23), EffectFormulas.Healed(new LifeRange(10, 13), caster, EffectElement.Fire));
    }

    [Fact]
    public void TheBestElementIsTheOneTheCharacteristicsRaiseMostEarthOnATie()
    {
        Assert.Equal(EffectElement.Water, EffectFormulas.Resolve(EffectElement.Best, Fighter((FightCharacteristic.Chance, 80), (FightCharacteristic.Agility, 40))));
        Assert.Equal(EffectElement.Earth, EffectFormulas.Resolve(EffectElement.Best, Fighter()));
        Assert.Equal(EffectElement.Earth, EffectFormulas.Resolve(EffectElement.Worst, Fighter((FightCharacteristic.Chance, 80))));
        Assert.Equal(EffectElement.Air, EffectFormulas.Resolve(EffectElement.Air, Fighter((FightCharacteristic.Chance, 80))));
    }

    [Fact]
    public void ErosionTakesItsShareOfALossForTheRestOfTheFight()
    {
        Assert.Equal(4, EffectFormulas.Eroded(45, Fighter((FightCharacteristic.Erosion, 10))));
        Assert.Equal(0, EffectFormulas.Eroded(45, Fighter()));
    }
}
