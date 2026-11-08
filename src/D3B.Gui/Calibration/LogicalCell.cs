using D3B.Data.Maps;

namespace D3B.Gui.Calibration;

/// <summary>A Dofus map cell identifier.</summary>
public readonly record struct LogicalCell
{
    /// <summary>Gets the cell identifier.</summary>
    public int Value { get; }

    /// <summary>Initializes a cell identifier.</summary>
    /// <param name="value">The cell identifier, from zero to <see cref="MapData.CellCount"/> minus one.</param>
    /// <exception cref="ArgumentOutOfRangeException">The identifier is outside the map's cell range.</exception>
    public LogicalCell(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, MapData.CellCount);

        Value = value;
    }
}
