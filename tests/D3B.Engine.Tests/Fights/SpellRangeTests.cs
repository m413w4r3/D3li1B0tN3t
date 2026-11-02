using D3B.Data.Spells;
using D3B.Engine.Fights;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Fights;

public sealed class SpellRangeTests
{
    private static readonly SpellLevel _level = new() { MinRange = 1, Range = 3 };

    [Fact]
    public void ALevelReachesTheCellsBetweenItsLeastAndMostRangeAsADiamond()
    {
        Assert.False(SpellRange.Reaches(_level, 0, Middle, Middle));
        Assert.True(SpellRange.Reaches(_level, 0, Middle, Off(1, 0)));
        Assert.True(SpellRange.Reaches(_level, 0, Middle, Off(2, -1)));
        Assert.True(SpellRange.Reaches(_level, 0, Middle, Off(-1, 2)));
        Assert.False(SpellRange.Reaches(_level, 0, Middle, Off(2, 2)));
        Assert.Equal(4 + 8 + 12, SpellRange.Cells(_level, 0, Middle).Count);
    }

    [Fact]
    public void TheCastersRangeBonusAddsOnlyToALevelThatMayBeBoosted()
    {
        var boosted = _level with { Properties = SpellLevelProperties.RangeCanBeBoosted };

        Assert.False(SpellRange.Reaches(_level, 2, Middle, Off(5, 0)));
        Assert.True(SpellRange.Reaches(boosted, 2, Middle, Off(5, 0)));
        Assert.False(SpellRange.Reaches(boosted, 2, Middle, Off(6, 0)));
        Assert.Equal(5, SpellRange.MaxRange(boosted, 2));
    }

    [Fact]
    public void TheMostRangeNeverFallsBelowTheLeast()
    {
        var boosted = _level with { MinRange = 2, Properties = SpellLevelProperties.RangeCanBeBoosted };

        Assert.Equal(2, SpellRange.MaxRange(boosted, -5));
        Assert.Equal(0, SpellRange.MaxRange(new SpellLevel { Properties = SpellLevelProperties.RangeCanBeBoosted }, -5));
        Assert.True(SpellRange.Reaches(boosted, -5, Middle, Off(2, 0)));
    }

    [Fact]
    public void ALevelCastInLineReachesTheCastersRowAndColumnOnly()
    {
        var inLine = _level with { Properties = SpellLevelProperties.CastInLine };

        Assert.True(SpellRange.Reaches(inLine, 0, Middle, Off(3, 0)));
        Assert.True(SpellRange.Reaches(inLine, 0, Middle, Off(0, -2)));
        Assert.False(SpellRange.Reaches(inLine, 0, Middle, Off(1, 1)));
        Assert.False(SpellRange.Reaches(inLine, 0, Middle, Off(2, -1)));
    }

    [Fact]
    public void ALevelCastInDiagonalCountsItsRangeInDiagonalSteps()
    {
        var diagonal = _level with { Properties = SpellLevelProperties.CastInDiagonal };
        var proportional = diagonal with { Properties = SpellLevelProperties.CastInDiagonal | SpellLevelProperties.ProportionalDiagonal };

        Assert.True(SpellRange.Reaches(diagonal, 0, Middle, Off(3, -3)));
        Assert.False(SpellRange.Reaches(diagonal, 0, Middle, Off(3, 0)));
        Assert.False(SpellRange.Reaches(proportional, 0, Middle, Off(2, 2)));
        Assert.True(SpellRange.Reaches(proportional, 0, Middle, Off(1, 1)));
    }

    [Fact]
    public void ALevelCastInLineAndDiagonalReachesBoth()
    {
        var both = _level with { Properties = SpellLevelProperties.CastInLine | SpellLevelProperties.CastInDiagonal };

        Assert.True(SpellRange.Reaches(both, 0, Middle, Off(3, 0)));
        Assert.True(SpellRange.Reaches(both, 0, Middle, Off(-2, 2)));
        Assert.False(SpellRange.Reaches(both, 0, Middle, Off(2, 1)));
    }
}
