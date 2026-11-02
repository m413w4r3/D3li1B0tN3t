using System.Collections.Concurrent;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.World;

namespace D3B.Bot.Tests.Interactives;

public sealed class GraphicLearnerTests
{
    private static async Task CountedAsync(GraphicSkills graphics, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (graphics.Count < count)
        {
            Assert.True(DateTime.UtcNow < deadline, $"{graphics.Count} graphics learned, not {count}.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task AMapEnteredTeachesWhatTheElementsTheServerListsOfferByTheirGraphics()
    {
        var graphics = new GraphicSkills();
        var told = new ConcurrentQueue<int>();

        graphics.Learned += told.Enqueue;

        // The learner reads the maps of a pack as the play's own.
        await using var pack = new TestPlay();
        await using var play = new TestPlay([new GraphicLearner(pack.Maps, graphics)]);

        await play.EnterAsync(433);
        await CountedAsync(graphics, 2);

        Assert.True(graphics.TryGet(TestPlay.TreeGfxId, out var tree));
        Assert.Equal([TestPlay.TreeSkillId], tree);
        Assert.True(graphics.TryGet(TestPlay.DoorGfxId, out var door));
        Assert.Equal([WorldPathfinder.DoorSkillId], door);

        // The tree of the map north, which the information lists with its own map's cell, teaches nothing from here.
        Assert.Equal(2, graphics.Count);
        Assert.Equal(new[] { TestPlay.TreeGfxId, TestPlay.DoorGfxId }.Order(), told.Order());
    }
}
