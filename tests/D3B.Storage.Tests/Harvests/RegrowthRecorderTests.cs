using D3B.Bot.Harvesting;
using D3B.Data.Tests;
using D3B.Storage.Harvests;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage.Tests.Harvests;

public sealed class RegrowthRecorderTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public async Task HowLongTheResourcesTakeToGrowBackIsKnownFromTheNextRunsStart()
    {
        var path = _directory.Combine("d3b.db");
        var start = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

        await using (var first = await new TestStorage(path).StartedAsync())
        {
            var regrowths = first.Services.GetRequiredService<Regrowths>();

            regrowths.Harvested(1, 10, 68, start);
            regrowths.GrownBack(1, 10, 68, start.AddSeconds(400.5), watchedSince: start);
            regrowths.Harvested(1, 11, 125, start);
            regrowths.GrownBack(1, 11, 125, start.AddSeconds(564), watchedSince: start.AddSeconds(564));
            await first.Services.GetRequiredService<RegrowthRecorder>().WrittenAsync();
        }

        await using var next = await new TestStorage(path).StartedAsync();
        var known = next.Services.GetRequiredService<Regrowths>();

        Assert.Equal(2, known.Count);
        Assert.Equal(new Regrowth(TimeSpan.FromMilliseconds(400_500), 1, null), known.TryGet(68, out var nettle) ? nettle : default);
        Assert.Equal(new Regrowth(null, 0, TimeSpan.FromSeconds(564)), known.TryGet(125, out var trout) ? trout : default);
        Assert.Contains(next.Log.GetSnapshot(), record => record.Message.Contains(", 2 resources' growing back,", StringComparison.Ordinal));
    }
}
