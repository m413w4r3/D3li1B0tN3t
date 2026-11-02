using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Rendering;
using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>A line icon drawn from a geometry on a grid of 24 units, in the colour of the text around it.</summary>
/// <remarks>
/// The geometries are Lucide's, in <c>Themes/Icons.axaml</c> (D161); a button's icon takes the button's text colour.
/// </remarks>
internal sealed class Icon : Control, ICustomHitTest
{
    /// <summary>The geometry's grid: an icon of any size draws it scaled from 24 units.</summary>
    private const double Grid = 24;

    /// <summary>Defines the <see cref="Data"/> property.</summary>
    public static readonly StyledProperty<Geometry?> DataProperty = AvaloniaProperty.Register<Icon, Geometry?>(nameof(Data));

    /// <summary>Defines the <see cref="Foreground"/> property, inherited as text's is.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Icon>();

    /// <summary>Defines the <see cref="StrokeThickness"/> property.</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<Icon, double>(nameof(StrokeThickness), 2);

    /// <summary>Defines the <see cref="Tone"/> property.</summary>
    public static readonly StyledProperty<Tone> ToneProperty = AvaloniaProperty.Register<Icon, Tone>(nameof(Tone));

    /// <summary>Gets or sets the icon's lines, on a grid of 24 units.</summary>
    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>Gets or sets the colour of the icon's lines.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets or sets the thickness of the icon's lines, in units of its grid.</summary>
    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Gets or sets the tone the icon's lines take, as <c>Themes/Styles.axaml</c> colours each; neutral keeps the text's colour.</summary>
    public Tone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    static Icon()
    {
        AffectsRender<Icon>(DataProperty, ForegroundProperty, StrokeThicknessProperty);
        WidthProperty.OverrideDefaultValue<Icon>(16);
        HeightProperty.OverrideDefaultValue<Icon>(16);
    }

    /// <inheritdoc/>
    public bool HitTest(Point point)
    {
        // An icon may carry a tooltip, such as a setting's help: its whole square answers the pointer, not its lines alone.
        return new Rect(Bounds.Size).Contains(point);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Data is not { } data || Foreground is not { } foreground)
            return;

        var scale = Math.Min(Bounds.Width, Bounds.Height) / Grid;
        var pen = new Pen(foreground, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
            context.DrawGeometry(null, pen, data);
    }
}
