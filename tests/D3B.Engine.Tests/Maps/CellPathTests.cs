using D3B.Engine.Maps;

namespace D3B.Engine.Tests.Maps;

public sealed class CellPathTests
{
    [Fact]
    public void TurningPointsAreTheFirstCellEveryTurnAndTheLastWithItsArrival()
    {
        // West twice, then north-west, then north: the shape of a path the server walked (client.md).
        const int start = 30 * 14 + 13;
        var path = new CellPath([start, start - 1, start - 2, start - 2 - 15, start - 2 - 15 - 28]);

        Assert.Equal(
            [new PathTurn(start, 4), new PathTurn(start - 2, 5), new PathTurn(start - 17, 6), new PathTurn(start - 45, 6)],
            path.TurningPoints());
        Assert.Equal(4, path.Steps);
        Assert.Equal(start, path.Start);
        Assert.Equal(start - 45, path.End);
    }

    [Fact]
    public void AStepIsTwoTurningPoints()
    {
        Assert.Equal([new PathTurn(100, 2), new PathTurn(128, 2)], new CellPath([100, 128]).TurningPoints());
    }

    [Fact]
    public void APathWithoutAStepHasNoTurningPoint()
    {
        var path = new CellPath([100]);

        Assert.Empty(path.TurningPoints());
        Assert.Equal(0, path.Steps);
    }

    [Fact]
    public void APathOfCellsThatAreNotNeighboursIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new CellPath([100, 102]));
        Assert.Throws<ArgumentException>(() => new CellPath([]));
    }
}
