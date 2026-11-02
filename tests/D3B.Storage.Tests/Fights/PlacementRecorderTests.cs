using D3B.Bot.Fights;
using D3B.Data.Tests;
using D3B.Storage.Fights;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage.Tests.Fights;

public sealed class PlacementRecorderTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public async Task TheFightsCellsAndMapsAreKnownFromTheNextRunsStart()
    {
        var path = _directory.Combine("d3b.db");
        var start = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

        await using (var first = await new TestStorage(path).StartedAsync())
        {
            var placements = first.Services.GetRequiredService<FightPlacements>();

            placements.Seen(1, 2, [314, 300], [385, 371], null, start);
            placements.Seen(1, 2, [314, 300], [385, 371], null, start.AddMinutes(3));
            placements.Seen(1, 1, [100], [101], null, start);
            await first.Services.GetRequiredService<PlacementRecorder>().WrittenAsync();
        }

        await using (var second = await new TestStorage(path).StartedAsync())
        {
            var placements = second.Services.GetRequiredService<FightPlacements>();

            Assert.Equal(2, placements.Count);
            Assert.Contains(second.Log.GetSnapshot(), record => record.Message.Contains(", 2 fights' placements,", StringComparison.Ordinal));

            // Counted on from what the database kept.
            placements.Seen(1, 2, [300, 314], [371, 385], null, start.AddDays(1));
            await second.Services.GetRequiredService<PlacementRecorder>().WrittenAsync();
        }

        await using var next = await new TestStorage(path).StartedAsync();
        var known = next.Services.GetRequiredService<FightPlacements>().From(1);

        Assert.Equal(2, known.Count);
        Assert.Equal((2L, 3), (known[0].MapId, known[0].Seen));
        Assert.Equal([300, 314], known[0].RedCells);
        Assert.Equal([371, 385], known[0].BlueCells);
        Assert.Equal((start, start.AddDays(1)), (known[0].FirstSeen, known[0].LastSeen));
    }
}
