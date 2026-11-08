using D3B.Gui.Windowing;

namespace D3B.Gui.Vision;

/// <summary>A rectangle measured in pixels relative to one captured window.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The rectangle width.</param>
/// <param name="Height">The rectangle height.</param>
/// <remarks>No conversion to desktop or logical map coordinates is implicit.</remarks>
public readonly record struct WindowLocalRect(int X, int Y, int Width, int Height)
{
    /// <summary>Gets whether a window-local pixel lies inside this half-open rectangle.</summary>
    public bool Contains(WindowLocalPixels point)
    {
        return Width > 0 && Height > 0
            && point.X >= X && point.Y >= Y
            && (long)point.X < (long)X + Width
            && (long)point.Y < (long)Y + Height;
    }

    /// <summary>Gets whether another positive-size rectangle is wholly contained in this rectangle.</summary>
    public bool Contains(WindowLocalRect other)
    {
        return Width > 0 && Height > 0 && other.Width > 0 && other.Height > 0
            && other.X >= X && other.Y >= Y
            && (long)other.X + other.Width <= (long)X + Width
            && (long)other.Y + other.Height <= (long)Y + Height;
    }

    /// <summary>Gets whether two positive-size half-open rectangles overlap.</summary>
    public bool Intersects(WindowLocalRect other)
    {
        return Width > 0 && Height > 0 && other.Width > 0 && other.Height > 0
            && (long)X < (long)other.X + other.Width
            && (long)other.X < (long)X + Width
            && (long)Y < (long)other.Y + other.Height
            && (long)other.Y < (long)Y + Height;
    }
}
