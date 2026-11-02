using System.Runtime.InteropServices;

namespace D3B.Data.World;

/// <summary>A vertex of the world graph: one zone of a map, the cells of which reach one another.</summary>
/// <remarks>Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct WorldVertex
{
    /// <summary>The highest zone a vertex may have: zones are the high four bits of a cell's <c>linkedZone</c>.</summary>
    public const int MaxZone = 15;

    /// <summary>Gets the map.</summary>
    public long MapId { get; }

    /// <summary>Gets the zone of the map, from 1 to <see cref="MaxZone"/>, which <see cref="Maps.MapCell.Zone"/> gives each cell.</summary>
    public int Zone { get; }

    /// <summary>Initializes a new instance of the <see cref="WorldVertex"/> struct.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="zone">The zone, from 1 to <see cref="MaxZone"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The zone is outside 1 to <see cref="MaxZone"/>.</exception>
    public WorldVertex(long mapId, int zone)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(zone, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(zone, MaxZone);

        MapId = mapId;
        Zone = zone;
    }
}
