using D3B.Bot.Journal;

namespace D3B.Bot.Tests.Journal;

public sealed class RecentItemsTests
{
    [Fact]
    public void AReaderGetsWhatCameSinceItLastRead()
    {
        var items = new RecentItems<int>(10);
        long read = 0;

        items.Add(1);
        items.Add(2);

        Assert.Equal([1, 2], items.After(ref read));
        Assert.Empty(items.After(ref read));

        items.Add(3);

        Assert.Equal([3], items.After(ref read));
        Assert.Equal(3, read);
    }

    [Fact]
    public void TheOldestItemsAreDroppedOnceTheCapacityIsReached()
    {
        var items = new RecentItems<int>(3);
        long read = 0;

        for (var item = 1; item <= 5; item++)
            items.Add(item);

        Assert.Equal([3, 4, 5], items.All());
        Assert.Equal(5, items.Written);

        // What was dropped before the reader came back is gone: it gets what is still kept.
        Assert.Equal([3, 4, 5], items.After(ref read));
    }
}
