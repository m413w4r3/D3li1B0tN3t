using System.Collections.Frozen;
using System.Collections.Immutable;
using D3B.Bot.Interactives;
using D3B.Bot.Movement;
using D3B.Data.Jobs;
using D3B.Data.Maps;
using D3B.Data.World;

namespace D3B.Bot.Harvesting;

/// <summary>What harvesting settings lead to in the pack: the skills to use, the sub-areas and the maps to go through (D127).</summary>
/// <remarks>
/// <para>
/// The pack does not say which maps hold which resources: a map's element names only its graphic, and the server says what
/// it is once a character enters the map (client.md). So the zone is every map of its sub-areas a character may travel
/// to, and the activity learns which hold the resources by going there (<see cref="HarvestSightings"/>), or foresees it
/// from what the host learned of its elements' graphics elsewhere (<see cref="Foresee"/>, D138).
/// </para>
/// <para>
/// The resources are those listed, the first first, or every one of the professions chosen but those left out, the nearest
/// first or the highest level first (D165).
/// </para>
/// <para>
/// An area alone stands for its sub-areas that list one of the resources among their harvestables, or hold a map the learned
/// graphics place one of <c>Base</c>'s on (D150); sub-areas chosen stand for themselves, of any area; a place the activity
/// chooses for the character, its sub-areas (D166).
/// </para>
/// <para>
/// A sub-area may fall apart through its own maps: those reached from its main part only far round are isolated, and a tour
/// leaves them out unless the settings say otherwise (D149).
/// </para>
/// </remarks>
public sealed class HarvestZone
{
    /// <summary>Gets the skills to use, by id: those gathering a resource asked for, <c>Base</c>'s included.</summary>
    public FrozenDictionary<int, HarvestTarget> Targets { get; }

    /// <summary>Gets every skill gathering an item, the professions' and <c>Base</c>'s, by id, with the item it gathers: what a map is seen holding.</summary>
    public FrozenDictionary<int, int> Harvests { get; }

    /// <summary>Gets the resources asked for, by item.</summary>
    public FrozenSet<int> Wanted { get; }

    /// <summary>Gets the sub-areas of the zone, in the order the settings give them.</summary>
    public IReadOnlyList<int> SubAreas { get; }

    /// <summary>Gets the maps of the zone a character may travel to, each with the index of its sub-area in <see cref="SubAreas"/>.</summary>
    public FrozenDictionary<long, int> Maps { get; }

    /// <summary>Gets the zone's maps on no world map, such as houses and mines, which a tour goes through last (D128).</summary>
    public FrozenSet<long> Insides { get; }

    /// <summary>Gets the graphics of each map's interactive elements, as the pack draws them, each once.</summary>
    public FrozenDictionary<long, ImmutableArray<int>> Graphics { get; }

    /// <summary>Gets the zone's maps reachable from their sub-area's main part only by leaving it, more than <see cref="IsolatedMaps.MostDetour"/> changes away, teleports aside (D149).</summary>
    public FrozenSet<long> Isolated { get; }

    /// <summary>Gets whether a tour goes through the isolated maps too, last, rather than leave them out.</summary>
    public bool IncludesIsolated { get; }

    private HarvestZone(FrozenDictionary<int, HarvestTarget> targets, FrozenDictionary<int, int> harvests, FrozenSet<int> wanted, IReadOnlyList<int> subAreas, FrozenDictionary<long, int> maps, FrozenSet<long> insides, FrozenDictionary<long, ImmutableArray<int>> graphics, FrozenSet<long> isolated, bool includesIsolated)
    {
        Targets = targets;
        Harvests = harvests;
        Wanted = wanted;
        SubAreas = subAreas;
        Maps = maps;
        Insides = insides;
        Graphics = graphics;
        Isolated = isolated;
        IncludesIsolated = includesIsolated;
    }

