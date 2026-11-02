using D3B.Engine.Criteria;
using D3B.Engine.Fights;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Fights;

public sealed class TargetMaskTests
{
    private static readonly Combatant _caster = Caster();
    private static readonly Combatant _ally = Caster() with { Id = 2, BreedId = 5 };
    private static readonly Combatant _enemy = Enemy(-1, Off(2, 0));
    private static readonly Combatant _summon = Enemy(-2, Off(3, 0)) with { MonsterId = 3958, SummonerId = -1, MonsterProperties = default, Life = 80 };
    private static readonly Combatant[] _fighters = [_caster, _ally, _enemy, _summon];

    [Fact]
    public void LowercaseLettersSelectAlliesAndUppercaseOnesEnemies()
    {
        Assert.Equal([_caster, _ally, _enemy, _summon], Selected("a,A"));
        Assert.Equal([_enemy, _summon], Selected("A"));
        Assert.Equal([_caster, _ally], Selected("a"));
        Assert.Equal([_ally], Selected("g"));
        Assert.Equal([_caster], Selected("C"));
    }

    [Fact]
    public void AnUppercaseCNamesTheCasterWhereverItStandsAndALowercaseOneInTheZoneOnly()
    {
        Assert.True(TargetMask.Parse("C").NamesTheCaster);
        Assert.True(TargetMask.Parse("C,E98").NamesTheCaster);
        Assert.False(TargetMask.Parse("l,m,c").NamesTheCaster);
        Assert.Equal([_caster, _ally], Selected("l,m,c"));
    }

    [Fact]
    public void KindsSelectCharactersMonstersAndSummons()
    {
        Assert.Equal([_ally], Selected("h,H"));
        Assert.Equal([_enemy], Selected("M"));
        Assert.Equal([_summon], Selected("J"));
        Assert.Equal([_summon], Selected("S"));
        Assert.Empty(Selected("I"));
    }

    [Fact]
    public void ConditionsMustAllHoldAndAlternativesOneOfThem()
    {
        var stated = _enemy with { States = [5] };

        Assert.Equal(CriterionAnswer.True, TargetMask.Parse("A,E5").Selects(_caster, stated, _fighters));
        Assert.Equal(CriterionAnswer.False, TargetMask.Parse("A,E5").Selects(_caster, _enemy, _fighters));
        Assert.Equal(CriterionAnswer.False, TargetMask.Parse("A,e5").Selects(_caster, stated, _fighters));
        Assert.Equal([_enemy, _summon], Selected("A,F31,F3958"));
        Assert.Equal([_summon], Selected("A,f31"));
        Assert.Equal([_enemy], Selected("A,V60"));
        Assert.Equal([_enemy, _summon], Selected("A,p"));
        Assert.Equal(CriterionAnswer.True, TargetMask.Parse("a,P").Selects(_caster, _summon with { Team = 0, SummonerId = 1 }, _fighters));
        Assert.Equal(CriterionAnswer.False, TargetMask.Parse("a,P").Selects(_caster, _ally, _fighters));
    }

    [Fact]
    public void ACastersConditionAsksTheCaster()
    {
        Assert.Empty(Selected("A,*E7"));
        Assert.Equal(CriterionAnswer.True, TargetMask.Parse("A,*E7").Selects(_caster with { States = [7] }, _enemy, _fighters));
        Assert.Equal([_enemy, _summon], Selected("A,*h,*m"));
        Assert.Empty(Selected("A,*m"));
    }

    [Fact]
    public void WhatTheFightsStateDoesNotTellIsUnknown()
    {
        Assert.Equal(CriterionAnswer.Unknown, TargetMask.Parse("Sce").Selects(_caster, _enemy, _fighters));
        Assert.Equal(CriterionAnswer.Unknown, TargetMask.Parse("A,K").Selects(_caster, _enemy, _fighters));
        Assert.Equal(CriterionAnswer.False, TargetMask.Parse("A,K").Selects(_caster, _ally, _fighters));
        Assert.Equal(CriterionAnswer.Unknown, TargetMask.Parse("A,q").Selects(_caster, _enemy, _fighters));
        Assert.Equal(CriterionAnswer.True, TargetMask.Parse("a,q").Selects(_caster with { MaxSummons = 1 }, _caster with { MaxSummons = 1 }, _fighters));
    }

    private static Combatant[] Selected(string mask)
    {
        var parsed = TargetMask.Parse(mask);

        return [.. _fighters.Where(fighter => parsed.Selects(_caster, fighter, _fighters) is CriterionAnswer.True)];
    }
}
