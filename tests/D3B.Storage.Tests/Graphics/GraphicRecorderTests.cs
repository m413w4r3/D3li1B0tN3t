using D3B.Bot.Interactives;
using D3B.Data.Tests;
using D3B.Storage.Graphics;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage.Tests.Graphics;

public sealed class GraphicRecorderTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public async Task WhatTheBotsLearnOfTheGraphicsIsKnownFromTheNextRunsStart()
    {
        var path = _directory.Combine("d3b.db");

        await using (var first = await new TestStorage(path).StartedAsync())
        {
            var graphics = first.Services.GetRequiredService<GraphicSkills>();

            graphics.Learn(1_018, [124]);
            graphics.Learn(3_518, []);
            graphics.Learn(1_018, [125]);
            await first.Services.GetRequiredService<GraphicRecorder>().WrittenAsync();
        }

        await using var next = await new TestStorage(path).StartedAsync();
        var known = next.Services.GetRequiredService<GraphicSkills>();

        Assert.Equal(2, known.Count);
        Assert.True(known.TryGet(1_018, out var fishing));
        Assert.Equal([124, 125], fishing.Order());
        Assert.True(known.TryGet(3_518, out var door));
        Assert.Empty(door);
        Assert.Contains(next.Log.GetSnapshot(), record => record.Message.Contains(", 2 graphics learned,", StringComparison.Ordinal));
    }
}
