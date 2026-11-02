using D3B.Bot.Interactives;

namespace D3B.Bot.Tests.Interactives;

public sealed class GraphicSkillsTests
{
    [Fact]
    public void AGraphicLearnsEverySkillSeenAndTellsOnlyWhatIsNew()
    {
        var graphics = new GraphicSkills();
        var told = new List<int>();

        graphics.Learned += told.Add;

        Assert.True(graphics.Learn(1_018, [124]));
        Assert.False(graphics.Learn(1_018, [124]));
        Assert.True(graphics.Learn(1_018, [125, 124]));
        Assert.False(graphics.Learn(1_018, [125]));
        Assert.True(graphics.TryGet(1_018, out var skills));
        Assert.Equal([124, 125], skills.Order());
        Assert.Equal([1_018, 1_018], told);
    }

    [Fact]
    public void AGraphicWhoseElementOffersNoneIsKnownOfferingNone()
    {
        var graphics = new GraphicSkills();

        Assert.False(graphics.TryGet(3_518, out _));
        Assert.True(graphics.Learn(3_518, []));
        Assert.True(graphics.TryGet(3_518, out var skills));
        Assert.Empty(skills);
        Assert.Equal(1, graphics.Count);
    }

    [Fact]
    public void WhatIsRememberedIsKnownWithoutBeingTold()
    {
        var graphics = new GraphicSkills();
        var told = new List<int>();

        graphics.Learned += told.Add;
        graphics.Remember(650, [6]);

        Assert.True(graphics.TryGet(650, out var skills));
        Assert.Equal([6], skills);
        Assert.False(graphics.Learn(650, [6]));
        Assert.Empty(told);
    }
}
