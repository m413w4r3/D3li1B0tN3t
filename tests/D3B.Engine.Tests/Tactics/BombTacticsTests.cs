using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>A Rogue's bombs as the AI imagines them: set off with the bombs their chain reaches, their combos summed, their walls, and their slots (9f3).</summary>
public sealed class BombTacticsTests
{
    private const int Bomb = 3112;
    private const int Throw = 10;
    private const int Detonator = 11;
    private const int Push = 12;
    private const int PushToCell = 13;
    private const int Chain = 30;
    private const int Explosion = 31;
    private const int Instant = 32;
    private const int Wall = 33;
    private const int Lighting = 34;
    private const int Bombard = 35;
    private const int Boot = 36;

    private static readonly SpellCatalog _spells = Catalog();

    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(5, 0, 0, '\0', 2, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(99, 0, 0, '\0', 0, 2, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(141, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(296, 0, 0, '\0', 3, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(783, 0, 0, '/', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1008, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1009, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1027, 0, 0, '+', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(1103, 0, 0, '\0', 2, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), Monsters());

    /// <summary>
    /// Spell 10 throws bomb 3112 at grade 1, its instant spell on a taken cell; 11 sets off the bomb cast on; 12 pushes an
    /// enemy 3 cells; 13 pushes the first fighter in line to the cell cast on. The bomb's chain reaction sets off its
    /// summoner's bombs in a circle of 2, its explosion deals 10 fire to enemies in a circle of 2 and kills it, its instant
    /// spell 15, its wall 20, the wall between two of them in a line 1 to 7 cells apart. Its circles leave out their centre, as
    /// the pack's. As it comes, it casts its starting spell, which gives its summoner a buff raising spell 10 by 1 AP, as
    /// a Roublard's Allumage. Spell 35 pushes 1 cell the fighters on a line across the cast, as a Bombarde; 36 pushes 3 cells with no damage, as a Botte.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "a", "I", "C", "a,A", "h,P"];
        var point = Zone(SpellZoneShape.Point);
        var circle = Zone(SpellZoneShape.Circle, 2, 1);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1010, SpellId = Throw, Grade = 1, ApCost = 2, MinRange = 1, Range = 6, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1011, SpellId = Detonator, Grade = 1, ApCost = 1, MinRange = 1, Range = 8, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
            new SpellLevel { Id = 1012, SpellId = Push, Grade = 1, ApCost = 2, MinRange = 1, Range = 3, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 1 },
            new SpellLevel { Id = 1013, SpellId = PushToCell, Grade = 1, ApCost = 2, MinRange = 2, Range = 7, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 3, EffectCount = 1 },
            new SpellLevel { Id = 1030, SpellId = Chain, Grade = 1, ApCost = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 4, EffectCount = 1 },
            new SpellLevel { Id = 1031, SpellId = Explosion, Grade = 1, ApCost = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 5, EffectCount = 2 },
            new SpellLevel { Id = 1032, SpellId = Instant, Grade = 1, ApCost = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 7, EffectCount = 1 },
            new SpellLevel { Id = 1033, SpellId = Wall, Grade = 1, ApCost = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 8, EffectCount = 1 },
            new SpellLevel { Id = 1034, SpellId = Lighting, Grade = 1, ApCost = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 9, EffectCount = 1 },
            new SpellLevel { Id = 1035, SpellId = Bombard, Grade = 1, ApCost = 3, MinRange = 1, Range = 6, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 10, EffectCount = 1 },
            new SpellLevel { Id = 1036, SpellId = Boot, Grade = 1, ApCost = 2, MinRange = 1, Range = 6, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 11, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 1008, TargetMask = 4, Triggers = 2, DiceNum = Bomb, DiceSide = 1, Zone = point },
            new SpellEffect { Uid = 2, EffectId = 1009, TargetMask = 1, Triggers = 2, Zone = point },
            new SpellEffect { Uid = 3, EffectId = 5, TargetMask = 0, Triggers = 2, DiceNum = 3, Zone = point },
            new SpellEffect { Uid = 4, EffectId = 783, TargetMask = 4, Triggers = 2, Zone = point },
            new SpellEffect { Uid = 5, EffectId = 1009, TargetMask = 1, Triggers = 2, Zone = circle },
            new SpellEffect { Uid = 6, EffectId = 99, TargetMask = 0, Triggers = 2, DiceNum = 10, DiceSide = 10, Zone = circle },
            new SpellEffect { Uid = 7, EffectId = 141, TargetMask = 3, Triggers = 2, DiceNum = 1, Zone = point },
            new SpellEffect { Uid = 8, EffectId = 99, TargetMask = 0, Triggers = 2, DiceNum = 15, DiceSide = 15, Zone = point },
            new SpellEffect { Uid = 9, EffectId = 99, TargetMask = 0, Triggers = 2, DiceNum = 20, DiceSide = 20, Zone = point },
            new SpellEffect { Uid = 10, EffectId = 296, TargetMask = 5, Triggers = 2, Duration = -1, DiceNum = Throw, Value = 1, Zone = Zone(SpellZoneShape.WholeMap) },
            new SpellEffect { Uid = 11, EffectId = 5, TargetMask = 0, Triggers = 2, DiceNum = 1, Zone = Zone(SpellZoneShape.PerpendicularLine, 1) },
            new SpellEffect { Uid = 12, EffectId = 1103, TargetMask = 0, Triggers = 2, DiceNum = 3, Zone = Zone(SpellZoneShape.Point) },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), new[] { new SpellBomb(Bomb, Chain, Explosion, 2, Instant, 0) }, new[] { new SpellBombWall(2, Wall, 0xFF0000, true, 1, 7) });
    }

    /// <summary>Bomb 3112, which neither plays nor moves, in a bomb's slot.</summary>
    private static MonsterCatalog Monsters()
    {
        var grade = new MonsterGrade { Grade = 1, Level = 1, StartingSpellLevelId = 1034, Characteristics = new MonsterCharacteristics { Vitality = 10 } };
        var monster = new Monster { Id = Bomb, Properties = MonsterProperties.UseBombSlot | MonsterProperties.CanBePushed, FirstGrade = 0, GradeCount = 1 };

        return new MonsterCatalog(new[] { monster }, new[] { grade }, Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>());
    }

    private static Combatant Rogue(int bombs = 3)
    {
        return Caster(Off(0, 0)) with { Characteristics = ImmutableSortedDictionary<int, int>.Empty.Add(93, bombs) };
    }

    private static Combatant BombAt(long id, int cell, int combo = 0)
    {
        var bomb = new Combatant { Id = id, Team = 0, Cell = cell, Life = 50, MaxLife = 50, MonsterId = Bomb, Grade = 1, SummonerId = 1, MonsterProperties = MonsterProperties.UseBombSlot | MonsterProperties.CanBePushed };

        return combo is 0 ? bomb : bomb with { Buffs = [new HeldBuff(20500, Bombs.Combo, combo)] };
    }

    private static ImaginedFight Fight(params Combatant[] others)
    {
        return new ImaginedFight { Map = Open(), Fighters = [Rogue(), .. others], ActorId = 1, Round = 1 };
    }

    [Fact]
    public void ABombSetOffSetsOffTheBombsItsChainReachesEachRaisedByTheirCombosSummed()
    {
        // Measured (9f3): an explosion's damage raised by the combos of all the bombs set off, 20 and 40 here, 10% less a step
        // farther from the second bomb.
        var enemy = Enemy(-1, Off(3, 1)) with { Life = 100 };
        var fight = Fight(BombAt(-10, Off(3, 0), 20), BombAt(-11, Off(4, 0), 40), BombAt(-12, Off(8, 0), 60), enemy);

        var after = _tactics.Imagination.Cast(fight, Level(_spells, Detonator), Off(3, 0));

        Assert.False(after.Find(-10)!.IsAlive);
        Assert.False(after.Find(-11)!.IsAlive);
        Assert.True(after.Find(-12)!.IsAlive);
        Assert.Equal(100 - 16 - 14, after.Find(enemy.Id)!.Life);
    }

    [Fact]
    public void ABombThatComesMakesAWallWithAnotherOfItsKindWhichStrikesTheEnemyItComesOnto()
    {
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var fight = Fight(BombAt(-10, Off(2, 0)), enemy);

        var after = _tactics.Imagination.Cast(fight, Level(_spells, Throw), Off(4, 0));

        Assert.Equal(80, after.Find(enemy.Id)!.Life);
        Assert.Contains(after.Fighters, fighter => fighter.Cell == Off(4, 0) && fighter.MonsterId == Bomb);
    }

    [Fact]
    public void APushStopsOnAWallWhichStrikesItsTarget()
    {
        // Measured (9f3): the pushes that met a wall stopped on its first cell, its spell cast on the fighter.
        var enemy = Enemy(-1, Off(1, 0)) with { Life = 100 };
        var fight = Fight(BombAt(-10, Off(2, -1)), BombAt(-11, Off(2, 1)), enemy);

        var after = _tactics.Imagination.Cast(fight, Level(_spells, Push), Off(1, 0));

        Assert.Equal((Off(2, 0), 80), (after.Find(enemy.Id)!.Cell, after.Find(enemy.Id)!.Life));
    }

    [Fact]
    public void APushAlongALineAcrossTheCastGoesTheWayItWasCast()
    {
        // Measured (9f4): a Bombarde pushed the fighters on its line from the caster towards the cell cast on, 6 of 6.
        var enemy = Enemy(-1, Off(3, 1)) with { Life = 100 };

        var after = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, Bombard), Off(3, 0));

