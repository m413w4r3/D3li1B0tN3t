using D3B.Engine.Fights;

namespace D3B.Engine.Tests.Fights;

public sealed class CastHistoryTests
{
    [Fact]
    public void ATurnCountsTheCastsOfASpellAndOnEachTarget()
    {
        var history = CastHistory.Empty.Cast(10, -1, 2).Cast(10, -2, 2).Cast(10, -1, 2).Cast(20, null, 2);

        Assert.Equal(3, history.CastsIn(10, 2));
        Assert.Equal(2, history.CastsOn(10, -1, 2));
        Assert.Equal(1, history.CastsOn(10, -2, 2));
        Assert.Equal(1, history.CastsIn(20, 2));
        Assert.Equal(0, history.CastsIn(30, 2));
    }

    [Fact]
    public void ANewRoundStartsTheCountsAgainAndKeepsTheLastRounds()
    {
        var history = CastHistory.Empty.Cast(10, -1, 2).Cast(20, null, 2).Cast(10, -1, 3);

        Assert.Equal(1, history.CastsIn(10, 3));
        Assert.Equal(0, history.CastsIn(20, 3));
        Assert.Equal(0, history.CastsIn(10, 2));
        Assert.Equal(1, history.CastsOn(10, -1, 3));
        Assert.Equal(3, history.LastRounds[10]);
        Assert.Equal(2, history.LastRounds[20]);
    }
}
