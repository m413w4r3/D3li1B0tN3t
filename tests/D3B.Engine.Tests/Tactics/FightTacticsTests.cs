using System.Collections.Immutable;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Maps;
using D3B.Engine.Tactics;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Tactics;

/// <summary>The fight's AI on fights the tests build: what it casts, where it walks, when it ends its turn, where it stands as the fight starts.</summary>
public sealed class FightTacticsTests
{
    private const int Blow = 100;
    private const int Heal = 200;
    private const int Bite = 300;
    private const int Shot = 400;
    private const int Critical = 500;
    private const int Rage = 600;
    private const int RageEffect = 9_107;

    private static readonly SpellCatalog _spells = Catalog();
    private static readonly FightTactics _tactics = new(_spells, States(), new EffectCatalog(new Effect[]
    {
        new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(108, 0, 0, '\0', 2, 2, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(RageEffect, 0, FightCharacteristic.DealtDamageMultiplier, '+', 0, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
    }), new MonsterCatalog(Array.Empty<Monster>(), Array.Empty<MonsterGrade>(), Array.Empty<MonsterSpell>(), Array.Empty<byte>(), Array.Empty<MonsterRatio>(), Array.Empty<MonsterRace>(), Array.Empty<MonsterRace>()));

    /// <summary>
    /// Spell 100, an earth blow of 10 to 12 on an enemy, 3 AP, range 1 to 4; spell 200, a fire heal of 30 on the caster,
    /// 2 AP, on its own cell; spell 300, a monster's earth bite of 25 to 30 on an enemy, 3 AP, range 1; spell 400, a monster's
    /// earth shot of 25 to 30, 3 AP, range 1 to 6, in sight; spell 500, an earth blow of 10, 30 on a critical hit, which half its
    /// casts are, 3 AP, range 1 to 4; spell 600, a rage of the caster's for 3 rounds, its damage dealt half as much again, 1 AP.
    /// </summary>
    private static SpellCatalog Catalog()
    {
        string[] strings = ["A", "I", "a"];
        var point = Zone(SpellZoneShape.Point);
        SpellLevel[] levels =
        [
            new SpellLevel { Id = 1100, SpellId = Blow, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 1200, SpellId = Heal, Grade = 1, ApCost = 2, MinRange = 0, Range = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
            new SpellLevel { Id = 1300, SpellId = Bite, Grade = 1, ApCost = 3, MinRange = 1, Range = 1, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 2, EffectCount = 1 },
            new SpellLevel { Id = 1400, SpellId = Shot, Grade = 1, ApCost = 3, MinRange = 1, Range = 6, StatesCriterion = SpellLevel.NoCriterion, Properties = SpellLevelProperties.CastTestLos, FirstEffect = 3, EffectCount = 1 },
            new SpellLevel { Id = 1500, SpellId = Critical, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, CriticalHitProbability = 50, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 4, EffectCount = 1, CriticalEffectCount = 1 },
            new SpellLevel { Id = 1600, SpellId = Rage, Grade = 1, ApCost = 1, MinRange = 0, Range = 0, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 6, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 10, DiceSide = 12, Zone = point },
            new SpellEffect { Uid = 2, EffectId = 108, TargetMask = 2, Triggers = 1, DiceNum = 30, DiceSide = 30, Zone = point },
            new SpellEffect { Uid = 3, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 25, DiceSide = 30, Zone = point },
            new SpellEffect { Uid = 4, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 25, DiceSide = 30, Zone = point },
            new SpellEffect { Uid = 5, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 10, DiceSide = 10, Zone = point },
            new SpellEffect { Uid = 6, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 30, DiceSide = 30, Zone = point },
            new SpellEffect { Uid = 7, EffectId = RageEffect, TargetMask = 2, Triggers = 1, DiceNum = 50, DiceSide = 50, Duration = 3, Zone = point },
        ];
        Spell[] spells = [.. levels.Select((level, index) => new Spell { Id = level.SpellId, NameId = level.SpellId, FirstLevel = index, LevelCount = 1 })];

        return new SpellCatalog(spells, levels, levels.Select(level => level.Id).ToArray(), effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings,
            Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static Combatant Biter(long id, int cell, int life = 50)
    {
        return Enemy(id, cell) with { Life = life, ActionPoints = 3, MovementPoints = 3 };
    }

    private static (ImaginedFight Fight, FightKnowledge Knowledge) Fight(Combatant actor, params Combatant[] enemies)
    {
        var fight = new ImaginedFight { Map = Open(), Fighters = [actor, .. enemies], ActorId = actor.Id, Round = 1 };
        var spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(actor.Id, [Level(_spells, Blow), Level(_spells, Heal)]);

        foreach (var enemy in enemies)
            spells = spells.Add(enemy.Id, [Level(_spells, Bite)]);

        return (fight, new FightKnowledge { Spells = spells });
    }

    private static TurnPlan Plan(ImaginedFight fight, FightKnowledge knowledge)
    {
        return _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default with { Budget = TimeSpan.FromSeconds(30) });
    }

    [Fact]
    public void AnEnemyThatOneBlowKillsIsKilledRatherThanSpared()
    {
        // Measured live (D203): next turn's damage once outweighed a kill now, and the AI spared a fighter at 14 life.
        var (fight, knowledge) = Fight(Caster(), Biter(-1, Off(2, 0), life: 9));

        var plan = Plan(fight, knowledge);

        Assert.Contains(TacticAction.Cast(Blow, 1100, Off(2, 0)), plan.Actions);
        Assert.True(plan.Score > plan.Standing);
    }

    [Fact]
    public void AnEnemyOutOfRangeIsWalkedTowardsThenStruck()
    {
        var (fight, knowledge) = Fight(Caster(), Biter(-1, Off(6, 0)));

        var plan = Plan(fight, knowledge);
        var first = plan.Actions[0];

        Assert.Equal(TacticActionKind.Walk, first.Kind);
        Assert.True(MapGeometry.Distance(first.Cell, Off(6, 0)) <= 4);
        Assert.Equal(TacticActionKind.Cast, plan.Actions[1].Kind);
        Assert.Equal(Blow, plan.Actions[1].SpellId);
    }

    [Fact]
    public void ABadlyHurtCharacterHealsItself()
    {
        var (fight, knowledge) = Fight(Caster() with { Life = 20, MovementPoints = 0 }, Biter(-1, Off(12, 0)));

        var plan = Plan(fight, knowledge);

        Assert.Equal(TacticAction.Cast(Heal, 1200, Middle), plan.Actions[0]);
    }

    [Fact]
    public void ACharacterWithNothingWorthDoingEndsItsTurnAtOnce()
    {
        var (fight, knowledge) = Fight(Caster() with { ActionPoints = 1, MovementPoints = 0 }, Biter(-1, Off(8, 0)));

        Assert.True(Plan(fight, knowledge).IsEmpty);
    }

    [Fact]
    public void ACastTheServerRefusedIsNotWeighedAgainThatTurn()
    {
        var (fight, knowledge) = Fight(Caster() with { MovementPoints = 0 }, Biter(-1, Off(2, 0)));

        var plan = _tactics.Plan(fight, knowledge, TacticWeights.Default, TurnSearchLimits.Default, new HashSet<(int, int)> { (Blow, Off(2, 0)) });

        Assert.DoesNotContain(plan.Actions, action => action.SpellId is Blow);
    }

    [Fact]
    public void ACharacterIsPlacedWhereItReachesAnEnemyItBeats()
    {
        var near = Off(-3, 0);
        var far = Off(-8, 0);
        var (fight, knowledge) = Fight(Caster(Off(-9, 0)), Biter(-1, Off(2, 0), life: 20));

        Assert.Equal(near, _tactics.Place(fight, [far, near], knowledge, TacticWeights.Default));
    }

    [Fact]
    public void OverTheFightEachEnemyStrikesOnlyWhileItLives()
    {
        // Six weak biters: every one striking every turn takes the character's life before it beats them; over the fight,
        // each dying in turn, the character outlasts them (D249).
        var biters = Enumerable.Range(1, 6).Select(index => Biter(-index, Off(3 + index, 0), life: 20)).ToArray();
        var (fight, knowledge) = Fight(Caster() with { Life = 600, MaxLife = 600 }, biters);
        var still = _tactics.Odds(fight, knowledge);
        var over = _tactics.OddsOverTheFight(fight, knowledge);

        Assert.Equal(still.TurnsToWin, over.TurnsToWin, 6);
        Assert.True(over.TurnsToLose > still.TurnsToLose);
        Assert.True(still.IsRisky);
        Assert.False(over.IsRisky);
    }

    [Fact]
    public void EnemiesThatHitHarderThanTheCharacterMakeTheFightRisky()
    {
        var (weak, weakKnowledge) = Fight(Caster(), Biter(-1, Off(4, 0), life: 20));
        var (strong, strongKnowledge) = Fight(Caster() with { Life = 40, MaxLife = 40 }, Biter(-1, Off(4, 0), life: 400), Biter(-2, Off(5, 0), life: 400));

        Assert.False(_tactics.Odds(weak, weakKnowledge).IsRisky);
        Assert.True(_tactics.Odds(strong, strongKnowledge).IsRisky);
    }

    [Fact]
    public void AWalkKeepsThePathTheReachableCellsKeep()
    {
        var (fight, _) = Fight(Caster(), Biter(-1, Off(6, 0)));

        var path = _tactics.PathTo(fight, Off(3, 0));

        Assert.Equal([Middle, Off(1, 0), Off(2, 0), Off(3, 0)], path);
        Assert.Empty(_tactics.PathTo(fight, Off(4, 0)));
    }

    [Fact]
    public void ACriticalHitCountsTheShareOfTheCastsItsChanceGivesIt()
    {
        var (fight, _) = Fight(Caster(), Biter(-1, Off(2, 0)));

        var cast = _tactics.Imagination.Cast(fight, Level(_spells, Critical), Off(2, 0));

        // Half the casts deal 10, half 30 (D207).
        Assert.Equal(30, cast.Find(-1)?.Life);
        Assert.Equal(0.5, EffectFormulas.CriticalChance(Level(_spells, Critical), Caster()));
        Assert.Equal(0.6, EffectFormulas.CriticalChance(Level(_spells, Critical), Caster() with { Characteristics = Caster().Characteristics.SetItem(FightCharacteristic.CriticalHits, 10) }));
        Assert.Equal(0, EffectFormulas.CriticalChance(Level(_spells, Critical), Caster() with { Characteristics = Caster().Characteristics.SetItem(FightCharacteristic.CriticalHits, -60) }));
        Assert.Equal(0, EffectFormulas.CriticalChance(Level(_spells, Blow), Caster()));
    }

    [Fact]
    public void AnEnemyWhoseSpellNeedsItsSightThreatensNothingAPillarHides()
    {
        var shooter = Biter(-1, Off(4, 0)) with { MovementPoints = 0 };
        var open = new ImaginedFight { Map = Open(), Fighters = [Caster(), shooter], ActorId = Caster().Id, Round = 1 };
        var hidden = open with { Map = WithPillars(Off(2, 0)) };
        var knowledge = new FightKnowledge
        {
            Spells = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty.Add(Caster().Id, [Level(_spells, Blow)]).Add(shooter.Id, [Level(_spells, Shot)]),
        };

        // Measured on the captures (D208): reckoning the reach alone, the threat foresaw half as much again as the enemies took.
        Assert.Equal(27.5, _tactics.JudgeOf(open, knowledge, TacticWeights.Default).Threat(open, Caster().Id));
        Assert.Equal(0, _tactics.JudgeOf(hidden, knowledge, TacticWeights.Default).Threat(hidden, Caster().Id));
    }

    [Fact]
    public void ABoostThatLastsIsCastInALongFightAndNotInAShortOne()
    {
        // 6 AP: two blows now, or a rage and one blow, the rage lasting the next two turns (D209).
        var actor = Caster() with { ActionPoints = 6, MovementPoints = 0 };
        var (longer, knowledge) = Fight(actor, Biter(-1, Off(2, 0), life: 400) with { MaxLife = 400 });
        var (shorter, _) = Fight(actor, Biter(-1, Off(2, 0), life: 20) with { MaxLife = 400 });
        var raging = knowledge with { Spells = knowledge.Spells.SetItem(actor.Id, [Level(_spells, Blow), Level(_spells, Rage)]) };

        Assert.Contains(Plan(longer, raging).Actions, action => action.SpellId is Rage);
        Assert.DoesNotContain(Plan(shorter, raging).Actions, action => action.SpellId is Rage);
    }

    [Fact]
    public void TheNextTurnStartsWithTheTurnsPointsAndCastsWhatRaisesTheWorthMost()
    {
        // The turn spent: no AP nor MP left, the enemy in reach of a blow from where the character stands (D210).
        var (fight, knowledge) = Fight(Caster() with { ActionPoints = 0, MovementPoints = 0 }, Biter(-1, Off(2, 0), life: 50));
        var turns = knowledge with { TurnPoints = knowledge.TurnPoints.Add(Caster().Id, (6, 3)) };
        var judge = _tactics.JudgeOf(fight, turns, TacticWeights.Default);

        var start = judge.NextTurnStart(fight);
        var (next, gain) = _tactics.NextTurn(fight, turns, judge, TurnSearchLimits.Default);

        Assert.Equal((6, 3, 2), (start.Actor.ActionPoints, start.Actor.MovementPoints, start.Round));
        Assert.True(gain > 0);
        Assert.True(next.Find(-1)?.Life < 50);
    }
}
