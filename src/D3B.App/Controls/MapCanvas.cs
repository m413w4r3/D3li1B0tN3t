using System.Collections.Immutable;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D3B.Data.Maps;

namespace D3B.App.Controls;

/// <summary>A map of the pack drawn flat, cell by cell, with what stands on it in layers, and the cell under the pointer (D120 to D122).</summary>
/// <remarks>
/// <para>
/// The cells' shapes are built once per map and size, so that a refresh of the bot's state, four times a second (D94),
/// only draws them again with what moved. The colours are the theme's palette, so both themes draw from the same code.
/// </para>
/// <para>
/// Floors are not drawn: on a map with stairs, cells drawn at their height would cover one another. The zones' layer
/// tints a stair's cells instead, and the page says a cell's floor under the pointer.
/// </para>
/// </remarks>
internal sealed class MapCanvas : Control
{
    private const double LabelSize = 11;
    private const double LabelGap = 6;

    /// <summary>Defines the <see cref="Scene"/> property.</summary>
    public static readonly StyledProperty<MapScene?> SceneProperty = AvaloniaProperty.Register<MapCanvas, MapScene?>(nameof(Scene));

    /// <summary>Defines the <see cref="Layers"/> property.</summary>
    public static readonly StyledProperty<MapLayers> LayersProperty = AvaloniaProperty.Register<MapCanvas, MapLayers>(nameof(Layers), MapLayers.Default);

    /// <summary>Defines the <see cref="Palette"/> property.</summary>
    public static readonly StyledProperty<MapPalette?> PaletteProperty = AvaloniaProperty.Register<MapCanvas, MapPalette?>(nameof(Palette));

    /// <summary>Defines the <see cref="HoveredCell"/> property, bound both ways by default.</summary>
    public static readonly StyledProperty<int?> HoveredCellProperty = AvaloniaProperty.Register<MapCanvas, int?>(nameof(HoveredCell), defaultBindingMode: BindingMode.TwoWay);

    private MapData? _shapedMap;
    private IReadOnlyList<MapExit>? _shapedExits;
    private MapLayout _shapedLayout;
    private StreamGeometry? _walkable;
    private StreamGeometry? _obstacles;
    private StreamGeometry? _stairs;
    private StreamGeometry? _exits;
    private List<(int Zone, StreamGeometry Cells)> _zones = [];