    /// <summary>Reckons the zone of settings from the pack.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="atlas">The atlas, placing each map in its sub-area and listing each sub-area's resources.</param>
    /// <param name="graph">The world graph, holding the maps a character may travel to.</param>
    /// <param name="jobs">The professions' skills, each gathering a resource.</param>
    /// <param name="mapData">The maps, whose elements' graphics foretell what they hold.</param>
    /// <param name="placed">The maps the host's learned graphics place a resource of <c>Base</c> asked for on, which an area alone stands for with its sub-areas listing the others (D150).</param>
    /// <param name="chosen">The sub-areas the activity chose for the character, when it chooses the place (D166); the settings' area and sub-areas otherwise.</param>
    /// <returns>The zone, empty when the settings name no resource or no place.</returns>
    public static HarvestZone Of(HarvestSettings settings, WorldAtlas atlas, WorldGraph graph, JobCatalog jobs, GameMaps mapData, IReadOnlySet<long>? placed = null, IReadOnlyList<int>? chosen = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(mapData);

        var harvests = jobs.Skills.ToArray().Where(skill => skill.GatheredItemId != Skill.NoItem).ToList();
        var targets = TargetsOf(settings, jobs);
        var wanted = targets.Values.Select(target => target.ItemId).ToFrozenSet();
        var subAreas = chosen is null ? SubAreasOf(settings, atlas, wanted, placed) : [.. chosen.Distinct()];
        var groups = subAreas.Select((subArea, index) => (subArea, index)).ToDictionary(pair => pair.subArea, pair => pair.index);
        var maps = new Dictionary<long, int>();
        var insides = new HashSet<long>();
        var graphics = new Dictionary<long, ImmutableArray<int>>();

        foreach (var position in atlas.Positions.Span)
        {
            if (!groups.TryGetValue(position.SubAreaId, out var group) || !graph.Contains(position.MapId))
                continue;

            maps[position.MapId] = group;

            if (position.WorldMap < 0)
                insides.Add(position.MapId);

            // Read once, the graphics alone kept: a zone may hold thousands of maps, more than the pack's cache.
            if (mapData.TryGet(position.MapId, out var map))
                graphics[position.MapId] = [.. map.Interactives.ToArray().Select(element => element.GfxId).Distinct()];
        }

        return new HarvestZone(
            targets,
            harvests.ToFrozenDictionary(skill => skill.Id, skill => skill.GatheredItemId),
            wanted,
            subAreas,
            maps.ToFrozenDictionary(),
            insides.ToFrozenSet(),
            graphics.ToFrozenDictionary(),
            IsolatedMaps.Of(graph, maps),
            settings.IsolatedMaps);
    }

    /// <summary>Finds the skills settings use, and the resource each gathers, with its priority.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="jobs">The professions' skills.</param>
    /// <returns>
    /// The skills, by id: of a list, those gathering a resource listed, its place in the list its priority; by profession,
    /// those of the professions chosen gathering a resource not left out, every one first, or the highest level first (D165).
    /// </returns>
    public static FrozenDictionary<int, HarvestTarget> TargetsOf(HarvestSettings settings, JobCatalog jobs)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(jobs);

        var harvests = jobs.Skills.ToArray().Where(skill => skill.GatheredItemId != Skill.NoItem);

        if (settings.Mode is HarvestMode.ByProfession)
        {
            var chosen = settings.Jobs.ToHashSet();
            var excluded = settings.Excluded.ToHashSet();
            var highest = settings.Order is HarvestOrder.HighestLevel;

            return harvests
                .Where(skill => chosen.Contains(skill.JobId) && !excluded.Contains(skill.GatheredItemId))
                .ToFrozenDictionary(skill => skill.Id, skill => new HarvestTarget(skill.Id, skill.GatheredItemId, skill.JobId, skill.MinLevel, highest ? -skill.MinLevel : 0));
        }

        var priorities = settings.Resources.Select((item, index) => (item, index)).ToDictionary(pair => pair.item, pair => pair.index);

