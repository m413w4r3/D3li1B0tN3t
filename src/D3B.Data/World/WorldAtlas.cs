using MemoryPack;

namespace D3B.Data.World;

/// <summary>Where maps lie in the world: their positions, the sub-areas and areas they belong to, and the zaaps; what grows and lives in each sub-area.</summary>
/// <remarks>
/// Each table is sorted by identifier and found by binary search. The client's tables do not cover every map: about
/// 2,300 maps of client 3.7.12.12 have no position (client.md). Loaded once and shared: nothing in it changes.
/// </remarks>
[MemoryPackable]
public sealed partial class WorldAtlas
{
    /// <summary>The name of the pack entry holding the atlas.</summary>
    public const string EntryName = "world/atlas";

    /// <summary>Gets the maps' positions, in ascending order of map.</summary>
    public ReadOnlyMemory<MapPosition> Positions { get; }

    /// <summary>Gets the sub-areas, in ascending order of identifier.</summary>
    public ReadOnlyMemory<SubArea> SubAreas { get; }

    /// <summary>Gets the areas, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Area> Areas { get; }

    /// <summary>Gets the zaaps, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Waypoint> Waypoints { get; }

    /// <summary>Gets the resources of every sub-area, by item: each sub-area's together, ascending, in the order of the sub-areas.</summary>
    /// <remarks>
    /// The items a profession's skill gathers that the client lists in a sub-area's <c>harvestables</c>, which also lists
    /// what harvesting may bring besides, such as loot boxes (client.md).
    /// </remarks>
    public ReadOnlyMemory<int> Harvestables { get; }

    /// <summary>Gets the monsters of every sub-area, by monster: each sub-area's together, ascending, in the order of the sub-areas.</summary>
    /// <remarks>
    /// The client's <c>monsters</c> of a sub-area, every one a monster of <see cref="D3B.Data.Monsters.MonsterCatalog"/>.
    /// The monsters' own <c>subareas</c> list every one of these pairs, and more: monsters of events and quests, such as
    /// Pwâk eggs and leprechauns, which no sub-area lists and the atlas leaves out. Bosses, archmonsters and monsters hidden
    /// from the bestiary are kept: their flags tell them apart.
    /// </remarks>
    public ReadOnlyMemory<int> Monsters { get; }

    /// <summary>Initializes a new instance of the <see cref="WorldAtlas"/> class.</summary>
    /// <param name="positions">The maps' positions, in strictly ascending order of map.</param>
    /// <param name="subAreas">The sub-areas, in strictly ascending order of identifier, each naming its own resources and monsters.</param>
    /// <param name="areas">The areas, in strictly ascending order of identifier.</param>
    /// <param name="waypoints">The zaaps, in strictly ascending order of identifier.</param>
    /// <param name="harvestables">The sub-areas' resources, each sub-area's together and in the order of the sub-areas, with nothing between.</param>
    /// <param name="monsters">The sub-areas' monsters, each sub-area's together and in the order of the sub-areas, with nothing between.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order, or the sub-areas' resources or monsters do not follow one another.</exception>
    [MemoryPackConstructor]
    public WorldAtlas(ReadOnlyMemory<MapPosition> positions, ReadOnlyMemory<SubArea> subAreas, ReadOnlyMemory<Area> areas, ReadOnlyMemory<Waypoint> waypoints, ReadOnlyMemory<int> harvestables, ReadOnlyMemory<int> monsters)
    {
        Tables.CheckAscending(positions.Span, static position => position.MapId, nameof(positions));
        Tables.CheckAscending(subAreas.Span, static subArea => subArea.Id, nameof(subAreas));
        Tables.CheckAscending(areas.Span, static area => area.Id, nameof(areas));
        Tables.CheckAscending(waypoints.Span, static waypoint => waypoint.Id, nameof(waypoints));
        Tables.CheckSlices(subAreas.Span, static subArea => (subArea.FirstHarvestable, subArea.HarvestableCount), harvestables.Length, nameof(harvestables));
        Tables.CheckSlices(subAreas.Span, static subArea => (subArea.FirstMonster, subArea.MonsterCount), monsters.Length, nameof(monsters));

        Positions = positions;
        SubAreas = subAreas;
        Areas = areas;
        Waypoints = waypoints;
        Harvestables = harvestables;
        Monsters = monsters;
    }

    /// <summary>Finds where a map lies.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="position">Its position, when found.</param>
    /// <returns>Whether the client gives the map a position.</returns>
    public bool TryGetPosition(long mapId, out MapPosition position)
    {
        return Tables.TryFind(Positions.Span, mapId, static position => position.MapId, out position);
    }

    /// <summary>Finds a sub-area.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="subArea">The sub-area, when found.</param>
    /// <returns>Whether the atlas holds it.</returns>
    public bool TryGetSubArea(int id, out SubArea subArea)
    {
        return Tables.TryFind(SubAreas.Span, id, static subArea => subArea.Id, out subArea);
    }

    /// <summary>Finds an area.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="area">The area, when found.</param>
    /// <returns>Whether the atlas holds it.</returns>
    public bool TryGetArea(int id, out Area area)
    {
        return Tables.TryFind(Areas.Span, id, static area => area.Id, out area);
    }

    /// <summary>Finds a zaap.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="waypoint">The zaap, when found.</param>
    /// <returns>Whether the atlas holds it.</returns>
    public bool TryGetWaypoint(int id, out Waypoint waypoint)
    {
        return Tables.TryFind(Waypoints.Span, id, static waypoint => waypoint.Id, out waypoint);
    }

    /// <summary>Gets the resources of a sub-area.</summary>
    /// <param name="subArea">A sub-area of the atlas.</param>
    /// <returns>The items professions gather there, in ascending order.</returns>
    public ReadOnlySpan<int> HarvestablesOf(SubArea subArea)
    {
        return Harvestables.Span.Slice(subArea.FirstHarvestable, subArea.HarvestableCount);
    }

    /// <summary>Gets the monsters that live in a sub-area.</summary>
    /// <param name="subArea">A sub-area of the atlas.</param>
    /// <returns>Its monsters, by identifier, in ascending order.</returns>
    public ReadOnlySpan<int> MonstersOf(SubArea subArea)
    {
        return Monsters.Span.Slice(subArea.FirstMonster, subArea.MonsterCount);
    }
}
