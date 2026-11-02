using D3B.Data.Maps;
using D3B.Data.Spells;
using D3B.Engine.Fights;
using static D3B.Engine.Tests.Fights.TestFights;

namespace D3B.Engine.Tests.Fights;

public sealed class SpellZonesTests
{
    private static readonly int _centre = Off(2, 0);

    [Fact]
    public void APointIsTheCellCastOn()
    {
        Assert.Equal(CellSet.Of(_centre), Cells(Zone(SpellZoneShape.Point)));
    }

    [Fact]
    public void ACircleIsADiamondBetweenItsSizes()
    {
        Assert.Equal(1 + 4 + 8, Cells(Zone(SpellZoneShape.Circle, 2)).Count);
        Assert.Equal(4 + 8, Cells(Zone(SpellZoneShape.Circle, 2, 1)).Count);
        Assert.Equal(8, Cells(Zone(SpellZoneShape.Ring, 2)).Count);
        Assert.False(Cells(Zone(SpellZoneShape.OutsideCircle, 1)).Contains(_centre));
    }

    [Fact]
    public void ACrossLiesAlongBothAxesAndAStarAlongTheDiagonalsToo()
    {
        Assert.Equal(1 + 8, Cells(Zone(SpellZoneShape.Cross, 2)).Count);
        Assert.Equal(8, Cells(Zone(SpellZoneShape.CrossWithoutCenter, 2)).Count);
        Assert.Equal(CellSet.Of(Off(1, -1), Off(1, 1), _centre, Off(3, -1), Off(3, 1)), Cells(Zone(SpellZoneShape.DiagonalCross, 1)));
        Assert.Equal(1 + 8, Cells(Zone(SpellZoneShape.Star, 1)).Count);
    }

    [Fact]
    public void ALineRunsFromTheCellAwayFromTheCaster()
    {
        Assert.Equal(CellSet.Of(Off(2, 0), Off(3, 0), Off(4, 0)), Cells(Zone(SpellZoneShape.Line, 2)));
        Assert.Equal(CellSet.Of(Off(2, 1), Off(3, 2)), Cells(Zone(SpellZoneShape.Line, 1), centre: Off(2, 1), caster: Off(0, -1)));
    }

    [Fact]
    public void AForcedDirectionTurnsTheZone()
    {
        Assert.Equal(CellSet.Of(_centre, Off(3, 0)), Cells(Zone(SpellZoneShape.Line, 1, forced: 1), caster: Off(2, 3)));
        Assert.Equal(CellSet.Of(Off(1, 0), _centre), Cells(Zone(SpellZoneShape.Line, 1, forced: 5)));
    }

    [Fact]
    public void ALineFromTheCasterStopsAtTheFirstFighterWhenItSaysSo()
    {
        var zone = Zone(SpellZoneShape.LineFromCaster, 1, 4);

        Assert.Equal(CellSet.Of(Off(1, 0), Off(2, 0), Off(3, 0), Off(4, 0)), Cells(zone));
        Assert.Equal(CellSet.Of(Off(1, 0), Off(2, 0)), Cells(zone with { StopsAtTarget = true }, fighters: CellSet.Of(Off(2, 0), Off(3, 0))));
    }

    [Fact]
    public void APerpendicularLineCrossesTheCastsDirection()
    {
        Assert.Equal(CellSet.Of(Off(2, -1), _centre, Off(2, 1)), Cells(Zone(SpellZoneShape.PerpendicularLine, 1)));
    }

    [Fact]
    public void AConeWidensAwayFromTheCaster()
    {
        Assert.Equal(CellSet.Of(_centre, Off(3, -1), Off(3, 0), Off(3, 1)), Cells(Zone(SpellZoneShape.Cone, 1)));
    }

    [Fact]
    public void ASquareCoversBothAxesAndLeavesItsDiagonalsWhenAsked()
    {
        Assert.Equal(9, Cells(Zone(SpellZoneShape.Square, 1)).Count);
        Assert.Equal(5, Cells(Zone(SpellZoneShape.SquareWithoutDiagonals, 1)).Count);
    }

    [Fact]
    public void AWholeMapZoneCoversEveryCellAndACustomOneItsList()
    {
        Assert.Equal(MapData.CellCount, Cells(Zone(SpellZoneShape.WholeMap)).Count);
        Assert.Equal(CellSet.Of(5, 300), SpellZones.Cells(Zone(SpellZoneShape.Custom), [300, 5], Open(), default, Middle, _centre));
        Assert.True(Cells(Zone(SpellZoneShape.Empty)).IsEmpty);
    }

    [Fact]
    public void AZoneKeepingToTheLineOfSightLeavesTheCellsItsCentreDoesNotSee()
    {
        var zone = Zone(SpellZoneShape.Circle, 2, seen: true);
        var cells = SpellZones.Cells(zone, [], WithPillars(Off(3, 0)), default, Middle, _centre);

        Assert.True(cells.Contains(Off(3, 0)));
        Assert.False(cells.Contains(Off(4, 0)));
        Assert.True(cells.Contains(Off(2, 2)));
    }

    private static CellSet Cells(SpellZone zone, int? centre = null, int caster = Middle, CellSet fighters = default)
    {
        return SpellZones.Cells(zone, [], Open(), fighters, caster, centre ?? _centre);
    }
}
