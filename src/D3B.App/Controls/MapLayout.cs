using Avalonia;
using D3B.Engine.Maps;

namespace D3B.App.Controls;

/// <summary>Where a map's cells lie in a control: the map fitted to the control in its own proportions, flat, with room around it for the exits' labels (D122).</summary>
/// <remarks>
/// A cell is a diamond twice as wide as it is high, its row half a height below the one before, odd rows half a cell
/// east, as <see cref="MapGeometry"/> lays them out. On the diagonal axes of <see cref="MapPoint"/>, a diamond is a square,
/// so the cell under a point is the nearest whole coordinates.
/// </remarks>
/// <param name="Left">Where the map's left edge lies in the control.</param>
/// <param name="Top">Where the map's top edge lies in the control.</param>
/// <param name="CellWidth">How wide a cell is.</param>
internal readonly record struct MapLayout(double Left, double Top, double CellWidth)
{
    /// <summary>The room left and right of the map, for the labels of the east and west exits.</summary>
    public const double SideRoom = 64;

    /// <summary>The room above and below the map, for the labels of the north and south exits.</summary>
    public const double EdgeRoom = 20;

    /// <summary>How many cells wide the map is: a row of cells, and the half cell odd rows lie east.</summary>
    private const double WidthInCells = MapGeometry.Columns + 0.5;

    /// <summary>How many cells high the map is: a row every half height, and the half height of the last.</summary>
    private const double HeightInCells = (MapGeometry.Rows + 1) / 4.0;

    /// <summary>Gets how high a cell is.</summary>
    public double CellHeight => CellWidth / 2;

    /// <summary>Gets the rectangle the map's cells fill.</summary>
    public Rect Bounds => new(Left, Top, CellWidth * WidthInCells, CellWidth * HeightInCells);

    /// <summary>Fits a map to a control's size, centred, leaving room for the exits' labels.</summary>
    /// <param name="size">The control's size.</param>
    /// <returns>The layout, its cells of no width when the size leaves no room.</returns>
    public static MapLayout Fit(Size size)
    {
        var width = Math.Max(0, size.Width - (2 * SideRoom));
        var height = Math.Max(0, size.Height - (2 * EdgeRoom));
        var cellWidth = Math.Min(width / WidthInCells, height / HeightInCells);

        return new MapLayout((size.Width - (cellWidth * WidthInCells)) / 2, (size.Height - (cellWidth * HeightInCells)) / 2, cellWidth);
    }

    /// <summary>Gets the centre of a cell.</summary>
    /// <param name="cell">A cell of the map.</param>
    /// <returns>Its centre, in the control.</returns>
    public Point Center(int cell)
    {
        var row = cell / MapGeometry.Columns;
        var column = cell % MapGeometry.Columns;

        return new Point(Left + ((column + ((row % 2) * 0.5) + 0.5) * CellWidth), Top + ((row + 1) * CellHeight / 2));
    }

    /// <summary>Finds the cell under a point.</summary>
    /// <param name="point">A point of the control.</param>
    /// <returns>The cell, or -1 when the point lies off the map.</returns>
    public int CellAt(Point point)
    {
        if (CellWidth <= 0)
            return -1;

        // A cell's centre lies at u = (x + y) / 2 + 1/2 widths and v = (x - y + 1) / 2 heights, x and y its coordinates.
        var u = (point.X - Left) / CellWidth;
        var v = (point.Y - Top) / CellHeight;
        var x = (int)Math.Round(u + v - 1, MidpointRounding.AwayFromZero);
        var y = (int)Math.Round(u - v, MidpointRounding.AwayFromZero);

        return MapGeometry.TryGetCell(new MapPoint(x, y), out var cell) ? cell : -1;
    }
}
