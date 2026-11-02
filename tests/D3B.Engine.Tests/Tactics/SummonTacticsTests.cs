using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>Summons as the AI imagines them: brought with their characteristics, playing right after their summoner, and judged by what they strike (9f2).</summary>
public sealed class SummonTacticsTests
{
    private const int Summon = 10;
    private const int Bite = 20;
    private const int Monster = 900;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(181, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), Monsters());

    /// <summary>Spell 10, a summon of monster 900 at grade 1, 2 AP, range 1 to 2 on a free cell; spell 20, its earth bite of 25 to 30 on an enemy, 3 AP, range 1.</summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "a", "I"];
        var point = Zone(SpellZoneShape.Point);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1010, SpellId = Summon, Grade = 1, ApCost = 2, MinRange = 1, Range = 2, StatesCriterion = SpellLevel.NoCriterion, Properties = SpellLevelProperties.NeedFreeCell, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1020, SpellId = Bite, Grade = 1, ApCost = 3, MinRange = 1, Range = 1, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 181, TargetMask = 1, Triggers = 2, DiceNum = Monster, DiceSide = 1, Zone = point },
            new SpellEffect { Uid = 2, EffectId = 97, TargetMask = 0, Triggers = 2, DiceNum = 25, DiceSide = 30, Zone = point },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    /// <summary>Monster 900, which plays and moves, bites at grade 1, with 3 AP, 3 MP and half its summoner's base life.</summary>
    private static MonsterCatalog Monsters()
    {
        var grade = new MonsterGrade
        {
            Grade = 1,
            Level = 1,
            Characteristics = new MonsterCharacteristics { ActionPoints = 3, MovementPoints = 3 },
            Bonus = new MonsterCharacteristics { LifePoints = 50 },
        };
        var monster = new Monster { Id = Monster, Properties = MonsterProperties.CanPlay | MonsterProperties.CanMove | MonsterProperties.UseSummonSlot, FirstGrade = 0, GradeCount = 1, FirstSpell = 0, SpellCount = 1 };

        return new MonsterCatalog(new[] { monster }, new[] { grade }, new[] { new MonsterSpell(Bite, 0, 1) }, new byte[] { 1 }, Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>());
    }

    private static Combatant Summoner()
    {
        return Caster() with { Characteristics = ImmutableSortedDictionary<int, int>.Empty.Add(0, 300), Level = 50 };
    }

    private static Combatant Summoned(int cell)
    {
        return new Combatant { Id = -5, Team = 0, Cell = cell, Life = 150, MaxLife = 150, ActionPoints = 3, MovementPoints = 3, MonsterId = Monster, Grade = 1, SummonerId = 1, MonsterProperties = MonsterProperties.CanPlay | MonsterProperties.CanMove | MonsterProperties.UseSummonSlot };
    }

    private static FightKnowledge Knowledge(params Combatant[] enemies)
    {
        var spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(1, [Level(_spells, Summon)]);

        foreach (var enemy in enemies)
            spells = spells.Add(enemy.Id, []);

        return new FightKnowledge { Spells = spells };
    }

    [Fact]
    public void ASummonComesWithItsShareOfItsSummonersLifeAndItsGradesPoints()
    {
        var fight = new ImaginedFight { Map = Open(), Fighters = [Summoner(), Enemy(-1, Off(8, 0))], ActorId = 1, Round = 1 };

        var cast = _tactics.Imagination.Cast(fight, Level(_spells, Summon), Off(1, 0));
        var summon = Assert.Single(cast.Fighters, fighter => fighter.IsSummon);

        Assert.Equal((Off(1, 0), 150, 3, 3), (summon.Cell, summon.MaxLife, summon.ActionPoints, summon.MovementPoints));
        Assert.Equal((1L, 0, Monster, 1), (summon.SummonerId!.Value, summon.Team, summon.MonsterId!.Value, summon.Grade!.Value));
    }

    [Fact]
    public void ASummonPlaysRightAfterItsSummonerBeforeTheEnemies()
    {
        // Measured (9f2): 78 of 103 turns' orders put a summon right after its summoner, the others after its later summons.
        var enemy = Enemy(-1, Off(4, 0)) with { Life = 20 };
        var fight = new ImaginedFight { Map = Open(), Fighters = [Summoner(), Summoned(Off(2, 0)), enemy], ActorId = 1, Round = 1 };
        var knowledge = Knowledge(enemy);

        var replied = _tactics.Replies(fight, knowledge, _tactics.JudgeOf(fight, knowledge, TacticWeights.Default));

        Assert.False(replied.Find(enemy.Id)!.IsAlive);
        Assert.Equal(1, replied.ActorId);
    }

    [Fact]
    public void TheJudgeWeighsTheBlowASummonStrikesNextAsItsSummonersTurnEnds()
    {
        var enemy = Enemy(-1, Off(10, 0));
        var start = new ImaginedFight { Map = Open(), Fighters = [Summoner(), enemy], ActorId = 1, Round = 1 };
        var judge = _tactics.JudgeOf(start, Knowledge(enemy), TacticWeights.Default);

        var near = _tactics.Imagination.Cast(start with { Fighters = [Summoner() with { Cell = Off(8, 0) }, enemy] }, Level(_spells, Summon), Off(9, 0));
        var away = near.With(near.Fighters.Single(fighter => fighter.IsSummon) with { Cell = Off(-6, 0) });

        Assert.True(judge.Judge(near) > judge.Judge(away));
    }
}
