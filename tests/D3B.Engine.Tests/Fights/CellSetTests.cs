using D3B.Data.Maps;
using D3B.Engine.Fights;

namespace D3B.Engine.Tests.Fights;

public sealed class CellSetTests
{
    [Fact]
    public void ASetHoldsTheCellsAddedAndNotThoseRemoved()
    {
        var set = CellSet.Of(0, 63, 64, 559);

        set.Remove(63);

        Assert.True(set.Contains(0));
        Assert.False(set.Contains(63));
        Assert.True(set.Contains(64));
        Assert.True(set.Contains(559));
        Assert.False(set.Contains(-1));
        Assert.False(set.Contains(MapData.CellCount));
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void ACellsSetEnumeratesInAscendingOrder()
    {
        Assert.Equal([2, 70, 128, 500], CellSet.Of(500, 2, 128, 70).ToArray());
        Assert.Empty(default(CellSet).ToArray());
        Assert.True(default(CellSet).IsEmpty);
    }

    [Fact]
    public void EveryCellOfAMapIsInTheWholeSet()
    {
        Assert.Equal(MapData.CellCount, CellSet.All.Count);
        Assert.Equal(Enumerable.Range(0, MapData.CellCount), CellSet.All.ToArray());
    }

    [Fact]
    public void SetsCombineAsSets()
    {
        var union = CellSet.Of(1, 2, 3);
        var intersection = union;
        var difference = union;

        union.UnionWith(CellSet.Of(3, 400));
        intersection.IntersectWith(CellSet.Of(2, 3, 400));
        difference.ExceptWith(CellSet.Of(2, 400));

        Assert.Equal([1, 2, 3, 400], union.ToArray());
        Assert.Equal([2, 3], intersection.ToArray());
        Assert.Equal([1, 3], difference.ToArray());
    }

    [Fact]
    public void TwoSetsOfTheSameCellsAreEqual()
    {
        Assert.Equal(CellSet.Of(5, 300), CellSet.Of(300, 5));
        Assert.True(CellSet.Of(5) != CellSet.Of(6));
        Assert.Equal(CellSet.Of(5, 300).GetHashCode(), CellSet.Of(300, 5).GetHashCode());
    }

    [Fact]
    public void ACellOffTheMapCannotBeAdded()
    {
        var set = default(CellSet);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(MapData.CellCount));
    }
}
