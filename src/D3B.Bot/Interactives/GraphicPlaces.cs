using System.Collections.Frozen;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.World;

namespace D3B.Bot.Interactives;

/// <summary>Where each graphic of the maps' elements is drawn, so that what the host learned of a graphic places a skill on the world's maps (D138, D150).</summary>
/// <remarks>
/// <para>
/// No sub-area lists what a skill of <c>Base</c> gathers, such as drinking water from a well: only the graphics learned say
/// where it is (client.md). The index covers the maps a character may travel to.
/// </para>
/// <para>
/// One per host, built the first time it is asked, from the pack's entries read apart from <see cref="GameMaps"/>'s cache,
/// which keeps the maps the bots are on. Measured on the pack of 3.7.12.12: 10,981 maps, 3,345 graphics, 564 ms.
/// </para>
/// </remarks>
public sealed class GraphicPlaces
{
    private readonly Lazy<FrozenDictionary<int, long[]>> _maps;

    /// <summary>Initializes a new instance of the <see cref="GraphicPlaces"/> class.</summary>
    /// <param name="pack">The installed build's pack, holding the maps.</param>
    /// <param name="world">The installed build's world, whose graph says the maps a character may travel to.</param>
    public GraphicPlaces(GamePack pack, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(world);

        _maps = new Lazy<FrozenDictionary<int, long[]>>(() => Index(pack, world), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static FrozenDictionary<int, long[]> Index(GamePack pack, GameWorld world)
    {
        var graph = world.Graph;
        var index = new Dictionary<int, List<long>>();

        foreach (var position in world.Atlas.Positions.Span)
        {
            var name = MapData.EntryName(position.MapId);

            if (!graph.Contains(position.MapId) || !pack.Contains(name))
                continue;

            foreach (var gfxId in pack.Read<MapData>(name).Interactives.ToArray().Select(element => element.GfxId).Distinct())
            {
                if (!index.TryGetValue(gfxId, out var maps))
                    index[gfxId] = maps = [];

                maps.Add(position.MapId);
            }
        }

        return index.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    /// <summary>Finds the maps drawing a graphic, such as the guild's chest's (D147).</summary>
    /// <param name="gfxId">The graphic.</param>
    /// <returns>The maps of the world graph drawing it, each once; none when no map does.</returns>
    public IReadOnlySet<long> MapsDrawing(int gfxId)
    {
        return _maps.Value.TryGetValue(gfxId, out var maps) ? maps.ToHashSet() : new HashSet<long>();
    }

    /// <summary>Finds the maps drawing an element whose graphic the host learned to offer one of some skills.</summary>
    /// <param name="skills">The skills, by id.</param>
    /// <param name="graphics">What the host learned of the graphics.</param>
    /// <returns>The maps, each once; none while no graphic drawn on a map is known to offer them.</returns>
    public IReadOnlySet<long> MapsOffering(IReadOnlySet<int> skills, GraphicSkills graphics)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(graphics);

        var found = new HashSet<long>();

        if (skills.Count is 0 || graphics.Count is 0)
            return found;

        foreach (var (gfxId, maps) in _maps.Value)
        {
            if (graphics.TryGet(gfxId, out var offered) && offered.Overlaps(skills))
                found.UnionWith(maps);
        }

        return found;
    }
}
