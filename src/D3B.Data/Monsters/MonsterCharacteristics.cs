using System.Runtime.InteropServices;

namespace D3B.Data.Monsters;

/// <summary>The characteristics of a monster's grade, or the bonus a summon of that grade takes from its summoner, named as the client names them.</summary>
/// <remarks>
/// The client holds both with one set of fields, the bonus unsigned and the grade signed, with the life, vitality and
/// initiative wider; the pack keeps every value at these widths, and the extraction fails on one that does not fit. Whether
/// a reduction is a percentage, and how a summon's bonus applies, is 9d's to measure. Unmanaged and packed, as
/// <see cref="MonsterGrade"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MonsterCharacteristics
{
    /// <summary>Gets the client's <c>lifePoints</c>: a grade's, the life the server gives the monster in a fight, measured.</summary>
    public int LifePoints { get; init; }

    /// <summary>Gets the client's <c>actionPoints</c>: a grade's, the AP the server gives the monster, measured.</summary>
    public short ActionPoints { get; init; }

    /// <summary>Gets the client's <c>movementPoints</c>: a grade's, the MP the server gives the monster, measured.</summary>
    public short MovementPoints { get; init; }

    /// <summary>Gets the client's <c>rangeBonus</c>.</summary>
    public short RangeBonus { get; init; }

    /// <summary>Gets the client's <c>vitality</c>.</summary>
    public int Vitality { get; init; }

    /// <summary>Gets the client's <c>strength</c>.</summary>
    public short Strength { get; init; }

    /// <summary>Gets the client's <c>intelligence</c>.</summary>
    public short Intelligence { get; init; }

    /// <summary>Gets the client's <c>chance</c>.</summary>
    public short Chance { get; init; }

    /// <summary>Gets the client's <c>agility</c>.</summary>
    public short Agility { get; init; }

    /// <summary>Gets the client's <c>wisdom</c>.</summary>
    public short Wisdom { get; init; }

    /// <summary>Gets the client's <c>damageBonus</c>.</summary>
    public short DamageBonus { get; init; }

    /// <summary>Gets the client's <c>percentDamageBonus</c>.</summary>
    public short PercentDamageBonus { get; init; }

    /// <summary>Gets the client's <c>healBonus</c>.</summary>
    public short HealBonus { get; init; }

    /// <summary>Gets the client's <c>trapDamageBonus</c>.</summary>
    public short TrapDamageBonus { get; init; }

    /// <summary>Gets the client's <c>trapDamageBonusPercent</c>.</summary>
    public short TrapDamageBonusPercent { get; init; }

    /// <summary>Gets the client's <c>criticalDamageBonus</c>.</summary>
    public short CriticalDamageBonus { get; init; }

    /// <summary>Gets the client's <c>pushDamageBonus</c>.</summary>
    public short PushDamageBonus { get; init; }

    /// <summary>Gets the client's <c>earthDamageBonus</c>.</summary>
    public short EarthDamageBonus { get; init; }

    /// <summary>Gets the client's <c>fireDamageBonus</c>.</summary>
    public short FireDamageBonus { get; init; }

    /// <summary>Gets the client's <c>waterDamageBonus</c>.</summary>
    public short WaterDamageBonus { get; init; }

    /// <summary>Gets the client's <c>airDamageBonus</c>.</summary>
    public short AirDamageBonus { get; init; }

    /// <summary>Gets the client's <c>neutralDamageBonus</c>.</summary>
    public short NeutralDamageBonus { get; init; }

    /// <summary>Gets the client's <c>criticalHitBonus</c>.</summary>
    public short CriticalHitBonus { get; init; }

    /// <summary>Gets the client's <c>apAttack</c>.</summary>
    public short ApAttack { get; init; }

    /// <summary>Gets the client's <c>mpAttack</c>.</summary>
    public short MpAttack { get; init; }

    /// <summary>Gets the client's <c>paLostDodge</c>.</summary>
    public short ApLostDodge { get; init; }

    /// <summary>Gets the client's <c>mpLostDodge</c>.</summary>
    public short MpLostDodge { get; init; }

    /// <summary>Gets the client's <c>reductionEarth</c>.</summary>
    public short ReductionEarth { get; init; }

    /// <summary>Gets the client's <c>reductionWater</c>.</summary>
    public short ReductionWater { get; init; }

    /// <summary>Gets the client's <c>reductionFire</c>.</summary>
    public short ReductionFire { get; init; }

    /// <summary>Gets the client's <c>reductionAir</c>.</summary>
    public short ReductionAir { get; init; }

    /// <summary>Gets the client's <c>reductionNeutral</c>.</summary>
    public short ReductionNeutral { get; init; }

    /// <summary>Gets the client's <c>reductionEarthFlat</c>.</summary>
    public short ReductionEarthFlat { get; init; }

    /// <summary>Gets the client's <c>reductionAirFlat</c>.</summary>
    public short ReductionAirFlat { get; init; }

    /// <summary>Gets the client's <c>reductionFireFlat</c>.</summary>
    public short ReductionFireFlat { get; init; }

    /// <summary>Gets the client's <c>reductionWaterFlat</c>.</summary>
    public short ReductionWaterFlat { get; init; }

    /// <summary>Gets the client's <c>reductionNeutralFlat</c>.</summary>
    public short ReductionNeutralFlat { get; init; }

    /// <summary>Gets the client's <c>criticalDamageReduction</c>.</summary>
    public short CriticalDamageReduction { get; init; }

    /// <summary>Gets the client's <c>pushDamageReduction</c>.</summary>
    public short PushDamageReduction { get; init; }

    /// <summary>Gets the client's <c>damageReflect</c>.</summary>
    public short DamageReflect { get; init; }

    /// <summary>Gets the client's <c>maxSummon</c>.</summary>
    public short MaxSummon { get; init; }

    /// <summary>Gets the client's <c>honoursPoints</c>.</summary>
    public short HonourPoints { get; init; }

    /// <summary>Gets the client's <c>initiativeBonus</c>.</summary>
    public int InitiativeBonus { get; init; }

    /// <summary>Gets the client's <c>tackleBonus</c>.</summary>
    public short TackleBonus { get; init; }

    /// <summary>Gets the client's <c>tackleEvade</c>.</summary>
    public short TackleEvade { get; init; }

    /// <summary>Gets the client's <c>aPRemoval</c>, which only the bonus holds: 0 on a grade.</summary>
    public short ApRemoval { get; init; }
}