        Assert.Equal(Off(4, 1), after.Find(enemy.Id)!.Cell);
    }

    [Fact]
    public void AFighterMovedFromAWallOntoAnotherWallCellIsNotStruckAgain()
    {
        // Measured (9f4): a monster a Botte moved from a wall's cell onto another was struck by nothing, 3 times.
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var fight = Fight(BombAt(-10, Off(2, 0)), BombAt(-11, Off(5, 0)), enemy);

        var after = _tactics.Imagination.Cast(fight, Level(_spells, Push), Off(3, 0));

        Assert.Equal((Off(4, 0), 100), (after.Find(enemy.Id)!.Cell, after.Find(enemy.Id)!.Life));
    }

    [Fact]
    public void APushWithoutDamageHurtsNoFighterItStopsShort()
    {
        var enemy = Enemy(-1, Off(1, 0)) with { Life = 100 };
        var blocker = Enemy(-2, Off(3, 0)) with { Life = 100 };

        var after = _tactics.Imagination.Cast(Fight(enemy, blocker), Level(_spells, Boot), Off(1, 0));

        Assert.Equal((Off(2, 0), 100), (after.Find(enemy.Id)!.Cell, after.Find(enemy.Id)!.Life));
    }

    [Fact]
    public void APushMovesNoMonsterThePackKeepsFromBeingPushed()
    {
        // Measured (9f4): the server moved none of the targets the pack or their states keep from being pushed.
        var enemy = Enemy(-1, Off(1, 0)) with { Life = 100, MonsterProperties = MonsterProperties.CanMove };

        var after = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, Push), Off(1, 0));

        Assert.Equal((Off(1, 0), 100), (after.Find(enemy.Id)!.Cell, after.Find(enemy.Id)!.Life));
    }

    [Fact]
    public void APushToACellTakesTheFirstFighterInLineThereThroughAWall()
    {
        var enemy = Enemy(-1, Off(1, 0)) with { Life = 100 };
        var fight = Fight(BombAt(-10, Off(3, -1)), BombAt(-11, Off(3, 1)), enemy);

        var after = _tactics.Imagination.Cast(fight, Level(_spells, PushToCell), Off(5, 0));

        Assert.Equal((Off(3, 0), 80), (after.Find(enemy.Id)!.Cell, after.Find(enemy.Id)!.Life));
    }

    [Fact]
    public void ABombThrownOnAFighterCastsItsInstantSpellThereInstead()
    {
        var enemy = Enemy(-1, Off(2, 0)) with { Life = 100 };

        var after = _tactics.Imagination.Cast(Fight(enemy), Level(_spells, Throw), Off(2, 0));

        Assert.Equal(85, after.Find(enemy.Id)!.Life);
        Assert.DoesNotContain(after.Fighters, fighter => fighter.MonsterId == Bomb);
    }

    [Fact]
    public void ARogueThrowsNoMoreBombsThanItsSlots()
    {
        var fight = Fight(BombAt(-10, Off(2, 2)), BombAt(-11, Off(-2, 2))) with { Fighters = [Rogue(bombs: 2), BombAt(-10, Off(2, 2)), BombAt(-11, Off(-2, 2))] };

        Assert.Equal(CastRefusal.TooManySummons, _tactics.Rules.Check(fight.Map, fight.Fighters, fight.Actor, Level(_spells, Throw), Off(2, 0), fight.History, fight.Round));
        Assert.Equal(CastRefusal.None, _tactics.Rules.Check(fight.Map, [Rogue(bombs: 3), BombAt(-10, Off(2, 2)), BombAt(-11, Off(-2, 2))], Rogue(bombs: 3), Level(_spells, Throw), Off(2, 0), fight.History, fight.Round));
    }

    [Fact]
    public void TheJudgeWeighsABombByTheExplosionItWouldSetOffAmongTheEnemies()
    {
        var enemy = Enemy(-1, Off(6, 0)) with { Life = 100 };
        var start = Fight(enemy);
        var knowledge = new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(1, [Level(_spells, Throw)]).Add(enemy.Id, []) };
        var judge = _tactics.JudgeOf(start, knowledge, TacticWeights.Default);

        var near = _tactics.Imagination.Cast(start, Level(_spells, Throw), Off(5, 0));
        var far = _tactics.Imagination.Cast(start, Level(_spells, Throw), Off(-5, 0));

        Assert.True(judge.Judge(near) > judge.Judge(far));
    }

    [Fact]
    public void ABombRaisesItsKindsSpellByAnAPWhileItLives()
    {
        // Measured (9f4): each bomb's starting spell gave its Roublard a buff of effect 296 on its kind's spell, which the
        // server took back as the bomb died.
        var enemy = Enemy(-1, Off(5, 0)) with { Life = 100 };
        var fight = Fight(enemy);

        var thrown = _tactics.Imagination.Cast(fight, Level(_spells, Throw), Off(4, 0));
        var bomb = thrown.Fighters.Single(fighter => fighter.MonsterId == Bomb);

        Assert.Equal(3, CastRules.Modified(Level(_spells, Throw), thrown.Actor).ApCost);

        var set = _tactics.Imagination.Cast(thrown, Level(_spells, Detonator), bomb.Cell);

        Assert.False(set.Find(bomb.Id)!.IsAlive);
        Assert.Equal(2, CastRules.Modified(Level(_spells, Throw), set.Actor).ApCost);
    }

    [Fact]
    public void AWallStrikesTheEnemyWhoseTurnBeginsOnIt()
    {
        // Measured (9f4): a monster whose turn began on a wall took its spell again, before its turn's own effects.
        var enemy = Enemy(-1, Off(3, 0)) with { Life = 100 };
        var fight = Fight(BombAt(-10, Off(2, 0)), BombAt(-11, Off(4, 0)), enemy);

        Assert.Equal(80, _tactics.Imagination.TurnStarted(fight, enemy.Id, 2).Find(enemy.Id)!.Life);
        Assert.Equal(100, _tactics.Imagination.TurnStarted(fight, 1, 2).Find(enemy.Id)!.Life);
    }

    [Fact]
    public void TheBombsAboutAnEnemyPromiseItsKillOnceSoThatKillingItNowIsWorthMore()
    {
        // Measured (9f4): with two bombs about the last enemy, each worth its kill, keeping them was judged worth more than the
        // Détonateur that won the fight.
        var enemy = Enemy(-1, Off(4, 2)) with { Life = 15 };
        var start = Fight(BombAt(-10, Off(4, 1), 20), BombAt(-11, Off(5, 2), 20), enemy);
        var knowledge = new FightKnowledge { Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(1, [Level(_spells, Detonator)]).Add(enemy.Id, []) };
        var judge = _tactics.JudgeOf(start, knowledge, TacticWeights.Default);

        var set = _tactics.Imagination.Cast(start, Level(_spells, Detonator), Off(4, 1));

        Assert.False(set.Find(enemy.Id)!.IsAlive);
        Assert.True(judge.Judge(set) > judge.Judge(start));
    }
}
