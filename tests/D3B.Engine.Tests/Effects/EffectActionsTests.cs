using D3B.Data.Effects;
using D3B.Engine.Effects;

namespace D3B.Engine.Tests.Effects;

public sealed class EffectActionsTests
{
    private static readonly EffectActions _actions = new(new EffectCatalog(new Effect[]
    {
        new(1, 0, 1, '+', 0, -1, 1, 0, 0, 168, EffectProperties.Boost),
        new(96, 0, 0, '\0', 2, 3, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(99, 0, 0, '\0', 2, 2, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(127, 0, 23, '-', 2, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(281, 0, 0, '+', 3, -1, 1, 0, 0, 0, EffectProperties.Boost),
        new(950, 0, 71, '/', 0, -1, 0, 0, 0, 0, EffectProperties.Boost),
        new(2822, 0, 0, '\0', 2, -1, 0, 0, 0, 0, EffectProperties.UseInFight),
        new(4000, 0, 0, '\0', 0, -1, 0, 0, 0, 0, EffectProperties.None),
    }));

    [Fact]
    public void DamageTakesThePacksElementAndTheBestElementD3BsOwn()
    {
        Assert.Equal(new EffectAction(EffectKind.Damage, EffectElement.Fire, 0), _actions.Of(99));
        Assert.Equal(new EffectAction(EffectKind.Damage, EffectElement.Water, 0), _actions.Of(96));
        Assert.Equal(new EffectAction(EffectKind.Damage, EffectElement.Best, 0), _actions.Of(2822));
    }

    [Fact]
    public void ACharacteristicsEffectIsABoostOrADeboostByItsOperator()
    {
        Assert.Equal(new EffectAction(EffectKind.Boost, EffectElement.None, 1), _actions.Of(1));
        Assert.Equal(new EffectAction(EffectKind.Deboost, EffectElement.None, 23), _actions.Of(127));
    }

    [Fact]
    public void SpellModifiersAreTheirCategoryAndAStateItsOwnKind()
    {
        Assert.Equal(EffectKind.SpellModifier, _actions.Of(281).Kind);
        Assert.Equal(EffectKind.StateSet, _actions.Of(950).Kind);
    }

    [Fact]
    public void AnEffectTheEngineDoesNotForeseeIsOther()
    {
        Assert.Equal(new EffectAction(EffectKind.Other, EffectElement.None, 0), _actions.Of(4000));
        Assert.Equal(EffectKind.Other, _actions.Of(5000).Kind);
    }
}
