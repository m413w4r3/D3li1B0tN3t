using D3B.Data.Interactives;

namespace D3B.Data.Tests.Interactives;

public sealed class InteractiveCatalogTests
{
    [Fact]
    public void AnInteractiveIsFoundByItsIdentifier()
    {
        var catalog = TestTables.Interactives();

        Assert.True(catalog.TryGet(16, out var zaap));
        Assert.Equal(5002, zaap.NameId);
        Assert.False(catalog.TryGet(2, out _));
    }

    [Fact]
    public void InteractivesOutOfOrderAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new InteractiveCatalog(new Interactive[] { new(16, 1), new(1, 1) }));
    }
}
