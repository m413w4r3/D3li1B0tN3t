namespace D3B.Gui.Windowing;

/// <summary>A pixel position within a captured image.</summary>
/// <param name="X">The horizontal pixel position.</param>
/// <param name="Y">The vertical pixel position.</param>
public readonly record struct WindowLocalPixels(int X, int Y);
