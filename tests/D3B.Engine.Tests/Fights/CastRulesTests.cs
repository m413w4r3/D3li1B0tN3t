using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Criteria;
using D3B.Engine.Fights;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Fights;

public sealed class CastRulesTests
{
    private static readonly SpellCatalog _spells = Spells();
    private static readonly CastRules _rules = new(_spells, States());
    private static readonly Combatant _caster = Caster();
    private static readonly Combatant _enemy = Enemy(-1, Off(2, 0));

    [Fact]
    public void ACastWithinRangeInSightIsAllowed()
    {
        Assert.Equal(CastRefusal.None, Check(10, Off(2, 0)));
        Assert.Equal(CastRefusal.None, Check(10, Off(3, 0), fighters: [_caster]));
    }

    [Fact]
    public void TheCasterMustLiveHaveTheApAndNoStateThatPreventsCasting()
    {
        Assert.Equal(CastRefusal.CasterAbsent, Check(10, Off(2, 0), caster: _caster with { IsAlive = false }));
        Assert.Equal(CastRefusal.CasterCannotCast, Check(10, Off(2, 0), caster: _caster with { States = [9] }));
        Assert.Equal(CastRefusal.NotEnoughActionPoints, Check(10, Off(2, 0), caster: _caster with { ActionPoints = 2 }));
    }

    [Fact]
    public void AStateThatPreventsCastingLetsThroughTheLevelsThatAskForIt()
    {
        var carrier = _caster with { States = [9] };

        Assert.Equal(CastRefusal.CasterCannotCast, Check(10, Off(2, 0), caster: carrier));
        Assert.Equal(CastRefusal.None, Check(70, Off(1, 0), caster: carrier));
        Assert.Equal(CastRefusal.StatesForbid, Check(70, Off(1, 0)));
    }

    [Fact]
    public void TheStatesLetACasterCastALevelWhenItsCriterionHoldsAndNoStatePreventsIt()
    {
        Assert.False(_rules.StatesAllow(Level(_spells, 30), _caster));
        Assert.True(_rules.StatesAllow(Level(_spells, 30), _caster with { States = [5] }));
        Assert.False(_rules.StatesAllow(Level(_spells, 10), _caster with { States = [9] }));
        Assert.True(_rules.StatesAllow(Level(_spells, 70), _caster with { States = [9] }));
        Assert.True(_rules.StatesAllow(Level(_spells, 10), _caster with { ActionPoints = 0 }));
    }

    [Fact]
    public void ModifiersChangeTheLevelOfTheirSpellAlone()
    {
        var level = Level(_spells, 10);
        var modified = CastRules.Modified(level, _caster with
        {
            Modifiers =
            [
                new(SpellModifierKind.Range, 10, 2),
                new(SpellModifierKind.ApCost, 10, 5),
                new(SpellModifierKind.LineOfSightDisabled, 10, 0),
                new(SpellModifierKind.CastsPerTurn, 10, 1),
                new(SpellModifierKind.IntervalSet, 10, 4),
                new(SpellModifierKind.FreeCellEnabled, 10, 0),
                new(SpellModifierKind.Range, 20, 9),
            ],
        });

        Assert.Equal(5, modified.Range);
        Assert.Equal(0, modified.ApCost);
        Assert.Equal(SpellLevelProperties.NeedFreeCell, modified.Properties);
        Assert.Equal(3, modified.MaxCastPerTurn);
        Assert.Equal(1, modified.MaxCastPerTarget);
        Assert.Equal(4, modified.MinCastInterval);
        Assert.Equal(level, CastRules.Modified(level, _caster));
    }

    [Fact]
    public void ARangeSetByAModifierOutweighsTheSums()
    {
        var modified = CastRules.Modified(Level(_spells, 10), _caster with
        {
            Modifiers = [new(SpellModifierKind.Range, 10, 2), new(SpellModifierKind.RangeSet, 10, 7), new(SpellModifierKind.MinRangeSet, 10, 2)],
        });

        Assert.Equal((2, 7), (modified.MinRange, modified.Range));
    }

    [Fact]
    public void TheChecksReadTheLevelAsModified()
    {
        var farther = _caster with { Modifiers = [new(SpellModifierKind.Range, 10, 2)] };
        var blind = _caster with { Modifiers = [new(SpellModifierKind.LineOfSightDisabled, 10, 0)] };
        var free = _caster with { Modifiers = [new(SpellModifierKind.FreeCellEnabled, 50, 0), new(SpellModifierKind.TakenCellDisabled, 50, 0)] };

        Assert.Equal(CastRefusal.None, Check(10, Off(5, 0), caster: farther, fighters: [farther]));
        Assert.Equal(CastRefusal.None, Check(10, Off(3, 0), caster: blind));
        Assert.Equal(CastRefusal.CellNotFree, Check(50, Off(2, 0), caster: free));
        Assert.Equal(CastRefusal.TargetNotVisible, Check(50, Off(1, 0), caster: free));
    }

    [Fact]
    public void TheLevelsCriterionAsksTheCastersStates()
    {
        Assert.Equal(CastRefusal.StatesForbid, Check(30, Off(1, 0), round: 2));
        Assert.Equal(CastRefusal.None, Check(30, Off(1, 0), round: 2, caster: _caster with { States = [5] }));
    }

