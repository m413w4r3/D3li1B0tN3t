using System.Collections.Immutable;
using D3B.Data.Monsters;
using D3B.Engine.Effects;
using D3B.Engine.Fights;

namespace D3B.Engine.Tests.Fights;

/// <summary>The fighter a summon brings, from its monster's grade and its summoner, as the server makes it (9f2).</summary>
public sealed class SummonsTests
{
    private static readonly Monster _monster = new() { Id = 900, Properties = MonsterProperties.CanPlay | MonsterProperties.CanMove | MonsterProperties.UseSummonSlot, GradeCount = 1 };

    private static readonly MonsterGrade _grade = new()
    {
        Grade = 1,
        Level = 30,
        Characteristics = new MonsterCharacteristics
        {
            ActionPoints = 4,
            MovementPoints = -1,
            Agility = 100,
            Wisdom = 40,
            Vitality = 10,
            TackleBonus = -500,
            TackleEvade = 2,
            ReductionFire = 15,
        },
        Bonus = new MonsterCharacteristics { LifePoints = 60, Strength = 50, DamageBonus = 100, FireDamageBonus = 100 },
    };

    private static Combatant Summoner(bool character)
    {
        return new Combatant
        {
            Id = 1,
            Team = 0,
            Cell = TestFights.Middle,
            BreedId = character ? 5 : null,
            MonsterId = character ? null : 31,
            Level = 100,
            Characteristics = ImmutableSortedDictionary.CreateRange(new Dictionary<int, int>
            {
                [0] = 550,
                [FightCharacteristic.Strength] = 200,
                [FightCharacteristic.AllDamageBonus] = 12,
                [FightCharacteristic.EarthDamageBonus + 1] = 7,
            }),
        };
    }

    [Fact]
    public void ASummonTakesItsBonusShareOfItsSummonersBaseLifeAndCharacteristics()
    {
        var summon = Summons.Of(Summoner(character: true), _monster, _grade, -5, TestFights.Off(1, 0));

        Assert.Equal((330 + 10, 330 + 10), (summon.Life, summon.MaxLife));
        Assert.Equal((4, 0), (summon.ActionPoints, summon.MovementPoints));
        Assert.Equal(100, summon.Characteristic(FightCharacteristic.Strength));
        Assert.Equal(12, summon.Characteristic(FightCharacteristic.AllDamageBonus));
        Assert.Equal(7, summon.Characteristic(FightCharacteristic.EarthDamageBonus + 1));
        Assert.Equal(15, summon.Characteristic(FightCharacteristic.EarthResistPercent + 1));
        Assert.Equal((1L, 0, 1, 100), (summon.SummonerId!.Value, summon.Team, summon.Grade!.Value, summon.Level));
    }

    [Fact]
    public void ACharactersSummonGrowsWithItsLevelAndDodgesByItsAgilityWithoutLockingBelowZero()
    {
        var summon = Summons.Of(Summoner(character: true), _monster, _grade, -5, TestFights.Off(1, 0));

        Assert.Equal(200, summon.Characteristic(FightCharacteristic.Agility));
        Assert.Equal((22, 0), (summon.Dodge, summon.Lock));
        Assert.Equal(8, summon.Characteristic(27));
    }

    [Fact]
    public void AMonstersSummonKeepsItsGradesCharacteristics()
    {
        var summon = Summons.Of(Summoner(character: false), _monster, _grade, -5, TestFights.Off(1, 0));

        Assert.Equal(100, summon.Characteristic(FightCharacteristic.Agility));
        Assert.Equal(30, summon.Level);
    }
}
