using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>A zone's mini-map: each map a square at its coordinates on the world map, as players place a zone, drawn from the atlas (D127, D131).</summary>
/// <remarks>
/// The squares fill the room as a grid of equal cells, keeping them square; the zone's maps are coloured by their part or
/// by where a tour stands in them, those around dim, the probable ones marked (D138). No texture: the atlas's coordinates
/// say it all.
/// </remarks>
internal sealed class ZoneMapCanvas : Control
{
    private const double Gap = 0.16;
    private const double Corner = 0.2;

    /// <summary>The largest a map's square is drawn, so that a zone of a few maps still reads as a map, not as blocks.</summary>
    private const double MostSize = 30;

    /// <summary>Defines the <see cref="Scene"/> property.</summary>
    public static readonly StyledProperty<ZoneScene?> SceneProperty = AvaloniaProperty.Register<ZoneMapCanvas, ZoneScene?>(nameof(Scene));

    /// <summary>Defines the <see cref="Palette"/> property.</summary>
    public static readonly StyledProperty<MapPalette?> PaletteProperty = AvaloniaProperty.Register<ZoneMapCanvas, MapPalette?>(nameof(Palette));

    /// <summary>Gets or sets what is drawn.</summary>
    public ZoneScene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>Gets or sets the theme's colours, the map's own.</summary>
    public MapPalette? Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    static ZoneMapCanvas()
    {
        AffectsRender<ZoneMapCanvas>(SceneProperty, PaletteProperty);
        ClipToBoundsProperty.OverrideDefaultValue<ZoneMapCanvas>(true);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        if (Scene is not { Squares.Length: > 0 } scene || Palette is not { } palette)
            return;

        var points = scene.Squares.Select(square => (square.X, square.Y)).ToList();

        if (scene.Landmark is { } landmark)
            points.Add((landmark.X, landmark.Y));

        if (scene.Character is { } character)
            points.Add((character.X, character.Y));

        var (left, top) = (points.Min(point => point.X), points.Min(point => point.Y));
        var columns = points.Max(point => point.X) - left + 1;
        var rows = points.Max(point => point.Y) - top + 1;
        var size = Math.Min(MostSize, Math.Min(Bounds.Width / columns, Bounds.Height / rows));

        if (size <= 0)
            return;

        var origin = new Point((Bounds.Width - (size * columns)) / 2, (Bounds.Height - (size * rows)) / 2);
        var outline = new Pen(palette.Resource, Math.Max(1, size * 0.08));
        var unknown = new Pen(palette.Unknown, Math.Max(1, size * 0.08));

        Rect Square(int x, int y)
        {
            var inset = size * Gap / 2;

            return new Rect(origin.X + ((x - left) * size) + inset, origin.Y + ((y - top) * size) + inset, size - (2 * inset), size - (2 * inset));
        }

        foreach (var square in scene.Squares)
        {
            var (fill, pen) = square.Kind switch
            {
                ZoneSquareKind.GroupA or ZoneSquareKind.Visited => (palette.Resource, null),
                ZoneSquareKind.GroupB => (palette.Harvested, null),
                ZoneSquareKind.ToVisit => (palette.Obstacle, unknown),
                ZoneSquareKind.Probable => (palette.Harvested, outline),
                ZoneSquareKind.Empty => (palette.Grid, null),
                ZoneSquareKind.Unreachable => (palette.Grid, new Pen(palette.Door, Math.Max(1, size * 0.08))),
                ZoneSquareKind.Isolated => (palette.Grid, new Pen(palette.Door, Math.Max(1, size * 0.08), DashStyle.Dash)),
                ZoneSquareKind.Growing => (palette.Harvested, new Pen(palette.Element, Math.Max(1, size * 0.08))),
                _ => (palette.Walkable, (Pen?)null),
            };

            context.DrawRectangle(fill, pen, Square(square.X, square.Y), size * Corner, size * Corner);

            if (square.IsProbable)
                context.DrawEllipse(palette.Bot, null, Square(square.X, square.Y).Center, size * 0.16, size * 0.16);
        }

        if (scene.Landmark is { } bank)
        {
            var center = Square(bank.X, bank.Y).Center;

            context.DrawGeometry(palette.Door, null, MapMarks.Diamond(center, size * 0.38, size * 0.38));
        }

        if (scene.Character is { } at)
        {
            var center = Square(at.X, at.Y).Center;

            context.DrawEllipse(palette.Character, new Pen(palette.CharacterRing, Math.Max(1, size * 0.1)), center, size * 0.3, size * 0.3);
        }
    }
}