        return harvests
            .Where(skill => priorities.ContainsKey(skill.GatheredItemId))
            .ToFrozenDictionary(skill => skill.Id, skill => new HarvestTarget(skill.Id, skill.GatheredItemId, skill.JobId, skill.MinLevel, priorities[skill.GatheredItemId]));
    }

    /// <summary>Finds the maps the host's learned graphics place settings' resources of <c>Base</c> on, which an area alone stands for (D150).</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="jobs">The skills.</param>
    /// <param name="places">Where each graphic is drawn.</param>
    /// <param name="graphics">What the host learned of the graphics.</param>
    /// <returns>The maps; <see langword="null"/> when the settings name sub-areas, no area, or none of <c>Base</c>'s resources.</returns>
    public static IReadOnlySet<long>? PlacedFor(HarvestSettings settings, JobCatalog jobs, GraphicPlaces places, GraphicSkills graphics)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(places);

        if (settings.Place is HarvestPlace.Automatic || settings.Area is null || settings.SubAreas.Count > 0)
            return null;

        HashSet<int> skills = [.. TargetsOf(settings, jobs).Values.Where(target => target.JobId == Job.Base).Select(target => target.SkillId)];

        return skills.Count is 0 ? null : places.MapsOffering(skills, graphics);
    }

    private static List<int> SubAreasOf(HarvestSettings settings, WorldAtlas atlas, FrozenSet<int> wanted, IReadOnlySet<long>? placed)
    {
        if (settings.SubAreas.Count > 0)
            return [.. settings.SubAreas.Distinct()];

        var subAreas = new List<int>();

        if (settings.Area is not { } area)
            return subAreas;

        var placedIn = new HashSet<int>();

        foreach (var map in placed ?? new HashSet<long>())
        {
            if (atlas.TryGetPosition(map, out var position))
                placedIn.Add(position.SubAreaId);
        }

        foreach (var subArea in atlas.SubAreas.Span)
        {
            if (subArea.AreaId != area)
                continue;

            if (placedIn.Contains(subArea.Id))
            {
                subAreas.Add(subArea.Id);

                continue;
            }

            foreach (var item in atlas.HarvestablesOf(subArea))
            {
                if (wanted.Contains(item))
                {
                    subAreas.Add(subArea.Id);

                    break;
                }
            }
        }

        return subAreas;
    }

    /// <summary>Foretells what a map of the zone holds, from what the host learned of its elements' graphics (D138).</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="graphics">What the host learned of the graphics.</param>
    /// <returns>
    /// <see cref="MapForecast.Probable"/> when one of its graphics offers a skill the zone uses, <see cref="MapForecast.Without"/>
    /// when every one is known and none does, a map without elements included, else <see cref="MapForecast.Unknown"/>.
    /// </returns>
    public MapForecast Foresee(long mapId, GraphicSkills graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (!Graphics.TryGetValue(mapId, out var drawn))
            return MapForecast.Unknown;

        var known = true;

        foreach (var gfxId in drawn)
        {
            if (!graphics.TryGet(gfxId, out var skills))
                known = false;
            else if (skills.Any(Targets.ContainsKey))
                return MapForecast.Probable;
        }

        return known ? MapForecast.Without : MapForecast.Unknown;
    }

    /// <summary>Gets the resources asked for that the zone's sub-areas list among their harvestables.</summary>
    /// <param name="atlas">The atlas.</param>
    /// <returns>The resources, by item.</returns>
    public IReadOnlySet<int> ListedIn(WorldAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        var listed = new HashSet<int>();

        foreach (var id in SubAreas)
        {
            if (!atlas.TryGetSubArea(id, out var subArea))
                continue;

            foreach (var item in atlas.HarvestablesOf(subArea))
            {
                if (Wanted.Contains(item))
                    listed.Add(item);
            }
        }

        return listed;
    }

    /// <summary>Gets the resources asked for that the zone likely holds: those its sub-areas list, and those a graphic its maps draw is known to offer (D138, D150).</summary>
    /// <param name="atlas">The atlas.</param>
    /// <param name="graphics">What the host learned of the graphics.</param>
    /// <returns>The resources, by item.</returns>
    public IReadOnlySet<int> PresentIn(WorldAtlas atlas, GraphicSkills graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        var present = new HashSet<int>(ListedIn(atlas));

        foreach (var drawn in Graphics.Values)
        {
            foreach (var gfxId in drawn)
            {
                if (!graphics.TryGet(gfxId, out var skills))
                    continue;

                foreach (var skill in skills)
                {
                    if (Targets.TryGetValue(skill, out var target))
                        present.Add(target.ItemId);
                }
            }
        }

        return present;
    }
}
