using System.Collections.Immutable;
using D3B.Bot.Fights;
using D3B.Data.Monsters;
using D3B.Engine.Fights;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Fights;

public sealed class FightCombatantsTests
{
    private static readonly MonsterCatalog _monsters = new(
        ReadOnlyMemory<Monster>.Empty,
        ReadOnlyMemory<MonsterGrade>.Empty,
        ReadOnlyMemory<MonsterSpell>.Empty,
        ReadOnlyMemory<byte>.Empty,
        ReadOnlyMemory<MonsterRatio>.Empty,
        ReadOnlyMemory<MonsterRace>.Empty,
        ReadOnlyMemory<MonsterRace>.Empty);

    [Fact]
    public void AFighterGivesTheEngineItsCellCharacteristicsStatesModifiersBuffsAndDamageShares()
    {
        var fighter = new Fighter
        {
            Id = 7,
            Team = FightTeam.Defender,
            Cell = 300,
            Life = 40,
            MaxLife = 50,
            ActionPoints = 6,
            MovementPoints = 3,
            Level = 19,
            BreedId = 15,
            Characteristics = ImmutableSortedDictionary<int, int>.Empty.Add(19, 2).Add(26, 3).Add(78, 4).Add(79, 5).Add(15, 140),
            Buffs =
            [
                new Bot.Fights.FightBuff { Uid = 1, ActionId = 950, Kind = FightBuffKind.State, SpellId = 12_787, Parameter = 3 },
                new Bot.Fights.FightBuff { Uid = 2, ActionId = 281, Kind = FightBuffKind.SpellModifier, SpellId = 12_787, Value = 12_832, Parameter = 3 },
                new Bot.Fights.FightBuff { Uid = 3, ActionId = 1163, Kind = FightBuffKind.Triggered, Value = 85 },
                new Bot.Fights.FightBuff { Uid = 4, ActionId = 111, Kind = FightBuffKind.Boost, SpellId = 13_386, Value = 1 },
            ],
        };

        var combatant = FightCombatants.Of(fighter, _monsters);

        Assert.Equal((7L, 1, 300, 40, 50, 19), (combatant.Id, combatant.Team, combatant.Cell, combatant.Life, combatant.MaxLife, combatant.Level));
        Assert.Equal((2, 3, 4, 5, 140), (combatant.RangeBonus, combatant.MaxSummons, combatant.Dodge, combatant.Lock, combatant.Characteristic(15)));
        Assert.Equal([3], combatant.States);
        Assert.Equal([new SpellModifier(SpellModifierKind.Range, 12_832, 3)], combatant.Modifiers);
        Assert.Equal([new HeldBuff(12_787, 950, 3), new HeldBuff(12_787, 281, 12_832, 3), new HeldBuff(13_386, 111, 1)], combatant.Buffs);
        Assert.Equal([85], combatant.ReceivedDamageShares);
        Assert.True(combatant.IsCharacter);
    }

    [Fact]
    public void ADeadOrCarriedFighterStandsOnNoCell()
    {
        Assert.Equal(Combatant.NoCell, FightCombatants.Of(new Fighter { Id = 1, IsAlive = false, Cell = 300 }, _monsters).Cell);
        Assert.Equal(Combatant.NoCell, FightCombatants.Of(new Fighter { Id = 1, CarrierId = 2 }, _monsters).Cell);
        Assert.Null(FightCombatants.Of(new Fighter { Id = 1 }, _monsters).MaxSummons);
    }
}
