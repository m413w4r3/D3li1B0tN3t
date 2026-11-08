namespace D3B.Gui.Windowing;

/// <summary>A pixel position within an X11 client area.</summary>
/// <param name="X">The horizontal pixel position.</param>
/// <param name="Y">The vertical pixel position.</param>
public readonly record struct ClientAreaPixels(int X, int Y);
