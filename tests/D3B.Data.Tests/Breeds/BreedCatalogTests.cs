using D3B.Data.Breeds;

namespace D3B.Data.Tests.Breeds;

public sealed class BreedCatalogTests
{
    [Fact]
    public void ABreedHoldsItsSpellsRolesAndCosts()
    {
        var catalog = TestTables.Breeds();

        Assert.True(catalog.TryGetBreed(1, out var breed));
        Assert.Equal([100, 200], catalog.SpellsOf(breed).ToArray());
        Assert.Equal([(1, 3)], catalog.RolesOf(breed).ToArray().Select(role => (role.RoleId, role.Value)));
        Assert.Equal([(10, 0, 1), (10, 100, 2)], catalog.StatCostsOf(breed).ToArray().Select(cost => (cost.CharacteristicId, cost.Threshold, cost.Cost)));
        Assert.False(catalog.TryGetBreed(2, out _));
    }

    [Fact]
    public void SpellsThatDoNotFollowTheirBreedsAreRejected()
    {
        var catalog = TestTables.Breeds();

        Assert.Throws<ArgumentException>(() => new BreedCatalog(catalog.Breeds, catalog.Spells[..1], catalog.Roles, catalog.BreedRoles, catalog.StatCosts));
    }
}
