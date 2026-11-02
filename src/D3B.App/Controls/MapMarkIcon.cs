using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>A mark of the map drawn on its own, for the map's legend, as the map draws it (D121).</summary>
internal sealed class MapMarkIcon : Control
{
    /// <summary>Defines the <see cref="Mark"/> property.</summary>
    public static readonly StyledProperty<MapMark> MarkProperty = AvaloniaProperty.Register<MapMarkIcon, MapMark>(nameof(Mark));

    /// <summary>Defines the <see cref="Palette"/> property.</summary>
    public static readonly StyledProperty<MapPalette?> PaletteProperty = AvaloniaProperty.Register<MapMarkIcon, MapPalette?>(nameof(Palette));

    /// <summary>Gets or sets what is drawn.</summary>
    public MapMark Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    /// <summary>Gets or sets the theme's colours.</summary>
    public MapPalette? Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    static MapMarkIcon()
    {
        AffectsRender<MapMarkIcon>(MarkProperty, PaletteProperty);
        WidthProperty.OverrideDefaultValue<MapMarkIcon>(18);
        HeightProperty.OverrideDefaultValue<MapMarkIcon>(12);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        if (Palette is not { } palette)
            return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);

        // The character's mark alone would read as any disc: a piece of its path beside it says what it is.
        if (Mark is MapMark.Character)
        {
            var path = new Pen(palette.Character, 1.5, new DashStyle([2.5, 1.6], 0), PenLineCap.Round);

            context.DrawLine(path, new Point(center.X, center.Y), new Point(Bounds.Width, center.Y));
            MapMarks.Draw(context, palette, Mark, new Point(center.X - 3, center.Y), Bounds.Height * 0.75);

            return;
        }

        MapMarks.Draw(context, palette, Mark, center, Mark is MapMark.Exit or MapMark.ChallengerCell or MapMark.DefenderCell ? Bounds.Height * 0.75 : Bounds.Height);
    }
}
