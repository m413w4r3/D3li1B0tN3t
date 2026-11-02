using System.Numerics;
using System.Runtime.InteropServices;

namespace D3B.Data.Maps;

/// <summary>A cell of a map: whether it can be walked on, how it leaves the map, and which zone it belongs to.</summary>
/// <remarks>
/// <para>
/// Every field is sized after what client 3.7.12.12 holds (client.md), and the constructor rejects a value that does not
/// fit, so that a client widening a field fails the extraction instead of being misread.
/// </para>
/// <para>
/// The struct is unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack
/// format (D25), and packing leaves no padding whose bytes could differ between two extractions.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MapCell : IEquatable<MapCell>
{
    /// <summary>The number of directions: 0 east, then clockwise to 7.</summary>
    public const int DirectionCount = 8;

    private const MapCellProperties AllProperties = (MapCellProperties)((1 << 10) - 1);

    /// <summary>Gets the cell's properties.</summary>
    public MapCellProperties Properties { get; }

    /// <summary>Gets the client's <c>altitude</c>, or <c>floor</c> in maps of the <c>MapMetadata</c> layout.</summary>
    public short Floor { get; }

    /// <summary>Gets the client's <c>speed</c>: 0 on every cell of client 3.7.12.12.</summary>
    public sbyte Speed { get; }

    /// <summary>Gets the directions in which a character standing on the cell may leave the map: bit <c>d</c> for direction <c>d</c>.</summary>
    /// <remarks>Directions run clockwise from east: 0 east, 2 south, 4 west, 6 north, odd numbers the diagonals between.</remarks>
    public byte MapChangeData { get; }

    /// <summary>Gets the client's <c>moveZone</c>, 0 or 1; what it means is not measured yet.</summary>
    public byte MoveZone { get; }

    /// <summary>Gets the client's <c>linkedZone</c>: the roleplay zone in its high four bits, see <see cref="Zone"/>.</summary>
    public byte LinkedZone { get; }

    /// <summary>Gets the client's <c>arrow</c>, from 0 to 10; what it means is not measured yet.</summary>
    public byte Arrow { get; }

    /// <summary>Gets whether a character may stand on the cell outside fights.</summary>
    public bool IsWalkable => (Properties & (MapCellProperties.Walkable | MapCellProperties.NonWalkableDuringRoleplay)) == MapCellProperties.Walkable;

    /// <summary>Gets the roleplay zone of the cell: the zone of the map's vertex in the world graph that the cell belongs to.</summary>
    /// <remarks>0 on cells that cannot be walked on. The cells of one zone reach one another; the world graph links zones.</remarks>
    public int Zone => LinkedZone >> 4;

    /// <summary>Initializes a new instance of the <see cref="MapCell"/> struct.</summary>
    /// <param name="properties">The cell's properties.</param>
    /// <param name="floor">The client's altitude or floor.</param>
    /// <param name="speed">The client's speed.</param>
    /// <param name="mapChangeData">The directions in which the cell leaves the map.</param>
    /// <param name="moveZone">The client's moveZone.</param>
    /// <param name="linkedZone">The client's linkedZone.</param>
    /// <param name="arrow">The client's arrow.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value does not fit its field.</exception>
    public MapCell(MapCellProperties properties, int floor, int speed, int mapChangeData, int moveZone, int linkedZone, int arrow)
    {
        if ((properties & ~AllProperties) is not MapCellProperties.None)
            throw new ArgumentOutOfRangeException(nameof(properties), properties, "Unknown cell properties.");

        Properties = properties;
        Floor = Fit<short>(floor, short.MinValue, short.MaxValue, nameof(floor));
        Speed = Fit<sbyte>(speed, sbyte.MinValue, sbyte.MaxValue, nameof(speed));
        MapChangeData = Fit<byte>(mapChangeData, byte.MinValue, byte.MaxValue, nameof(mapChangeData));
        MoveZone = Fit<byte>(moveZone, byte.MinValue, byte.MaxValue, nameof(moveZone));
        LinkedZone = Fit<byte>(linkedZone, byte.MinValue, byte.MaxValue, nameof(linkedZone));
        Arrow = Fit<byte>(arrow, byte.MinValue, byte.MaxValue, nameof(arrow));
    }

    private static T Fit<T>(int value, int min, int max, string name)
        where T : struct, IBinaryInteger<T>
    {
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(name, value, $"A cell's {name} must lie between {min} and {max}.");

        return T.CreateTruncating(value);
    }

    /// <summary>Tells whether a character standing on the cell may leave the map in a direction.</summary>
    /// <param name="direction">A direction, from 0 (east) clockwise to 7.</param>
    /// <returns>Whether the cell leaves the map that way.</returns>
    public bool LeavesMapTowards(int direction)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(direction);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(direction, DirectionCount);

        return (MapChangeData & (1 << direction)) is not 0;
    }

    /// <inheritdoc/>
    public bool Equals(MapCell other)
    {
        return Properties == other.Properties && Floor == other.Floor && Speed == other.Speed && MapChangeData == other.MapChangeData
            && MoveZone == other.MoveZone && LinkedZone == other.LinkedZone && Arrow == other.Arrow;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is MapCell other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Properties, Floor, Speed, MapChangeData, MoveZone, LinkedZone, Arrow);
    }

    /// <summary>Compares two cells.</summary>
    /// <param name="left">A cell.</param>
    /// <param name="right">Another cell.</param>
    /// <returns>Whether every field is equal.</returns>
    public static bool operator ==(MapCell left, MapCell right)
    {
        return left.Equals(right);
    }

    /// <summary>Compares two cells.</summary>
    /// <param name="left">A cell.</param>
    /// <param name="right">Another cell.</param>
    /// <returns>Whether a field differs.</returns>
    public static bool operator !=(MapCell left, MapCell right)
    {
        return !left.Equals(right);
    }
}
