using Avalonia.Controls;
using Avalonia.Input;

namespace D3B.App.Views;

/// <summary>The map page: a bot's map drawn live, its layers, its legend, and what the cell under the pointer holds (D120 to D122).</summary>
internal sealed partial class MapView : UserControl
{
    private const double PointerGap = 16;

    /// <summary>Initializes a new instance of the <see cref="MapView"/> class.</summary>
    public MapView()
    {
        InitializeComponent();
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        var at = e.GetPosition(Surface);
        var size = HoverCard.Bounds.Size;

        // Beside the pointer, on the side the surface has room on, so that the card never hides the cell it describes.
        var left = at.X + PointerGap + size.Width <= Surface.Bounds.Width ? at.X + PointerGap : at.X - PointerGap - size.Width;
        var top = Math.Clamp(at.Y - (size.Height / 2), 0, Math.Max(0, Surface.Bounds.Height - size.Height));

        Canvas.SetLeft(HoverCard, Math.Max(0, left));
        Canvas.SetTop(HoverCard, top);
    }
}
