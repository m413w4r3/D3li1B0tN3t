using D3B.Data.Maps;
using D3B.Data.Spells;
using D3B.Engine.Maps;

namespace D3B.Engine.Fights;

/// <summary>The cells an effect's zone covers around the cell a spell is cast on: its shape, sizes and direction.</summary>
/// <remarks>
/// <para>
/// A zone lies on the map's coordinates (<see cref="MapPoint"/>), centred on the cell cast on, its size
/// (<see cref="SpellZone.Size"/>, the client's <c>param1</c>) and least size (<see cref="SpellZone.MinSize"/>,
/// <c>param2</c>) counted as the range is. A zone with a direction takes the cast's, from the caster's cell to the cell
/// cast on: along the axis of the coordinates it is farthest on, or along a diagonal when it lies on one exactly, unless
/// the zone forces one (<see cref="SpellZone.ForcedDirection"/>, a direction of the map, 0 east then clockwise).
/// </para>
/// <list type="table">
/// <item><term>Point</term><description>The cell.</description></item>
/// <item><term>Circle</term><description>The cells at a distance between the least size and the size: a diamond.</description></item>
/// <item><term>Ring, OutsideCircle, OutsideComplexCircle</term><description>At the size exactly; at the size or beyond; as the latter.</description></item>
/// <item><term>Cross, CrossWithoutCenter</term><description>Along both axes, between the sizes; the centre only when the least size is 0.</description></item>
/// <item><term>DiagonalCross, DiagonalCrossWithoutCenter, Star</term><description>Along both diagonals, likewise; the axes and the diagonals.</description></item>
/// <item><term>Line, DiagonalLine</term><description>From the cell on, away from the caster, between the sizes.</description></item>
/// <item><term>LineFromCaster</term><description>From the caster's cell towards the cell cast on, from <c>param1</c> on for <c>param2</c> cells, stopping
/// at the first fighter when the zone says so.</description></item>
/// <item><term>PerpendicularLine, DiagonalPerpendicularLine</term><description>Across the direction, on both sides, up to the size.</description></item>
/// <item><term>Cone, HalfCircle, Boomerang, Fork</term><description>Widening away from the caster; two arms back towards it; two arms away; three
/// arms away.</description></item>
/// <item><term>Square, SquareWithoutDiagonals, Checkerboard</term><description>Within the size on both axes; without the diagonals; every other cell of
/// the diamond.</description></item>
/// <item><term>Rectangle</term><description>From the cell away from the caster, the size long and the least size wide.</description></item>
/// <item><term>WholeMap, WholeMapWithTheDead, Custom</term><description>Every cell; every cell, the dead counted; the cells the zone lists.</description></item>
/// </list>
/// <para>
/// A zone that keeps to the line of sight keeps the cells seen from its centre. The 26 shapes and their sizes are the
/// pack's, measured (client.md); which cells each covers is the references' idea (D181), measured on the 49 fights captured
/// for the 15 they cast: of 388 losses of life, 380 lie in the zone of a cast of their sequence whose mask selects the
/// fighter, and each of point, circle, cross, square, line, line from the caster, perpendicular line, diagonal cross, cross
/// without centre, star, diagonal perpendicular line, fork, half circle, cone and whole map explains some alone; of 343
/// fighters an effect damaging at once foresaw, 342 lost life. Ring, outside circles, checkerboard, square without
/// diagonals, rectangle, boomerang, diagonal line, diagonal cross without centre and the custom zones are not measured.
/// </para>
/// </remarks>
public static class SpellZones
{
    /// <summary>Gets the cells an effect's zone covers.</summary>
    /// <param name="zone">The zone.</param>
    /// <param name="listed">The cells it lists, for a <see cref="SpellZoneShape.Custom"/> zone: <see cref="SpellCatalog.CellsOf"/>.</param>
    /// <param name="map">The fight's map.</param>
    /// <param name="fighters">The cells fighters stand on, where a zone may stop or a line of sight be blocked.</param>
    /// <param name="caster">The caster's cell.</param>
    /// <param name="centre">The cell cast on.</param>
    /// <returns>The cells of the map the zone covers.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A cell does not exist.</exception>
    public static CellSet Cells(in SpellZone zone, ReadOnlySpan<int> listed, FightMap map, in CellSet fighters, int caster, int centre)
    {
        ArgumentNullException.ThrowIfNull(map);

        var origin = MapGeometry.PointOf(centre);
        var from = MapGeometry.PointOf(caster);
        var forced = zone.ForcedDirection is >= 0 and < MapCell.DirectionCount;
        var straight = forced ? Of(zone.ForcedDirection) : Straight(from, origin);
        var axis = forced ? Of(zone.ForcedDirection) : Axis(from, origin);
        var size = (int)zone.Size;
        var least = (int)zone.MinSize;
        var cells = default(CellSet);

        switch (zone.Shape)
        {
            case SpellZoneShape.Point:
                cells.Add(centre);
                break;
            case SpellZoneShape.Circle or SpellZoneShape.Regular:
                Diamond(ref cells, origin, least, size, everyOther: false);
                break;
            case SpellZoneShape.Ring:
                Diamond(ref cells, origin, size, size, everyOther: false);
                break;
            case SpellZoneShape.OutsideCircle or SpellZoneShape.OutsideComplexCircle:
                Diamond(ref cells, origin, size, int.MaxValue, everyOther: false);
                break;
            case SpellZoneShape.Checkerboard:
                Diamond(ref cells, origin, least, size, everyOther: true);
                break;
            case SpellZoneShape.Cross:
                Star(ref cells, origin, least, size, axes: true, diagonals: false);
                break;
            case SpellZoneShape.CrossWithoutCenter:
                Star(ref cells, origin, Math.Max(least, 1), size, axes: true, diagonals: false);
                break;
            case SpellZoneShape.DiagonalCross:
                Star(ref cells, origin, least, size, axes: false, diagonals: true);
                break;
            case SpellZoneShape.DiagonalCrossWithoutCenter:
                Star(ref cells, origin, Math.Max(least, 1), size, axes: false, diagonals: true);
                break;
            case SpellZoneShape.Star:
                Star(ref cells, origin, least, size, axes: true, diagonals: true);
                break;
            case SpellZoneShape.Line or SpellZoneShape.DiagonalLine:
                Ray(ref cells, origin, straight, least, size, fighters, stop: false);
                break;
            case SpellZoneShape.LineFromCaster:
                Ray(ref cells, from, straight, size, size + Math.Max(least, 1) - 1, fighters, zone.StopsAtTarget);
                break;
            case SpellZoneShape.PerpendicularLine:
                Across(ref cells, origin, axis, least, size);
                break;
            case SpellZoneShape.DiagonalPerpendicularLine:
                Across(ref cells, origin, straight, least, size);
                break;
            case SpellZoneShape.Cone:
                Cone(ref cells, origin, axis, least, size);
                break;
            case SpellZoneShape.HalfCircle:
                Arms(ref cells, origin, axis, size, forward: -1, middle: false);
                break;
            case SpellZoneShape.Boomerang:
                Arms(ref cells, origin, axis, size, forward: 1, middle: false);
                break;
            case SpellZoneShape.Fork:
                Arms(ref cells, origin, axis, size, forward: 1, middle: true);
                break;
            case SpellZoneShape.Square:
                Square(ref cells, origin, least, size, diagonals: true);
                break;
            case SpellZoneShape.SquareWithoutDiagonals:
                Square(ref cells, origin, least, size, diagonals: false);
                break;
            case SpellZoneShape.Rectangle:
                Rectangle(ref cells, origin, axis, size, least);
                break;
            case SpellZoneShape.WholeMap or SpellZoneShape.WholeMapWithTheDead:
                cells = CellSet.All;
                break;
            case SpellZoneShape.Custom:
                foreach (var cell in listed)
                {
                    if (cell is >= 0 and < MapData.CellCount)
                        cells.Add(cell);
                }

                break;
        }

        if (zone.OnlyInLineOfSight)
            KeepSeen(ref cells, map, fighters, centre);

        return cells;
    }

