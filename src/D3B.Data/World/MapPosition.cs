using System.Runtime.InteropServices;

namespace D3B.Data.World;

/// <summary>Where a map lies in the world: its coordinates, sub-area and world map, from the client's <c>MapsInformationDataRoot</c>.</summary>
/// <remarks>Unmanaged and packed, as <see cref="WorldVertex"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MapPosition
{
    private const uint TeleportFlag = 1 << 3;

    /// <summary>Gets the map.</summary>
    public long MapId { get; }

    /// <summary>Gets the map's horizontal coordinate, as the game shows it in brackets.</summary>
    public short X { get; }

    /// <summary>Gets the map's vertical coordinate.</summary>
    public short Y { get; }

    /// <summary>Gets the sub-area the map belongs to.</summary>
    public int SubAreaId { get; }

    /// <summary>Gets the world map the coordinates are on, or -1 for maps shown on none, such as interiors.</summary>
    public sbyte WorldMap { get; }

    /// <summary>Gets the text naming the map, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets the client's <c>m_flags</c>: bit 3 is measured (<see cref="AllowsTeleport"/>), and bit 2, which lets the haven bag open; the others are not (client.md).</summary>
    public uint Flags { get; }

    /// <summary>Gets whether the server lets a character teleport from the map to a zaap it knows: refused on a map without bit 3, taken from six with it (client.md).</summary>
    public bool AllowsTeleport => (Flags & TeleportFlag) != 0;

    /// <summary>Initializes a new instance of the <see cref="MapPosition"/> struct.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="x">Its horizontal coordinate.</param>
    /// <param name="y">Its vertical coordinate.</param>
    /// <param name="subAreaId">Its sub-area.</param>
    /// <param name="worldMap">Its world map, or -1.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="flags">The client's flags.</param>
    public MapPosition(long mapId, short x, short y, int subAreaId, sbyte worldMap, int nameId, uint flags)
    {
        MapId = mapId;
        X = x;
        Y = y;
        SubAreaId = subAreaId;
        WorldMap = worldMap;
        NameId = nameId;
        Flags = flags;
    }
}
