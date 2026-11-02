using D3B.Data.Items;

namespace D3B.Data.Tests.Items;

public sealed class ItemCatalogTests
{
    [Fact]
    public void AnItemAndItsTypeAreFoundByTheirIdentifiers()
    {
        var catalog = TestTables.Items();

        Assert.True(catalog.TryGetItem(303, out var item));
        Assert.Equal((1002, 38, 1, 1, 38_017), (item.NameId, (int)item.TypeId, (int)item.Level, (int)item.Weight, item.IconId));
        Assert.True(catalog.TryGetType(item.TypeId, out var type));
        Assert.Equal(9, type.SuperTypeId);
        Assert.False(catalog.TryGetItem(304, out _));
    }

    [Fact]
    public void AnItemWhoseUseOnlyHealsKeepsTheLifeItGivesBack()
    {
        var catalog = TestTables.Items();

        Assert.True(catalog.TryGetItem(468, out var bread));
        Assert.True(bread.Heals);
        Assert.Equal((10, 15), ((int)bread.HealMin, (int)bread.HealMax));
        Assert.True(catalog.TryGetItem(303, out var wood));
        Assert.False(wood.Heals);
    }

    [Fact]
    public void AnItemMayNameATypeTheCatalogLacks()
    {
        var catalog = TestTables.Items();

        Assert.True(catalog.TryGetItem(34_569, out var item));
        Assert.False(catalog.TryGetType(item.TypeId, out _));
    }

    [Fact]
    public void ATableOutOfOrderIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ItemCatalog(new Item[] { new(303, 1, 38, 1, 1, 0), new(289, 1, 34, 1, 1, 0) }, ReadOnlyMemory<ItemType>.Empty));
        Assert.Throws<ArgumentException>(() => new ItemCatalog(ReadOnlyMemory<Item>.Empty, new ItemType[] { new(38, 1, 9, 2), new(38, 2, 9, 2) }));
    }
}
