using D3B.Data.Packs;
using D3B.Data.Texts;

namespace D3B.Data.Tests.Packs;

public sealed class PackCacheTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly GamePack _pack;

    public PackCacheTests()
    {
        _pack = TestPacks.Open(_directory, TestPacks.Write(writer =>
        {
            for (var id = 1; id <= 3; id++)
                writer.Add($"records/{id}", TestPacks.Record(id));
        }));
    }

    public void Dispose()
    {
        _pack.Dispose();
        _directory.Dispose();
    }

    [Fact]
    public void AnEntryIsReadOnceWhileItStaysInTheCache()
    {
        var cache = new PackCache(_pack, long.MaxValue);

        Assert.Same(cache.Get<SampleRecord>("records/1"), cache.Get<SampleRecord>("records/1"));
        Assert.Equal(1, cache.Count);
        Assert.Equal(_pack.LengthOf("records/1"), cache.Weight);
    }

    [Fact]
    public void TheLeastRecentlyUsedEntryIsDroppedBeyondTheCapacity()
    {
        var cache = new PackCache(_pack, _pack.LengthOf("records/1") + _pack.LengthOf("records/2"));
        var first = cache.Get<SampleRecord>("records/1");
        var second = cache.Get<SampleRecord>("records/2");

        cache.Get<SampleRecord>("records/1");
        cache.Get<SampleRecord>("records/3");

        Assert.Equal(2, cache.Count);
        Assert.True(cache.Weight <= cache.Capacity);
        Assert.Same(first, cache.Get<SampleRecord>("records/1"));
        Assert.NotSame(second, cache.Get<SampleRecord>("records/2"));
    }

    [Fact]
    public void AnEntryHeavierThanTheCapacityIsStillReturned()
    {
        var cache = new PackCache(_pack, 1);
        var first = cache.Get<SampleRecord>("records/1");

        Assert.Equal(1, first.Id);
        Assert.Same(first, cache.Get<SampleRecord>("records/1"));

        cache.Get<SampleRecord>("records/2");

        Assert.Equal(1, cache.Count);
        Assert.NotSame(first, cache.Get<SampleRecord>("records/1"));
    }

    [Fact]
    public void ACachedEntryIsReadOnlyAsTheTypeItWasWrittenAs()
    {
        var cache = new PackCache(_pack, long.MaxValue);

        cache.Get<SampleRecord>("records/1");

        Assert.Throws<PackFormatException>(() => cache.Get<TextTable>("records/1"));
        Assert.Throws<PackFormatException>(() => cache.Get<object>("records/1"));
    }
}
