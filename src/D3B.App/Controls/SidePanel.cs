using Avalonia;
using Avalonia.Controls;

namespace D3B.App.Controls;

/// <summary>Lays out its first child, the main part, and its second, a side part at its own width beside it on the right, or under it once the main part would be narrower than its least width.</summary>
/// <remarks>
/// A profile's settings and their preview (D246): beside each other in a wide window, the preview under the settings in a
/// narrow one, where beside them it squeezed the settings' names and controls until they ran over each other. A side part
/// not visible takes no room.
/// </remarks>
internal sealed class SidePanel : Panel
{
    /// <summary>Defines the <see cref="SideWidth"/> property.</summary>
    public static readonly StyledProperty<double> SideWidthProperty = AvaloniaProperty.Register<SidePanel, double>(nameof(SideWidth), 320);

    /// <summary>Defines the <see cref="MinMainWidth"/> property.</summary>
    public static readonly StyledProperty<double> MinMainWidthProperty = AvaloniaProperty.Register<SidePanel, double>(nameof(MinMainWidth), 520);

    /// <summary>Defines the <see cref="Spacing"/> property.</summary>
    public static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<SidePanel, double>(nameof(Spacing), 24);

    /// <summary>Gets or sets the side part's width, beside the main part or under it.</summary>
    public double SideWidth
    {
        get => GetValue(SideWidthProperty);
        set => SetValue(SideWidthProperty, value);
    }

    /// <summary>Gets or sets the narrowest the main part may be with the side part beside it.</summary>
    public double MinMainWidth
    {
        get => GetValue(MinMainWidthProperty);
        set => SetValue(MinMainWidthProperty, value);
    }

    /// <summary>Gets or sets the space between the two parts.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private Control? Main => Children.Count > 0 ? Children[0] : null;

    private Control? Side => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;

    static SidePanel()
    {
        AffectsMeasure<SidePanel>(SideWidthProperty, MinMainWidthProperty, SpacingProperty);
    }

    /// <summary>Tells whether the parts stand beside each other at a width.</summary>
    /// <param name="width">The width the panel has.</param>
    /// <returns>Whether they do; with no side part shown, they do.</returns>
    public bool IsBeside(double width)
    {
        return Side is null || width >= MinMainWidth + Spacing + SideWidth;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinMainWidth + Spacing + SideWidth : availableSize.Width;
        var main = Main;
        var side = Side;

        if (IsBeside(width))
        {
            var mainWidth = side is null ? width : width - Spacing - SideWidth;

            main?.Measure(new Size(mainWidth, availableSize.Height));
            side?.Measure(new Size(SideWidth, availableSize.Height));

            return new Size(width, Math.Max(main?.DesiredSize.Height ?? 0, side?.DesiredSize.Height ?? 0));
        }

        main?.Measure(new Size(width, double.PositiveInfinity));
        side!.Measure(new Size(width, double.PositiveInfinity));

        return new Size(width, (main?.DesiredSize.Height ?? 0) + Spacing + side.DesiredSize.Height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var main = Main;
        var side = Side;

        if (IsBeside(finalSize.Width))
        {
            var mainWidth = side is null ? finalSize.Width : finalSize.Width - Spacing - SideWidth;

            main?.Arrange(new Rect(0, 0, mainWidth, finalSize.Height));
            side?.Arrange(new Rect(mainWidth + Spacing, 0, SideWidth, side.DesiredSize.Height));

            return finalSize;
        }

        var mainHeight = main?.DesiredSize.Height ?? 0;

        main?.Arrange(new Rect(0, 0, finalSize.Width, mainHeight));
        side!.Arrange(new Rect(0, mainHeight + Spacing, finalSize.Width, side.DesiredSize.Height));

        return finalSize;
    }
}
