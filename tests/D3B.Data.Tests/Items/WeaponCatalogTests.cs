using D3B.Data.Items;
using D3B.Data.Spells;

namespace D3B.Data.Tests.Items;

public sealed class WeaponCatalogTests
{
    [Fact]
    public void AWeaponItsEffectsAndItsTypesZoneAreFoundByTheirIdentifiers()
    {
        var catalog = TestTables.Weapons();

        Assert.True(catalog.TryGetWeapon(6751, out var sword));
        Assert.Equal((6, 4, 1, 1, 30, 10, 1), ((int)sword.TypeId, (int)sword.ApCost, (int)sword.MinRange, (int)sword.Range, (int)sword.CriticalHitProbability, (int)sword.CriticalHitBonus, (int)sword.MaxCastPerTurn));
        Assert.Equal(SpellLevelProperties.CastTestLos, sword.Properties);
        Assert.Equal([(97, 16, 25), (118, 21, 30)], catalog.EffectsOf(sword).ToArray().Select(effect => ((int)effect.EffectId, effect.DiceNum, effect.DiceSide)));
        Assert.True(catalog.TryGetType(sword.TypeId, out var type));
        Assert.Equal("P", catalog.ZoneOf(type));
        Assert.True(catalog.TryGetWeapon(7000, out var hammer));
        Assert.True(catalog.TryGetType(hammer.TypeId, out var hammers));
        Assert.Equal("X1,0,10,1", catalog.ZoneOf(hammers));
        Assert.False(catalog.TryGetWeapon(303, out _));
        Assert.False(catalog.TryGetType(38, out _));
    }

    [Fact]
    public void ATableOutOfOrderOrAZoneTheCatalogLacksIsRejected()
    {
        string[] zones = ["P"];

        Assert.Throws<ArgumentException>(() => new WeaponCatalog(new Weapon[] { new() { ItemId = 7000 }, new() { ItemId = 6751 } }, ReadOnlyMemory<WeaponEffect>.Empty, ReadOnlyMemory<WeaponType>.Empty, zones));
        Assert.Throws<ArgumentException>(() => new WeaponCatalog(new Weapon[] { new() { ItemId = 6751, EffectCount = 1 } }, ReadOnlyMemory<WeaponEffect>.Empty, ReadOnlyMemory<WeaponType>.Empty, zones));
        Assert.Throws<ArgumentException>(() => new WeaponCatalog(ReadOnlyMemory<Weapon>.Empty, ReadOnlyMemory<WeaponEffect>.Empty, new WeaponType[] { new() { Id = 6, Zone = 1 } }, zones));
    }
}