    [Fact]
    public void CooldownsAndCastsPerTurnCountTheCastersHistory()
    {
        var stated = _caster with { States = [5] };

        Assert.Equal(CastRefusal.InitialCooldown, Check(30, Off(1, 0), round: 1, caster: stated));
        Assert.Equal(CastRefusal.Interval, Check(30, Off(1, 0), round: 3, caster: stated, history: CastHistory.Empty.Cast(30, null, 2)));
        Assert.Equal(CastRefusal.None, Check(30, Off(1, 0), round: 4, caster: stated, history: CastHistory.Empty.Cast(30, null, 2)));

        var twice = CastHistory.Empty.Cast(10, -5, 1).Cast(10, -6, 1);

        Assert.Equal(CastRefusal.TooManyCasts, Check(10, Off(2, 0), history: twice));
        Assert.Equal(CastRefusal.TooManyCastsOnTarget, Check(10, Off(2, 0), history: CastHistory.Empty.Cast(10, -1, 1)));
        Assert.Equal(CastRefusal.None, Check(10, Off(2, 0), round: 2, history: twice));
    }

    [Fact]
    public void TheCellMustBeWalkableFreeOrTakenAsTheLevelAsks()
    {
        Assert.Equal(CastRefusal.CellNotWalkable, Check(20, Off(1, 0), map: Open(Off(1, 0))));
        Assert.Equal(CastRefusal.CellNotFree, Check(20, Off(2, 0)));
        Assert.Equal(CastRefusal.None, Check(20, Off(1, 0)));
        Assert.Equal(CastRefusal.CellNotTaken, Check(50, Off(1, 0)));
        Assert.Equal(CastRefusal.TargetNotVisible, Check(50, Off(2, 0), fighters: [_caster, _enemy with { IsVisible = false }]));
        Assert.Equal(CastRefusal.None, Check(50, Off(2, 0)));
    }

    [Fact]
    public void TheRangeTheCastersBonusAndTheLineOfSightDecideLast()
    {
        Assert.Equal(CastRefusal.OutOfRange, Check(10, Off(4, 0)));
        Assert.Equal(CastRefusal.OutOfRange, Check(20, Off(6, 0)));
        Assert.Equal(CastRefusal.None, Check(20, Off(6, 0), caster: _caster with { RangeBonus = 1 }));
        Assert.Equal(CastRefusal.OutOfRange, Check(60, Off(2, 1)));
        Assert.Equal(CastRefusal.NoLineOfSight, Check(10, Off(3, 0)));
        Assert.Equal(CastRefusal.NoLineOfSight, Check(10, Off(3, 0), map: WithPillars(Off(2, 0)), fighters: [_caster]));
    }

    [Fact]
    public void ASummonNeedsAFreeSlot()
    {
        var full = _caster with { MaxSummons = 1 };
        var summon = new Combatant { Id = -9, Team = 0, Cell = Off(-2, 0), SummonerId = 1, MonsterId = 31, MonsterProperties = MonsterProperties.UseSummonSlot };

        Assert.Equal(CastRefusal.None, Check(40, Off(1, 0), caster: full, fighters: [full]));
        Assert.Equal(CastRefusal.TooManySummons, Check(40, Off(1, 0), caster: full, fighters: [full, summon]));
        Assert.Equal(CastRefusal.None, Check(40, Off(1, 0), caster: full, fighters: [full, summon with { MonsterProperties = default }]));
    }

    [Fact]
    public void TheCellsAFighterMayCastOnAreThoseTheChecksAllow()
    {
        var targets = _rules.Targets(Open(), [_caster, _enemy], _caster, Level(_spells, 10), CastHistory.Empty, 1);

        Assert.True(targets.Contains(Off(2, 0)));
        Assert.False(targets.Contains(Off(3, 0)));
        Assert.Equal(4 + 8 + 12 - 1, targets.Count);
    }

    [Fact]
    public void AnEffectReachesTheFightersInItsZoneItsMaskSelects()
    {
        var effect = _spells.EffectsOf(Level(_spells, 10))[0] with { Zone = Zone(SpellZoneShape.Circle, 1) };
        var near = Enemy(-2, Off(2, 1));
        var far = Enemy(-3, Off(4, 0));

        var reached = _rules.Reached(Open(), [_caster, _enemy, near, far], _caster, effect, Off(2, 0));

        Assert.Equal([_enemy, near], reached.Select(pair => pair.Fighter));
        Assert.All(reached, pair => Assert.Equal(CriterionAnswer.True, pair.Selected));
    }

    [Fact]
    public void ACasterOnNoCellReachesFromTheCellCastOn()
    {
        var effect = _spells.EffectsOf(Level(_spells, 10))[0] with { Zone = Zone(SpellZoneShape.Circle, 1) };
        var carried = _caster with { Cell = Combatant.NoCell };

        Assert.Equal([_enemy], _rules.Reached(Open(), [carried, _enemy], carried, effect, Off(2, 0)).Select(pair => pair.Fighter));
    }

    private static CastRefusal Check(int spell, int cell, int round = 1, Combatant? caster = null, CastHistory? history = null, FightMap? map = null, Combatant[]? fighters = null)
    {
        var who = caster ?? _caster;

        return _rules.Check(map ?? Open(), fighters ?? [who, _enemy], who, Level(_spells, spell), cell, history ?? CastHistory.Empty, round);
    }
}
