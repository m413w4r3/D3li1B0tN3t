using System.Collections.Immutable;
using D3B.Data.Monsters;
using D3B.Engine.Effects;

namespace D3B.Engine.Fights;

/// <summary>The fighter a summon brings into a fight, as the server makes it from its monster's grade and its summoner.</summary>
/// <remarks>
/// <para>
/// Measured on the 57 summons of the 100 game connections of 3.7.12.12, by 8 breeds and 2 monsters (9f2): the life is the
/// grade's, plus the share its bonus names of the summoner's base life, characteristic 0, which a character holds at 50 and 5
/// a level; strength, intelligence, chance, agility and wisdom are the grade's, raised by a hundredth a level of a character
/// summoning, plus the share its bonus names of the summoner's; each other characteristic is the grade's plus that share of
/// the summoner's; the AP and range are the grade's, the MP too but never below 0; the dodge is a tenth of the agility plus
/// the grade's, the lock the same and never below 0, the dodge of AP and MP removed a tenth of the wisdom, which the server
/// gives no more, plus the grade's and the share of the summoner's. 57 of 57 lives and AP, 47 of 57 MP before they were
/// kept at 0; of every other characteristic, all but 3 of a Steamer's turret's damage bonuses, which the server gave none.
/// </para>
/// <para>A summon's level is its summoner's, as the references read it, when the summoner is a character; its grade's otherwise.</para>
/// </remarks>
public static class Summons
{
    private const int BaseLife = 0;
    private const int Vitality = 11;
    private const int ApDodge = 27;
    private const int MpDodge = 28;
    private const int Range = 19;
    private const int MaxSummons = 26;
    private const int Dodge = 78;
    private const int Lock = 79;
    private const int CriticalDamageReduction = 87;

