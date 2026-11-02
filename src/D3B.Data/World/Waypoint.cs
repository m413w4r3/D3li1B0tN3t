using System.Runtime.InteropServices;

namespace D3B.Data.World;

/// <summary>A zaap, from the client's <c>WaypointsDataRoot</c>: a map a character may teleport to once it knows the zaap.</summary>
/// <remarks>Unmanaged and packed, as <see cref="WorldVertex"/>. The world graph has no zaap transitions: a path through zaaps adds them from here.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Waypoint
{
    /// <summary>Gets the zaap's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the map it stands on.</summary>
    public long MapId { get; }

    /// <summary>Gets that map's sub-area.</summary>
    public int SubAreaId { get; }

    /// <summary>Gets the client's <c>activated</c>, false for 15 of the 62 zaaps of client 3.7.12.12; what it means is not measured yet.</summary>
    public bool Activated { get; }

    /// <summary>Initializes a new instance of the <see cref="Waypoint"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="mapId">Its map.</param>
    /// <param name="subAreaId">Its map's sub-area.</param>
    /// <param name="activated">The client's activated.</param>
    public Waypoint(int id, long mapId, int subAreaId, bool activated)
    {
        Id = id;
        MapId = mapId;
        SubAreaId = subAreaId;
        Activated = activated;
    }
}
