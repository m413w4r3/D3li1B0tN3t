using D3B.Data.Spells;

namespace D3B.Data.Tests.Spells;

public sealed class SpellStateCatalogTests
{
    [Fact]
    public void AStateSaysWhatItDoesAndHoldsItsOwnEffects()
    {
        var catalog = TestTables.States();

        Assert.True(catalog.TryGetState(3, out var unmovable));
        Assert.Equal(SpellStateProperties.CantBeMoved, unmovable.Properties);
        Assert.Equal([3], catalog.EffectsOf(unmovable).ToArray());
        Assert.True(catalog.TryGetState(8, out var other));
        Assert.True(catalog.EffectsOf(other).IsEmpty);
        Assert.Equal(1002, other.SpellLevelId);
        Assert.False(catalog.TryGetState(9, out _));
    }

    [Fact]
    public void EffectsThatDoNotFollowTheirStatesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new SpellStateCatalog(TestTables.States().States, ReadOnlyMemory<int>.Empty));
    }
}
