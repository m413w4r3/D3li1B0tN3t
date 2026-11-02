using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A Zobal's masks as the pack makes them, the spells each lets it cast, and the AI choosing one (D224, D227, D228).</summary>
public sealed class MaskTacticsTests
{
    private const int Brave = 10;
    private const int Psychopath = 20;
    private const int TakeOff = 30;
    private const int Dispel = 40;
    private const int Furia = 50;
    private const int Shield = 60;
    private const int BraveBlow = 70;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(111, 0, 1, '+', 0, -1, 0, 0, 0, 0, EffectProperties.Boost),
        new(406, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(792, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(950, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(951, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1040, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(2794, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 10, the brave mask, and 20, the psychopath's, 1 AP on the caster's cell, of 1 stack: each casts 30, which casts
    /// its own grade 2, taking off the masks' states 98 and 99 the caster is in; then puts its state on, casts 40, dispelling
    /// the buffs of the mask whose state the caster is no longer in; the brave mask gives 1 AP for the whole fight. Spell 50,
    /// Furia, an earth blow of 30 at range 1 asking state 99; 70 a blow of 5 asking state 98; 60, a shield of 300 at range 4
    /// to 10, of mask <c>l,m,c</c>.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["C", "I", "C,E98", "C,E99", "C,*e98", "C,*e99", "A", "HS=99", "HS=98", "l,m,c"];
        var point = Zone(SpellZoneShape.Point);

        SpellLevel Level(int id, int spell, int grade, int cost, int least, int most, int first, int count, int criterion = SpellLevel.NoCriterion)
        {
            return new SpellLevel
            {
                Id = id,
                SpellId = spell,
                Grade = (byte)grade,
                ApCost = (short)cost,
                MinRange = (byte)least,
                Range = (byte)most,
                MaxStack = 1,
                StatesCriterion = criterion,
                FirstEffect = first,
                EffectCount = (ushort)count,
            };
        }

        SpellEffect Effect(int effectId, int mask, int dice = 0, int side = 0, int value = 0, int duration = 0)
        {
            return new SpellEffect { EffectId = (ushort)effectId, TargetMask = mask, Triggers = 1, DiceNum = dice, DiceSide = side, Value = value, Duration = (sbyte)duration, Zone = point };
        }

        SpellLevel[] levels =
        [
            Level(1010, Brave, 1, 1, 0, 0, 0, 4),
            Level(1020, Psychopath, 1, 1, 0, 0, 4, 3),
            Level(1030, TakeOff, 1, 1, 0, 63, 7, 1),
            Level(1031, TakeOff, 2, 1, 0, 63, 8, 2),
            Level(1040, Dispel, 1, 1, 0, 63, 10, 2),
            Level(1050, Furia, 1, 3, 1, 1, 12, 1, criterion: 7),
            Level(1060, Shield, 1, 3, 4, 10, 13, 1),
            Level(1070, BraveBlow, 1, 3, 1, 1, 14, 1, criterion: 8),
        ];
        SpellEffect[] effects =
        [
            Effect(2794, 0, dice: TakeOff, side: 1),
            Effect(950, 0, value: 98, duration: -1),
            Effect(2794, 0, dice: Dispel, side: 1),
            Effect(111, 0, dice: 1, duration: -1),
            Effect(2794, 0, dice: TakeOff, side: 1),
            Effect(950, 0, value: 99, duration: -1),
            Effect(2794, 0, dice: Dispel, side: 1),
            Effect(792, 0, dice: TakeOff, side: 2),
            Effect(951, 2, value: 98),
            Effect(951, 3, value: 99),
            Effect(406, 4, value: Brave),
            Effect(406, 5, value: Psychopath),
            Effect(97, 6, dice: 30, side: 30),
            Effect(1040, 9, dice: 300, side: 300, duration: 3),
            Effect(97, 6, dice: 5, side: 5),
        ];
        Spell[] spells =
        [
            new Spell { Id = Brave, NameId = 1, FirstLevel = 0, LevelCount = 1 },
            new Spell { Id = Psychopath, NameId = 2, FirstLevel = 1, LevelCount = 1 },
            new Spell { Id = TakeOff, NameId = 3, FirstLevel = 2, LevelCount = 2 },
            new Spell { Id = Dispel, NameId = 4, FirstLevel = 4, LevelCount = 1 },
            new Spell { Id = Furia, NameId = 5, FirstLevel = 5, LevelCount = 1 },
            new Spell { Id = Shield, NameId = 6, FirstLevel = 6, LevelCount = 1 },
            new Spell { Id = BraveBlow, NameId = 7, FirstLevel = 7, LevelCount = 1 },
        ];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static ImaginedFight Fight(params Combatant[] fighters)
    {
        return new ImaginedFight { Map = Open(), Fighters = [.. fighters], ActorId = fighters[0].Id, Round = 1 };
    }

    private static TurnPlan Plan(Combatant zobal, Combatant enemy, params int[] spells)
    {
        var knowledge = new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(zobal.Id, [.. spells.Select(spell => Level(_spells, spell))]) };

        return _tactics.Plan(Fight(zobal, enemy), knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });
    }

    [Fact]
    public void AMaskTakesTheOtherMasksStateOffPutsItsOwnOnAndDispelsTheOthersBuffs()
    {
        // The brave mask worn, its state and the AP it gives held as the server gave them.
        var zobal = Caster() with { ActionPoints = 7, States = [98], Buffs = [new HeldBuff(Brave, 950, 98), new HeldBuff(Brave, 111, 1)] };

        var masked = _tactics.Imagination.Cast(Fight(zobal), Level(_spells, Psychopath), zobal.Cell).Actor;

        Assert.Equal([99], masked.States);
        Assert.Equal(5, masked.ActionPoints);
        Assert.Equal([new HeldBuff(Psychopath, 950, 99, CasterId: zobal.Id)], masked.Buffs);
    }

    [Fact]
    public void AMaskCastAgainReplacesItsBoostPastItsLevelsStack()
    {
        var zobal = Caster() with { ActionPoints = 6, States = [98] };

        var once = _tactics.Imagination.Cast(Fight(zobal), Level(_spells, Brave), zobal.Cell);
        var twice = _tactics.Imagination.Cast(once, Level(_spells, Brave), zobal.Cell);

        Assert.Equal(6, once.Actor.ActionPoints);
        Assert.Equal(5, twice.Actor.ActionPoints);
        Assert.Equal([98], twice.Actor.States);
        Assert.Single(twice.Actor.Buffs, held => held.EffectId is 111);
        Assert.Equal(2, twice.Buffs.Length);
    }

    [Fact]
    public void TheAIPutsOnTheMaskWhoseSpellsDealMoreThenCastsThem()
    {
        var zobal = Caster() with { ActionPoints = 4, MovementPoints = 0, States = [98] };
        var enemy = Enemy(-1, Off(1, 0)) with { Life = 500, MaxLife = 500 };

        var casts = Plan(zobal, enemy, Brave, Psychopath, Furia, BraveBlow).Actions.Where(action => action.Kind is TacticActionKind.Cast);

        Assert.Equal([TacticAction.Cast(Psychopath, 1020, zobal.Cell), TacticAction.Cast(Furia, 1050, enemy.Cell)], casts);
    }

    [Fact]
    public void TheSearchKeepsBesideItsBestSequencesTheBestOfEachStatesTheyLeaveOut()
    {
        // The masks put on deal nothing yet and rank after the blows of the mask worn (D255).
        Combatant[] ranked =
        [
            Caster() with { States = [98] },
            Caster() with { States = [98] },
            Caster() with { States = [99] },
            Caster() with { States = [98] },
            Caster() with { States = [100] },
            Caster() with { States = [99] },
        ];

        var kept = FightTactics.WithEachStates(ranked, fighter => fighter, 2);

        Assert.Equal([ranked[0], ranked[1], ranked[2], ranked[4]], kept);
    }

    [Fact]
    public void TheSameStatesInAnotherOrderAreNotKeptAgain()
    {
        Combatant[] ranked = [Caster() with { States = [98, 7] }, Caster() with { States = [7, 98] }, Caster() with { States = [7] }];

        var kept = FightTactics.WithEachStates(ranked, fighter => fighter, 1);

        Assert.Equal([ranked[0], ranked[2]], kept);
    }

    [Fact]
    public void AShieldOfTheCasterInItsZoneOnlyIsNotCastOnAnEnemy()
    {
        var zobal = Caster() with { ActionPoints = 3, MovementPoints = 0 };
        var enemy = Enemy(-1, Off(4, 0)) with { Life = 500, MaxLife = 500 };

        var shielded = _tactics.Imagination.Cast(Fight(zobal, enemy), Level(_spells, Shield), enemy.Cell).Actor;

        Assert.Equal(0, shielded.Characteristic(FightCharacteristic.Shield));
        Assert.DoesNotContain(Plan(zobal, enemy, Shield).Actions, action => action.Kind is TacticActionKind.Cast);
    }
}
