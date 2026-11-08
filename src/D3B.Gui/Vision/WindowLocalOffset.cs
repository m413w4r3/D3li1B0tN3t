namespace D3B.Gui.Vision;

/// <summary>A finite pixel offset relative to a point in one captured window.</summary>
public readonly record struct WindowLocalOffset
{
    /// <summary>The largest absolute component accepted for one measured offset.</summary>
    public const double MaximumMagnitudePixels = 8192;

    /// <summary>Gets the horizontal offset in pixels.</summary>
    public double X { get; }

    /// <summary>Gets the vertical offset in pixels.</summary>
    public double Y { get; }

    /// <summary>Creates a finite, bounded pixel offset.</summary>
    public WindowLocalOffset(double x, double y)
    {
        if (!double.IsFinite(x) || Math.Abs(x) > MaximumMagnitudePixels)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "The horizontal offset must be finite and within the supported pixel bound.");
        }

        if (!double.IsFinite(y) || Math.Abs(y) > MaximumMagnitudePixels)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "The vertical offset must be finite and within the supported pixel bound.");
        }

        X = x;
        Y = y;
    }
}