    /// <summary>Gets the fighter a summon brings.</summary>
    /// <param name="summoner">Who summons it.</param>
    /// <param name="monster">Its monster.</param>
    /// <param name="grade">Its monster's grade.</param>
    /// <param name="id">Its id.</param>
    /// <param name="cell">The cell it comes on.</param>
    /// <returns>The summon, of its summoner's team, its life whole.</returns>
    public static Combatant Of(Combatant summoner, in Monster monster, in MonsterGrade grade, long id, int cell)
    {
        ArgumentNullException.ThrowIfNull(summoner);

        var stats = grade.Characteristics;
        var bonus = grade.Bonus;
        var level = summoner.IsCharacter ? summoner.Level : 0;
        var characteristics = ImmutableSortedDictionary.CreateBuilder<int, int>();

        int Scaled(int value)
        {
            return value + (value * level / 100);
        }

        int Shared(int id, int share)
        {
            return summoner.Characteristic(id) * share / 100;
        }

        void Set(int id, int value)
        {
            if (value is not 0)
                characteristics[id] = value;
        }

        Set(Vitality, stats.Vitality);
        Set(FightCharacteristic.Strength, Scaled(stats.Strength) + Shared(FightCharacteristic.Strength, bonus.Strength));
        Set(FightCharacteristic.Intelligence, Scaled(stats.Intelligence) + Shared(FightCharacteristic.Intelligence, bonus.Intelligence));
        Set(FightCharacteristic.Chance, Scaled(stats.Chance) + Shared(FightCharacteristic.Chance, bonus.Chance));
        Set(FightCharacteristic.Agility, Scaled(stats.Agility) + Shared(FightCharacteristic.Agility, bonus.Agility));
        Set(FightCharacteristic.AllDamageBonus, stats.DamageBonus + Shared(FightCharacteristic.AllDamageBonus, bonus.DamageBonus));
        Set(FightCharacteristic.CriticalHits, stats.CriticalHitBonus + Shared(FightCharacteristic.CriticalHits, bonus.CriticalHitBonus));
        Set(Range, stats.RangeBonus + Shared(Range, bonus.RangeBonus));
        Set(FightCharacteristic.Power, stats.PercentDamageBonus + Shared(FightCharacteristic.Power, bonus.PercentDamageBonus));
        Set(MaxSummons, stats.MaxSummon + Shared(MaxSummons, bonus.MaxSummon));
        Set(FightCharacteristic.HealBonus, stats.HealBonus + Shared(FightCharacteristic.HealBonus, bonus.HealBonus));
        Set(FightCharacteristic.PushDamageBonus, stats.PushDamageBonus + Shared(FightCharacteristic.PushDamageBonus, bonus.PushDamageBonus));
        Set(FightCharacteristic.PushDamageReduction, stats.PushDamageReduction + Shared(FightCharacteristic.PushDamageReduction, bonus.PushDamageReduction));
        Set(FightCharacteristic.CriticalDamageBonus, stats.CriticalDamageBonus + Shared(FightCharacteristic.CriticalDamageBonus, bonus.CriticalDamageBonus));
        Set(CriticalDamageReduction, stats.CriticalDamageReduction + Shared(CriticalDamageReduction, bonus.CriticalDamageReduction));

        // Earth, fire, water, air, then neutral, as the ids follow each other from earth's.
        ReadOnlySpan<(int Grade, int Bonus)> resists = [(stats.ReductionEarth, bonus.ReductionEarth), (stats.ReductionFire, bonus.ReductionFire), (stats.ReductionWater, bonus.ReductionWater), (stats.ReductionAir, bonus.ReductionAir), (stats.ReductionNeutral, bonus.ReductionNeutral)];
        ReadOnlySpan<(int Grade, int Bonus)> reductions = [(stats.ReductionEarthFlat, bonus.ReductionEarthFlat), (stats.ReductionFireFlat, bonus.ReductionFireFlat), (stats.ReductionWaterFlat, bonus.ReductionWaterFlat), (stats.ReductionAirFlat, bonus.ReductionAirFlat), (stats.ReductionNeutralFlat, bonus.ReductionNeutralFlat)];
        ReadOnlySpan<(int Grade, int Bonus)> damages = [(stats.EarthDamageBonus, bonus.EarthDamageBonus), (stats.FireDamageBonus, bonus.FireDamageBonus), (stats.WaterDamageBonus, bonus.WaterDamageBonus), (stats.AirDamageBonus, bonus.AirDamageBonus), (stats.NeutralDamageBonus, bonus.NeutralDamageBonus)];

        for (var element = 0; element < 5; element++)
        {
            Set(FightCharacteristic.EarthResistPercent + element, resists[element].Grade + Shared(FightCharacteristic.EarthResistPercent + element, resists[element].Bonus));
            Set(FightCharacteristic.EarthReduction + element, reductions[element].Grade + Shared(FightCharacteristic.EarthReduction + element, reductions[element].Bonus));
            Set(FightCharacteristic.EarthDamageBonus + element, damages[element].Grade + Shared(FightCharacteristic.EarthDamageBonus + element, damages[element].Bonus));
        }

        var agility = Scaled(stats.Agility) + Shared(FightCharacteristic.Agility, bonus.Agility);
        var wisdom = Scaled(stats.Wisdom);
        var dodge = (agility / 10) + stats.TackleEvade;
        var tackle = Math.Max(0, (agility / 10) + stats.TackleBonus);

        Set(ApDodge, (wisdom / 10) + stats.ApLostDodge + Shared(ApDodge, bonus.Wisdom));
        Set(MpDodge, (wisdom / 10) + stats.MpLostDodge + Shared(MpDodge, bonus.Wisdom));
        Set(Dodge, dodge);
        Set(Lock, tackle);

        var life = Scaled(stats.LifePoints) + stats.Vitality + Shared(BaseLife, bonus.LifePoints);
        var movement = Math.Max(0, (int)stats.MovementPoints);

        return new Combatant
        {
            Id = id,
            Team = summoner.Team,
            Cell = cell,
            Life = life,
            MaxLife = life,
            ActionPoints = stats.ActionPoints,
            MovementPoints = movement,
            RangeBonus = characteristics.GetValueOrDefault(Range),
            Lock = tackle,
            Dodge = dodge,
            MaxSummons = characteristics.TryGetValue(MaxSummons, out var most) ? most : null,
            MonsterId = monster.Id,
            Grade = grade.Grade,
            MonsterProperties = monster.Properties,
            SummonerId = summoner.Id,
            Level = summoner.IsCharacter ? summoner.Level : grade.Level,
            Characteristics = characteristics.ToImmutable(),
        };
    }
}
