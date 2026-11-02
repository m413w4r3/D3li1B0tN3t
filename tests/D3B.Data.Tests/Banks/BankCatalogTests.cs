using D3B.Data.Banks;

namespace D3B.Data.Tests.Banks;

public sealed class BankCatalogTests
{
    [Fact]
    public void ABankerListsHisChestReplies()
    {
        var catalog = TestTables.Banks();

        Assert.True(catalog.TryGetBanker(100, out var banker));
        Assert.Equal([259, 5422], catalog.RepliesOf(banker).ToArray());
        Assert.True(catalog.TryGetBanker(6394, out banker));
        Assert.Equal([63_535], catalog.RepliesOf(banker).ToArray());
        Assert.False(catalog.TryGetBanker(101, out _));
    }

    [Fact]
    public void RepliesThatDoNotFollowOneAnotherAreRejected()
    {
        Bank[] banks = [new(2756, 191_104_002)];
        int[] replies = [259, 5422, 63_535];

        Assert.Throws<ArgumentException>(() => new BankCatalog(banks, new Banker[] { new(100, 1, 0, 2), new(6394, 1, 1, 2) }, replies));
        Assert.Throws<ArgumentException>(() => new BankCatalog(banks, new Banker[] { new(100, 1, 0, 1), new(6394, 1, 1, 1) }, replies));
    }

    [Fact]
    public void ABankerWithoutRepliesIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Banker(100, 1, 0, 0));
    }
}
