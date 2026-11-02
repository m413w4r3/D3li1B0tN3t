using D3B.Data.Effects;

namespace D3B.Data.Tests.Effects;

public sealed class EffectCatalogTests
{
    [Fact]
    public void AnEffectIsFoundByTheIdentifierASpellAndTheServerNameItBy()
    {
        var catalog = TestTables.Effects();

        Assert.True(catalog.TryGetEffect(96, out var water));
        Assert.Equal((9001, 3), (water.DescriptionId, (int)water.ElementId));
        Assert.True(water.Properties.HasFlag(EffectProperties.UseDice));
        Assert.False(catalog.TryGetEffect(97, out _));
    }

    [Fact]
    public void EffectsOutOfOrderAreRejected()
    {
        var effects = TestTables.Effects().Effects.ToArray().Reverse().ToArray();

        Assert.Throws<ArgumentException>(() => new EffectCatalog(effects));
    }
}
