using D3B.Bot.Interactives;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Interactives;

public sealed class ElementUseTests
{
    private static readonly InteractiveUseRequest _treeUse = new() { SkillInstanceId = TestPlay.TreeInstanceId, ElementId = TestPlay.TreeElementId };

    private static InteractiveUsedEvent Started(int duration = 30)
    {
        return new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = duration };
    }

    private static InteractiveUseEndedEvent Ended()
    {
        return new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId };
    }

    [Fact]
    public async Task AHarvestWalksNextToTheElementThenUsesItUntilItsEnd()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var use = bot.UseAsync(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestContext.Current.CancellationToken);

        var cell = await TestWalks.WalkedAsync(play, connection, 1);

        Assert.NotEqual(-1, MapGeometry.Direction(cell, TestPlay.TreeCell));
        Assert.Equal(_treeUse, await connection.SentAsync(2));

        connection.Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: false) });
        connection.Server(Started());
        await bot.ProcessedAsync();
        Assert.True(bot.State.IsUsing);

        play.Time.Advance(TimeSpan.FromMilliseconds(2_999));
        await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.False(use.IsCompleted);

        connection.Server(Ended());
        connection.Server(new ObjectObtainedEvent { ItemId = 476, Quantity = 9 });

        Assert.Equal(UseOutcome.Used, await use);
        await bot.ProcessedAsync();
        Assert.False(bot.State.IsUsing);
        Assert.Equal(2, connection.Sent.Count);
    }

    [Fact]
    public async Task ACharacterNextToTheElementUsesItAtOnce()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var use = bot.UseAsync(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestContext.Current.CancellationToken);

        Assert.Equal(_treeUse, await connection.SentAsync(1));

        connection.Server(Started(duration: 15));
        connection.Server(Ended());

        Assert.Equal(UseOutcome.Used, await use);
    }

    [Fact]
    public async Task AUseTheServerRefusesIsRefused()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var use = bot.UseAsync(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.TreeElementId, SkillInstanceId = TestPlay.TreeSkillId });
        connection.Server(new InteractiveUseErrorEvent { ElementId = TestPlay.TreeElementId, SkillInstanceId = TestPlay.TreeInstanceId });

        Assert.Equal(UseOutcome.Refused, await use);
    }

    [Fact]
    public async Task AUseTheServerDoesNotAnswerIsOverAfterItsDelay()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var use = bot.UseAsync(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);

        Assert.Equal(UseOutcome.NoAnswer, await TestWalks.AdvanceUntilDoneAsync(play.Time, use));
    }

    [Fact]
    public async Task AHarvestTheServerDoesNotEndIsOverAfterItsDurationAndADelay()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var use = bot.UseAsync(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(Started());

        Assert.Equal(UseOutcome.NotEnded, await TestWalks.AdvanceUntilDoneAsync(play.Time, use));
    }

    [Fact]
    public async Task AnElementWithoutTheSkillNowOrOffTheMapIsUnavailable()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        Assert.Equal(UseOutcome.Unavailable, await bot.UseAsync(TestPlay.TreeElementId, 39, TestContext.Current.CancellationToken));
        Assert.Equal(UseOutcome.Unavailable, await bot.UseAsync(1, TestPlay.TreeSkillId, TestContext.Current.CancellationToken));

        // An element of the map north, which the information lists too: the server would answer nothing.
        Assert.Equal(UseOutcome.Unavailable, await bot.UseAsync(TestPlay.NeighboursElementId, TestPlay.TreeSkillId, TestContext.Current.CancellationToken));

        connection.Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: false) });
        await bot.ProcessedAsync();

        Assert.Equal(UseOutcome.Unavailable, await bot.UseAsync(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnElementAnotherActorStartsHarvestingDuringTheWalkIsLeftAlone()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);
        var use = bot.UseAsync(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: false) });
        await TestWalks.WalkedAsync(play, connection, 1);

        Assert.Equal(UseOutcome.Unavailable, await use);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task ACharacterWalledInHasNoPathToTheElement()
    {
        await using var play = new TestPlay([.. Enumerable.Range(0, 8).Select(direction => MapGeometry.Neighbour(433, direction))]);
        var (connection, bot) = await play.EnterAsync(433);

        Assert.Equal(UseOutcome.NoPath, await bot.UseAsync(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACharacterUsingAnElementIsBusy()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);

        connection.Server(Started());
        await bot.ProcessedAsync();

        Assert.Equal(UseOutcome.Busy, await bot.UseAsync(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestContext.Current.CancellationToken));
        Assert.Equal(WalkOutcome.Busy, await bot.WalkAsync(300, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }
}
