namespace D3B.Gui.Windowing;

/// <summary>A global pixel position on the desktop, which can be negative on a multi-monitor layout.</summary>
/// <param name="X">The horizontal pixel position.</param>
/// <param name="Y">The vertical pixel position.</param>
public readonly record struct DesktopPixels(int X, int Y);
