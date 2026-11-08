using D3B.Engine.Maps;
using D3B.Gui.Windowing;

namespace D3B.Gui.Calibration;

/// <summary>A measured screen annotation paired with one logical map cell.</summary>
public sealed record CalibrationAnchor
{
    /// <summary>Gets the logical cell.</summary>
    public LogicalCell Cell { get; }

    /// <summary>Gets the logical point from <see cref="MapGeometry.PointOf(int)"/>.</summary>
    public MapPoint LogicalPoint { get; }

    /// <summary>Gets the manually measured point in captured-window pixels.</summary>
    public WindowLocalPixels MeasuredPixelPoint { get; }

    /// <summary>Gets the independent fitting or validation role.</summary>
    public CalibrationAnchorRole Role { get; }

    /// <summary>Gets the provenance of the screen annotation.</summary>
    public CalibrationAnchorSource Source { get; }

    /// <summary>Creates an anchor and verifies its logical map geometry.</summary>
    /// <param name="cell">The identified map cell.</param>
    /// <param name="logicalPoint">The logical point for that cell.</param>
    /// <param name="measuredPixelPoint">The manually measured screen point.</param>
    /// <param name="role">Whether the point is used for fitting or validation.</param>
    /// <param name="source">The point's provenance.</param>
    /// <exception cref="ArgumentException">The logical point does not match the cell.</exception>
    public CalibrationAnchor(
        LogicalCell cell,
        MapPoint logicalPoint,
        WindowLocalPixels measuredPixelPoint,
        CalibrationAnchorRole role,
        CalibrationAnchorSource source)
    {
        var expected = MapGeometry.PointOf(cell.Value);

        if (logicalPoint != expected)
            throw new ArgumentException("The logical point must equal MapGeometry.PointOf(cell.Value).", nameof(logicalPoint));

        Cell = cell;
        LogicalPoint = logicalPoint;
        MeasuredPixelPoint = measuredPixelPoint;
        Role = role;
        Source = source;
    }
}