    /// <summary>Gets or sets what is drawn.</summary>
    public MapScene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>Gets or sets the layers shown.</summary>
    public MapLayers Layers
    {
        get => GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    /// <summary>Gets or sets the theme's colours.</summary>
    public MapPalette? Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>Gets or sets the cell under the pointer, if the pointer is over one.</summary>
    public int? HoveredCell
    {
        get => GetValue(HoveredCellProperty);
        set => SetValue(HoveredCellProperty, value);
    }

    static MapCanvas()
    {
        AffectsRender<MapCanvas>(SceneProperty, LayersProperty, PaletteProperty, HoveredCellProperty);
        ClipToBoundsProperty.OverrideDefaultValue<MapCanvas>(true);
    }

    private static Point Middle(MapLayout layout, ImmutableArray<int> cells)
    {
        double x = 0, y = 0;

        foreach (var cell in cells)
        {
            var center = layout.Center(cell);

            x += center.X;
            y += center.Y;
        }

        return new Point(x / cells.Length, y / cells.Length);
    }

    private static StreamGeometry Cells(MapLayout layout, IEnumerable<int> cells)
    {
        var geometry = new StreamGeometry();

        using (var draw = geometry.Open())
        {
            foreach (var cell in cells)
                MapMarks.AddDiamond(draw, layout.Center(cell), layout.CellWidth / 2, layout.CellHeight / 2);
        }

        return geometry;
    }

    private static void DrawCharacter(DrawingContext context, MapPalette palette, MapScene scene, MapLayout layout)
    {
        if (scene.Path.Length > 1)
        {
            var path = new StreamGeometry();

            using (var draw = path.Open())
            {
                draw.BeginFigure(layout.Center(scene.Path[0]), false);

                for (var step = 1; step < scene.Path.Length; step++)
                    draw.LineTo(layout.Center(scene.Path[step]));

                draw.EndFigure(false);
            }

            context.DrawGeometry(null, new Pen(palette.Character, 2, new DashStyle([2.5, 1.6], 0), PenLineCap.Round, PenLineJoin.Round), path);
            context.DrawEllipse(null, new Pen(palette.Character, 1.5), layout.Center(scene.Path[^1]), layout.CellWidth * 0.3, layout.CellHeight * 0.3);
        }

        if (scene.Character is { } cell)
            MapMarks.Draw(context, palette, MapMark.Character, layout.Center(cell), layout.CellHeight);
    }

    private static void DrawIcon(DrawingContext context, IImage icon, bool harvested, Point center, double height)
    {
        // Centred on its cell, its drawing within the ring the Activity layer draws around a wanted element; harvested, faded.
        var size = height * 1.3;
        var bounds = new Rect(center.X - (size / 2), center.Y - (size / 2), size, size);

        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
        using (context.PushOpacity(harvested ? 0.35 : 1))
            context.DrawImage(icon, bounds);
    }

    private static void DrawActivity(DrawingContext context, MapPalette palette, MapActivity activity, MapLayout layout)
    {
        var height = layout.CellHeight;

        if (!activity.NextExit.IsDefaultOrEmpty)
            context.DrawGeometry(palette.ExitFill, new Pen(palette.Exit, 2), Cells(layout, activity.NextExit));

        // A ring around each element the activity wants, a brighter one around the one it goes for.
        foreach (var wanted in activity.Wanted)
            context.DrawEllipse(null, new Pen(palette.Resource, 1.5), layout.Center(wanted), height * 0.62, height * 0.62);

        if (activity.Target is { } target)
            context.DrawEllipse(null, new Pen(palette.Hover, 2), layout.Center(target), height * 0.8, height * 0.8);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        // A transparent ground, so that the pointer finds the canvas between the cells too.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        if (Scene is not { } scene || Palette is not { } palette)
            return;

        var layout = MapLayout.Fit(Bounds.Size);

        if (layout.CellWidth <= 0)
            return;

        Shape(scene, layout);

        var layers = Layers;
        var height = layout.CellHeight;

        context.DrawGeometry(palette.Walkable, layers.HasFlag(MapLayers.Cells) ? new Pen(palette.Grid, 1) : null, _walkable!);

        if (layers.HasFlag(MapLayers.Zones))
        {
            foreach (var (zone, cells) in _zones)
                context.DrawGeometry(palette.ZoneOf(zone), null, cells);

            if (_stairs is not null)
                context.DrawGeometry(palette.Stairs, null, _stairs);
        }

        if (layers.HasFlag(MapLayers.Cells))
            context.DrawGeometry(palette.Obstacle, null, _obstacles!);

        if (layers.HasFlag(MapLayers.Fight))
        {
            foreach (var cell in scene.ChallengerCells)
                MapMarks.Draw(context, palette, MapMark.ChallengerCell, layout.Center(cell), height);

            foreach (var cell in scene.DefenderCells)
                MapMarks.Draw(context, palette, MapMark.DefenderCell, layout.Center(cell), height);
        }

        if (layers.HasFlag(MapLayers.Exits))
        {
            context.DrawGeometry(palette.ExitFill, new Pen(palette.Exit, 1), _exits!);
            DrawLabels(context, palette, scene, layout);
        }

        foreach (var element in scene.Elements)
        {
            var shown = element.Mark is MapMark.Door ? layers.HasFlag(MapLayers.Elements) || layers.HasFlag(MapLayers.Exits) : layers.HasFlag(MapLayers.Elements);

            if (!shown)
                continue;

            if (element.Icon is { } icon)
                DrawIcon(context, icon, element.Mark is MapMark.Harvested, layout.Center(element.Cell), height);
            else
                MapMarks.Draw(context, palette, element.Mark, layout.Center(element.Cell), height);
        }

        if (layers.HasFlag(MapLayers.Activity) && scene.Activity is { } activity)
            DrawActivity(context, palette, activity, layout);

        if (layers.HasFlag(MapLayers.Npcs))
        {
            foreach (var cell in scene.Npcs)
                MapMarks.Draw(context, palette, MapMark.Npc, layout.Center(cell), height);
        }

        if (layers.HasFlag(MapLayers.Bots))
        {
            foreach (var cell in scene.Bots)
                MapMarks.Draw(context, palette, MapMark.Bot, layout.Center(cell), height);
        }

        if (layers.HasFlag(MapLayers.Fight))
        {
            foreach (var cell in scene.Allies)
                MapMarks.Draw(context, palette, MapMark.Ally, layout.Center(cell), height);

            foreach (var cell in scene.Enemies)
                MapMarks.Draw(context, palette, MapMark.Enemy, layout.Center(cell), height);
        }

        if (layers.HasFlag(MapLayers.Character))
            DrawCharacter(context, palette, scene, layout);

        if (HoveredCell is { } hovered)
            context.DrawGeometry(null, new Pen(palette.Hover, 1.5), MapMarks.Diamond(layout.Center(hovered), layout.CellWidth / 2, height / 2));
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var cell = Scene is null ? -1 : MapLayout.Fit(Bounds.Size).CellAt(e.GetPosition(this));

        SetCurrentValue(HoveredCellProperty, cell < 0 ? null : cell);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetCurrentValue(HoveredCellProperty, null);
    }

    private void Shape(MapScene scene, MapLayout layout)
    {
        if (ReferenceEquals(scene.Map, _shapedMap) && ReferenceEquals(scene.Exits, _shapedExits) && layout == _shapedLayout)
            return;

        var cells = scene.Map.Cells.Span;
        var walkable = new List<int>();
        var obstacles = new List<int>();
        var stairs = new List<int>();
        var zones = new SortedDictionary<int, List<int>>();
        var bothMoveZones = false;

        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            var mapCell = cells[cell];

            if (mapCell.IsWalkable)
            {
                walkable.Add(cell);
                bothMoveZones |= mapCell.MoveZone != cells[walkable[0]].MoveZone;

                if (!zones.TryGetValue(mapCell.Zone, out var zone))
                    zones.Add(mapCell.Zone, zone = []);

                zone.Add(cell);
            }
            else if ((mapCell.Properties & MapCellProperties.LineOfSight) is MapCellProperties.None)
            {
                obstacles.Add(cell);
            }
        }

        // A stair is a cell of moveZone 1 on a map holding both (D86); a map of one zone has nothing to tint.
        if (bothMoveZones)
        {
            foreach (var cell in walkable)
            {
                if (cells[cell].MoveZone is 1)
                    stairs.Add(cell);
            }
        }

        _walkable = Cells(layout, walkable);
        _obstacles = Cells(layout, obstacles);
        _stairs = stairs.Count > 0 ? Cells(layout, stairs) : null;
        _zones = zones.Count > 1 ? [.. zones.Select(zone => (zone.Key, Cells(layout, zone.Value)))] : [];
        _exits = Cells(layout, scene.Exits.Where(exit => exit.Kind is not MapExitKind.Door).SelectMany(exit => exit.Cells).Distinct());
        _shapedMap = scene.Map;
        _shapedExits = scene.Exits;
        _shapedLayout = layout;
    }

    private void DrawLabels(DrawingContext context, MapPalette palette, MapScene scene, MapLayout layout)
    {
        var typeface = new Typeface(GetValue(TextElement.FontFamilyProperty), FontStyle.Normal, FontWeight.Medium);
        var bounds = layout.Bounds;

        foreach (var exit in scene.Exits)
        {
            if (exit.Kind is not MapExitKind.Scroll || exit.Label.Length is 0 || exit.Cells.IsDefaultOrEmpty)
                continue;

            var text = new FormattedText(exit.Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, LabelSize, palette.Exit);
            var middle = Middle(layout, exit.Cells);
            var at = exit.Direction switch
            {
                0 => new Point(bounds.Right + LabelGap, middle.Y - (text.Height / 2)),
                2 => new Point(middle.X - (text.Width / 2), bounds.Bottom + (LabelGap / 2)),
                4 => new Point(bounds.Left - LabelGap - text.Width, middle.Y - (text.Height / 2)),
                _ => new Point(middle.X - (text.Width / 2), bounds.Top - (LabelGap / 2) - text.Height),
            };

            context.DrawText(text, at);
        }
    }
}
