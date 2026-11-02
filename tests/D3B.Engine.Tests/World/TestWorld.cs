using D3B.Data.World;
using D3B.Engine.World;

namespace D3B.Engine.Tests.World;

/// <summary>A world graph and its atlas as a test draws them: maps, each one zone unless told, and the transitions between them.</summary>
/// <remarks>Every map lets a character teleport from it unless told otherwise.</remarks>
internal sealed class TestWorld
{
    public const int SubAreaId = 95;
    public const int ClosedSubAreaId = 96;
    public const int AreaId = 7;

    private readonly List<(long Map, int Zone, long ToMap, int ToZone, WorldTransition Transition)> _transitions = [];
    private readonly List<string> _criteria = [];
    private readonly SortedDictionary<long, int> _subAreas = [];
    private readonly Dictionary<long, (short X, short Y, sbyte WorldMap, bool Teleport)> _places = [];

    /// <summary>Adds a map, in a sub-area accounts without a subscription enter unless told otherwise.</summary>
    public TestWorld Map(long map, int subAreaId = SubAreaId)
    {
        _subAreas[map] = subAreaId;

        return this;
    }

    /// <summary>Places a map at coordinates, on a world map or on none, -1, as an inside, a character teleporting from it or not; maps are at 0,0 on world map 1 unless told.</summary>
    public TestWorld Place(long map, short x, short y, sbyte worldMap = 1, bool teleport = true)
    {
        _subAreas.TryAdd(map, SubAreaId);
        _places[map] = (x, y, worldMap, teleport);

        return this;
    }

    /// <summary>A scroll from a map to its neighbour, in a direction, from a cell.</summary>
    public TestWorld Scroll(long from, long to, int direction, int cell = 0, string? criterion = null, int zone = 1, int toZone = 1)
    {
        return Add(from, zone, to, toZone, new WorldTransition(WorldTransitionType.Scroll, direction, cell, WorldTransition.None, to, WorldTransition.None, Criterion(criterion)));
    }

    /// <summary>A scroll with an action: the client asks for the neighbour, and the server sends the character elsewhere.</summary>
    public TestWorld ScrollAction(long from, long neighbour, long to, int direction, int cell = 0, string? criterion = null)
    {
        return Add(from, 1, to, 1, new WorldTransition(WorldTransitionType.ScrollAction, direction, cell, WorldTransition.None, neighbour, WorldTransition.None, Criterion(criterion)));
    }

    /// <summary>A map action: stepping on a cell moves the character to another map.</summary>
    public TestWorld MapAction(long from, long to, int cell, string? criterion = null)
    {
        return Add(from, 1, to, 1, new WorldTransition(WorldTransitionType.MapAction, WorldTransition.NoDirection, cell, WorldTransition.None, to, WorldTransition.None, Criterion(criterion)));
    }

    /// <summary>An interactive transition: using an element of the map, a door with skill 184.</summary>
    public TestWorld Interactive(long from, long to, int cell, int skill = WorldPathfinder.DoorSkillId, string? criterion = null, int element = 515308)
    {
        return Add(from, 1, to, 1, new WorldTransition(WorldTransitionType.Interactive, WorldTransition.NoDirection, cell, skill, to, element, Criterion(criterion)));
    }

    public WorldGraph Graph()
    {
        var vertices = _transitions.SelectMany(t => new[] { (t.Map, t.Zone), (t.ToMap, t.ToZone) })
            .Concat(_subAreas.Keys.Select(map => (map, 1)))
            .Distinct()
            .Order()
            .ToList();
        var index = vertices.Select((vertex, i) => (vertex, i)).ToDictionary(pair => pair.vertex, pair => pair.i);
        var edges = new List<WorldEdge>();
        var transitions = new List<WorldTransition>();

        foreach (var group in _transitions.GroupBy(t => (From: index[(t.Map, t.Zone)], To: index[(t.ToMap, t.ToZone)])).OrderBy(group => group.Key))
        {
            edges.Add(new WorldEdge(group.Key.From, group.Key.To, transitions.Count, group.Count()));
            transitions.AddRange(group.Select(t => t.Transition));
        }

        return new WorldGraph(vertices.Select(v => new WorldVertex(v.Item1, v.Item2)).ToArray(), edges.ToArray(), transitions.ToArray(), _criteria.ToArray());
    }

    public WorldAtlas Atlas()
    {
        var maps = Graph().Vertices.ToArray().Select(vertex => vertex.MapId).Distinct().Order();
        var positions = maps.Select(Position).ToArray();
        SubArea[] subAreas =
        [
            new(SubAreaId, 0, AreaId, 10, SubArea.NoZaap, true, 0, 0, 0, 0),
            new(ClosedSubAreaId, 0, AreaId, 10, SubArea.NoZaap, false, 0, 0, 0, 0),
        ];

        return new WorldAtlas(positions, subAreas, new[] { new Area(AreaId, 0, 0) }, Array.Empty<Waypoint>(), Array.Empty<int>(), Array.Empty<int>());
    }

    public WorldPathfinder Pathfinder(params NpcWay[] npcWays)
    {
        return new WorldPathfinder(Graph(), Atlas(), npcWays);
    }

    private MapPosition Position(long map)
    {
        var (x, y, worldMap, teleport) = _places.TryGetValue(map, out var place) ? place : ((short)0, (short)0, (sbyte)1, true);

        // Bit 3 of the client's flags lets a character teleport from the map (client.md).
        return new MapPosition(map, x, y, _subAreas.GetValueOrDefault(map, SubAreaId), worldMap, 0, teleport ? 1u << 3 : 0);
    }

    private int Criterion(string? criterion)
    {
        if (criterion is null)
            return WorldTransition.None;

        _criteria.Add(criterion);

        return _criteria.Count - 1;
    }

    private TestWorld Add(long from, int zone, long to, int toZone, WorldTransition transition)
    {
        _subAreas.TryAdd(from, SubAreaId);
        _subAreas.TryAdd(to, SubAreaId);
        _transitions.Add((from, zone, to, toZone, transition));

        return this;
    }
}