    private static (int X, int Y) Of(int direction)
    {
        return direction switch
        {
            0 => (1, 1),
            1 => (1, 0),
            2 => (1, -1),
            3 => (0, -1),
            4 => (-1, -1),
            5 => (-1, 0),
            6 => (-1, 1),
            _ => (0, 1),
        };
    }

    // The direction from the caster to the cell cast on, along an axis or exactly along a diagonal.
    private static (int X, int Y) Straight(MapPoint from, MapPoint to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;

        return dx is not 0 && Math.Abs(dx) == Math.Abs(dy) ? (Math.Sign(dx), Math.Sign(dy)) : Axis(from, to);
    }

    // The axis the cell cast on lies farthest along from the caster's; X on a tie, and east of the caster on the same cell.
    private static (int X, int Y) Axis(MapPoint from, MapPoint to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;

        if (dx is 0 && dy is 0)
            return (1, 0);

        return Math.Abs(dx) >= Math.Abs(dy) ? (Math.Sign(dx), 0) : (0, Math.Sign(dy));
    }

    private static void Add(ref CellSet cells, int x, int y)
    {
        if (MapGeometry.TryGetCell(new MapPoint(x, y), out var cell))
            cells.Add(cell);
    }

    private static void Diamond(ref CellSet cells, MapPoint origin, int least, int most, bool everyOther)
    {
        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            var point = MapGeometry.PointOf(cell);
            var distance = Math.Abs(point.X - origin.X) + Math.Abs(point.Y - origin.Y);

            if (distance >= least && distance <= most && (!everyOther || distance % 2 is 0))
                cells.Add(cell);
        }
    }

    private static void Square(ref CellSet cells, MapPoint origin, int least, int most, bool diagonals)
    {
        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            var point = MapGeometry.PointOf(cell);
            var dx = Math.Abs(point.X - origin.X);
            var dy = Math.Abs(point.Y - origin.Y);
            var distance = Math.Max(dx, dy);

            if (distance >= least && distance <= most && (diagonals || dx != dy || dx is 0))
                cells.Add(cell);
        }
    }

    private static void Star(ref CellSet cells, MapPoint origin, int least, int most, bool axes, bool diagonals)
    {
        if (least is 0)
            Add(ref cells, origin.X, origin.Y);

        for (var r = Math.Max(least, 1); r <= most && r < MapData.CellCount; r++)
        {
            if (axes)
            {
                Add(ref cells, origin.X + r, origin.Y);
                Add(ref cells, origin.X - r, origin.Y);
                Add(ref cells, origin.X, origin.Y + r);
                Add(ref cells, origin.X, origin.Y - r);
            }

            if (diagonals)
            {
                Add(ref cells, origin.X + r, origin.Y + r);
                Add(ref cells, origin.X - r, origin.Y - r);
                Add(ref cells, origin.X + r, origin.Y - r);
                Add(ref cells, origin.X - r, origin.Y + r);
            }
        }
    }

    private static void Ray(ref CellSet cells, MapPoint origin, (int X, int Y) direction, int first, int last, in CellSet fighters, bool stop)
    {
        for (var r = Math.Max(first, 0); r <= last && r < MapData.CellCount; r++)
        {
            if (!MapGeometry.TryGetCell(new MapPoint(origin.X + r * direction.X, origin.Y + r * direction.Y), out var cell))
                return;

            cells.Add(cell);

            if (stop && r > 0 && fighters.Contains(cell))
                return;
        }
    }

    private static void Across(ref CellSet cells, MapPoint origin, (int X, int Y) direction, int least, int most)
    {
        var across = (-direction.Y, direction.X);

        if (least is 0)
            Add(ref cells, origin.X, origin.Y);

        for (var r = Math.Max(least, 1); r <= most && r < MapData.CellCount; r++)
        {
            Add(ref cells, origin.X + r * across.Item1, origin.Y + r * across.Item2);
            Add(ref cells, origin.X - r * across.Item1, origin.Y - r * across.Item2);
        }
    }

    private static void Cone(ref CellSet cells, MapPoint origin, (int X, int Y) axis, int least, int most)
    {
        var across = (X: -axis.Y, Y: axis.X);

        for (var forward = 0; forward <= most && forward < MapData.CellCount; forward++)
        {
            for (var side = -forward; side <= forward; side++)
            {
                if (forward + Math.Abs(side) >= least)
                    Add(ref cells, origin.X + forward * axis.X + side * across.X, origin.Y + forward * axis.Y + side * across.Y);
            }
        }
    }

    private static void Arms(ref CellSet cells, MapPoint origin, (int X, int Y) axis, int most, int forward, bool middle)
    {
        var across = (X: -axis.Y, Y: axis.X);

        Add(ref cells, origin.X, origin.Y);

        for (var r = 1; r <= most && r < MapData.CellCount; r++)
        {
            var x = origin.X + forward * r * axis.X;
            var y = origin.Y + forward * r * axis.Y;

            Add(ref cells, x + r * across.X, y + r * across.Y);
            Add(ref cells, x - r * across.X, y - r * across.Y);

            if (middle)
                Add(ref cells, x, y);
        }
    }

    private static void Rectangle(ref CellSet cells, MapPoint origin, (int X, int Y) axis, int length, int width)
    {
        var across = (X: -axis.Y, Y: axis.X);
        var side = Math.Max(width, 1);

        for (var forward = 0; forward < Math.Max(length, 1) && forward < MapData.CellCount; forward++)
        {
            for (var offset = 0; offset < side && offset < MapData.CellCount; offset++)
            {
                var b = offset - side / 2;

                Add(ref cells, origin.X + forward * axis.X + b * across.X, origin.Y + forward * axis.Y + b * across.Y);
            }
        }
    }

    private static void KeepSeen(ref CellSet cells, FightMap map, in CellSet fighters, int centre)
    {
        foreach (var cell in cells)
        {
            if (!LineOfSight.IsClear(map, fighters, centre, cell))
                cells.Remove(cell);
        }
    }
}
