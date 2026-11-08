using D3B.Gui.Windowing;

namespace D3B.Gui.Calibration;

/// <summary>A measured client-area rectangle and its desktop and window-local origins.</summary>
public readonly record struct ClientAreaGeometry
{
    /// <summary>Gets the client's measured desktop origin.</summary>
    public DesktopPixels OriginDesktop { get; }

    /// <summary>Gets the client's measured origin within the captured window.</summary>
    public ClientAreaPixels OriginInWindow { get; }

    /// <summary>Gets the measured client width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the measured client height in pixels.</summary>
    public int Height { get; }

    /// <summary>Creates a measured client-area rectangle.</summary>
    /// <param name="originDesktop">The desktop origin, which may be negative for a secondary monitor.</param>
    /// <param name="originInWindow">The origin within the captured window.</param>
    /// <param name="width">The measured width.</param>
    /// <param name="height">The measured height.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive or the window-local origin is negative.</exception>
    public ClientAreaGeometry(DesktopPixels originDesktop, ClientAreaPixels originInWindow, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(originInWindow.X);
        ArgumentOutOfRangeException.ThrowIfNegative(originInWindow.Y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        OriginDesktop = originDesktop;
        OriginInWindow = originInWindow;
        Width = width;
        Height = height;
    }
}
