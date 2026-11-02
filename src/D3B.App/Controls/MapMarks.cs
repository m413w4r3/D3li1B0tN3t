using Avalonia;
using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>Draws the marks of the map, the same on the map and in its legend (D121).</summary>
internal static class MapMarks
{
    /// <summary>Draws a mark.</summary>
    /// <param name="context">What to draw on.</param>
    /// <param name="palette">The theme's colours.</param>
    /// <param name="mark">What is drawn.</param>
    /// <param name="center">Where its centre lies.</param>
    /// <param name="cellHeight">How high a cell is: the marks are sized after it.</param>
    public static void Draw(DrawingContext context, MapPalette palette, MapMark mark, Point center, double cellHeight)
    {
        var radius = Math.Max(2.5, cellHeight * 0.3);
        var brush = palette.Of(mark);

        switch (mark)
        {
            case MapMark.Character:
                context.DrawEllipse(brush, new Pen(palette.CharacterRing, 1.5), center, radius * 1.35, radius * 1.35);
                break;
            case MapMark.Resource or MapMark.Harvested:
                context.DrawEllipse(brush, null, center, radius, radius);
                break;
            case MapMark.Unknown:
                context.DrawEllipse(brush, null, center, radius * 0.7, radius * 0.7);
                break;
            case MapMark.Element or MapMark.Door:
                context.DrawGeometry(brush, null, Diamond(center, radius * 1.4, radius * 1.4));
                break;
            case MapMark.Npc:
                context.DrawGeometry(brush, null, Triangle(center, radius * 1.3));
                break;
            case MapMark.Bot:
                context.DrawEllipse(null, new Pen(brush, 2), center, radius * 1.3, radius * 1.3);
                break;
            case MapMark.Ally or MapMark.Enemy:
                context.DrawRectangle(brush, null, new Rect(center.X - (radius * 1.1), center.Y - (radius * 1.1), radius * 2.2, radius * 2.2), radius * 0.4, radius * 0.4);
                break;
            case MapMark.ChallengerCell or MapMark.DefenderCell:
                context.DrawGeometry(brush, null, Diamond(center, cellHeight, cellHeight / 2));
                break;
            default:
                context.DrawGeometry(palette.ExitFill, new Pen(palette.Exit, 1), Diamond(center, cellHeight, cellHeight / 2));
                break;
        }
    }

    /// <summary>Gets a diamond.</summary>
    /// <param name="center">Its centre.</param>
    /// <param name="halfWidth">Half its width.</param>
    /// <param name="halfHeight">Half its height.</param>
    /// <returns>The diamond.</returns>
    public static StreamGeometry Diamond(Point center, double halfWidth, double halfHeight)
    {
        var geometry = new StreamGeometry();

        using (var draw = geometry.Open())
            AddDiamond(draw, center, halfWidth, halfHeight);

        return geometry;
    }

    /// <summary>Adds a diamond to a geometry being drawn.</summary>
    /// <param name="draw">The geometry's context.</param>
    /// <param name="center">The diamond's centre.</param>
    /// <param name="halfWidth">Half its width.</param>
    /// <param name="halfHeight">Half its height.</param>
    public static void AddDiamond(StreamGeometryContext draw, Point center, double halfWidth, double halfHeight)
    {
        draw.BeginFigure(new Point(center.X, center.Y - halfHeight), true);
        draw.LineTo(new Point(center.X + halfWidth, center.Y));
        draw.LineTo(new Point(center.X, center.Y + halfHeight));
        draw.LineTo(new Point(center.X - halfWidth, center.Y));
        draw.EndFigure(true);
    }

    private static StreamGeometry Triangle(Point center, double size)
    {
        var geometry = new StreamGeometry();

        using (var draw = geometry.Open())
        {
            draw.BeginFigure(new Point(center.X, center.Y - size), true);
            draw.LineTo(new Point(center.X + size, center.Y + (size * 0.75)));
            draw.LineTo(new Point(center.X - size, center.Y + (size * 0.75)));
            draw.EndFigure(true);
        }

        return geometry;
    }
}
