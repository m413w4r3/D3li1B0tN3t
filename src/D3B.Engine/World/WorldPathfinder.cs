using System.Diagnostics.CodeAnalysis;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Criteria;
using D3B.Engine.Maps;

namespace D3B.Engine.World;

/// <summary>Finds paths between maps over the client's world graph, and the cells each map is left from.</summary>
/// <remarks>
/// <para>
/// A vertex is a zone of a map, the cells of which reach one another (D27). The search follows scrolls, scrolls with an
/// action, map actions and every interactive transition, a door used with <see cref="DoorSkillId"/> as stairs, a portal or
/// a "go to" with their own skills (D248). A transition is followed only when its criterion is met (D71): what the
/// character cannot be sure of closes it. A sub-area the client closes to accounts without a subscription is entered only by a subscribed
/// account.
/// </para>
/// <para>
/// A subscribed character also teleports, from any map the server lets it teleport from
/// (<see cref="MapPosition.AllowsTeleport"/>), to the zaap of every map it knows one on
/// (<see cref="WorldPathRules.KnownZaaps"/>) whose cost it carries (<see cref="TeleportCost"/>), which the graph does not
/// hold (D83). It lands on the zaap's map, on its only vertex: a zaap's map with several zones is left out, where the
/// character lands there not being measured.
/// </para>
/// <para>
/// It also goes through the NPCs' dialogs it is given (<see cref="NpcWays"/>), which the graph does not hold either, such as
/// the portal out of Incarnam (D253): from any vertex of the NPC's map to the only vertex of the map the dialog leads to.
/// </para>
/// <para>
/// Every map change costs the same, a teleport and an NPC's dialog too: the character walks across a map or the server moves it, then the
/// client loads the next, a few seconds either way (client.md), so the quickest path is the one with the fewest. A
/// teleport may count as more (<see cref="WorldPathRules.TeleportChanges"/>), so that a zaap is taken only when it saves
/// that many changes, and paid only then; one that saves nothing is never taken (D167). Dijkstra's search, with no estimate: map actions and teleports lead anywhere, so no distance between maps bounds what
/// is left.
/// </para>
/// <para>
/// Built once per pack and shared: the criteria are read once and the vertices' sub-areas found once, and nothing changes
/// after. Each search keeps its state in its own arrays, so any number of bots search at once (D9).
/// </para>
/// </remarks>
public sealed class WorldPathfinder
{
    /// <summary>The skill a door is used with, "Utiliser": 5,237 of the graph's 5,506 interactive transitions name it.</summary>
    public const int DoorSkillId = 184;

    // Doubled, and a teleport one more than its changes' double: odd, it never ties a walk, so that one saving fewer changes
    // than it counts as, or none, is walked (D167).
    private const int ChangeCost = 2;
    private const int KamasPerCoordinate = 10;
    private const int OtherWorldMapCost = 1000;
    private const int NoVertex = -1;

    private readonly Criterion?[] _criteria;
    private readonly int[] _subAreas;
    private readonly int[] _areas;
    private readonly bool[] _basic;
    private readonly MapPosition?[] _positions;
    private readonly Dictionary<long, int> _onlyVertices;
    private readonly Dictionary<long, List<int>> _npcWaysFrom;
    private readonly int[] _npcWayTo;

    /// <summary>Gets the world graph the paths go through.</summary>
    public WorldGraph Graph { get; }

    /// <summary>Gets the atlas the paths are placed on: each map's coordinates and sub-area.</summary>
    public WorldAtlas Atlas { get; }

    /// <summary>Gets the NPCs' ways the paths may go through, a step naming one by its index here (<see cref="WorldStep.NpcWay"/>).</summary>
    public IReadOnlyList<NpcWay> NpcWays { get; }

    /// <summary>Initializes a new instance of the <see cref="WorldPathfinder"/> class, with no NPC's way.</summary>
    /// <param name="graph">The world graph.</param>
    /// <param name="atlas">The atlas giving each map its sub-area, and each sub-area its area and whether accounts without a subscription enter it.</param>
    public WorldPathfinder(WorldGraph graph, WorldAtlas atlas)
        : this(graph, atlas, [])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WorldPathfinder"/> class.</summary>
    /// <param name="graph">The world graph.</param>
    /// <param name="atlas">The atlas giving each map its sub-area, and each sub-area its area and whether accounts without a subscription enter it.</param>
    /// <param name="npcWays">The NPCs' ways the paths may go through: one whose map the graph lacks, or that leads to a map without a vertex or with several, is never taken.</param>
    public WorldPathfinder(WorldGraph graph, WorldAtlas atlas, IReadOnlyList<NpcWay> npcWays)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(npcWays);

        var vertices = graph.Vertices.Span;

        Graph = graph;
        Atlas = atlas;
        _criteria = new Criterion?[graph.Criteria.Length];
        _subAreas = new int[vertices.Length];
        _areas = new int[vertices.Length];
        _basic = new bool[vertices.Length];
        _positions = new MapPosition?[vertices.Length];
        _onlyVertices = [];

        for (var i = 0; i < _criteria.Length; i++)
            _criteria[i] = Criterion.Parse(graph.Criteria.Span[i]);

        for (var i = 0; i < vertices.Length; i++)
        {
            var placed = atlas.TryGetPosition(vertices[i].MapId, out var position);
            var known = atlas.TryGetSubArea(position.SubAreaId, out var subArea) && placed;

            _subAreas[i] = placed ? position.SubAreaId : -1;
            _areas[i] = known ? subArea.AreaId : -1;
            _basic[i] = known && subArea.BasicAccountAllowed;
            _positions[i] = placed ? position : null;

            // A second vertex of the map leaves it with no only vertex.
            _onlyVertices[vertices[i].MapId] = _onlyVertices.ContainsKey(vertices[i].MapId) ? NoVertex : i;
        }

        NpcWays = [.. npcWays];
        _npcWaysFrom = [];
        _npcWayTo = new int[NpcWays.Count];

        for (var i = 0; i < NpcWays.Count; i++)
        {
            var way = NpcWays[i];

            // Where the dialog lands the character on a map of several zones is not measured, as for a zaap.
            _npcWayTo[i] = _onlyVertices.TryGetValue(way.ToMapId, out var to) ? to : NoVertex;

            if (_npcWayTo[i] is NoVertex || !graph.Contains(way.MapId))
                continue;

            if (!_npcWaysFrom.TryGetValue(way.MapId, out var ways))
                _npcWaysFrom[way.MapId] = ways = [];

            ways.Add(i);
        }
    }

    /// <summary>Gets what teleporting from a map to a zaap's costs, in kamas.</summary>
    /// <param name="from">Where the character teleports from.</param>
    /// <param name="to">The zaap's map.</param>
    /// <returns>Ten kamas a whole coordinate of the straight line between the two maps, and a thousand more towards another world map.</returns>
    /// <remarks>
    /// Measured on the 120 costs four lists of zaaps gave, from four maps, and on thirteen teleports (client.md): every one, the
    /// thousand towards Incarnam's world map. A map on no world map, such as a bank's inside, costs from its own
    /// coordinates; world maps other than these two are not measured, and count the thousand as Incarnam's does.
    /// </remarks>
    public static long TeleportCost(MapPosition from, MapPosition to)
    {
        var dx = (double)(to.X - from.X);
        var dy = (double)(to.Y - from.Y);
        var cost = KamasPerCoordinate * (long)Math.Floor(Math.Sqrt((dx * dx) + (dy * dy)));

        return from.WorldMap >= 0 && to.WorldMap >= 0 && from.WorldMap != to.WorldMap ? cost + OtherWorldMapCost : cost;
    }

    private static bool IsFollowed(WorldTransition transition)
    {
        // Measured on 3.7.12.12 (D248): beside the 5,237 doors, some 300 transitions of 50 other skills are stairs,
        // portals and "go to"s, used as a door is.
        return transition.Type is WorldTransitionType.Scroll or WorldTransitionType.ScrollAction or WorldTransitionType.MapAction or WorldTransitionType.Interactive;
    }

    private static int SourceOf(ReadOnlySpan<WorldEdge> edges, int transition)
    {
        // Edges hold their transitions in order: the last edge starting at or before the transition holds it.
        int low = 0, high = edges.Length - 1;

        while (low < high)
        {
            var middle = low + ((high - low + 1) >> 1);

            if (edges[middle].FirstTransition <= transition)
                low = middle;
            else
                high = middle - 1;
        }

        return edges[low].From;
    }

    /// <summary>Finds the vertex a character stands in.</summary>
    /// <param name="map">The map it is on.</param>
    /// <param name="cell">The cell it stands on.</param>
    /// <param name="vertex">The vertex, when the graph holds the cell's zone of the map.</param>
    /// <returns>Whether the graph holds it: a cell that cannot be walked on has no zone.</returns>
    public bool TryGetVertex(MapData map, int cell, out int vertex)
    {
        ArgumentNullException.ThrowIfNull(map);

        return Graph.TryGetVertex(map.Id, map.Cell(cell).Zone, out vertex);
    }

    /// <summary>Finds the path with the fewest map changes from a vertex to any zone of one of several maps.</summary>
    /// <param name="from">The vertex the character stands in.</param>
    /// <param name="maps">The maps sought.</param>
    /// <param name="rules">What the character may follow.</param>
    /// <param name="path">The path, when there is one: no step when the vertex's map is sought already.</param>
    /// <returns>Whether any of the maps can be reached.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The graph has no such vertex.</exception>
    public bool TryFind(int from, IReadOnlySet<long> maps, WorldPathRules rules, [NotNullWhen(true)] out WorldPath? path)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(from);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(from, Graph.Vertices.Length);
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(rules);

        var vertices = Graph.Vertices.Span;
        var costs = new int[vertices.Length];
        var through = new int[vertices.Length];
        var closed = new bool[vertices.Length];
        var open = new PriorityQueue<int, int>();
        var zaaps = ZaapsOf(rules);

        // Where a teleport or an NPC's way was taken from, which no edge says.
        var origins = zaaps.Count > 0 || _npcWaysFrom.Count > 0 ? new int[vertices.Length] : null;
        var reached = -1;

        Array.Fill(costs, int.MaxValue);
        costs[from] = 0;
        open.Enqueue(from, 0);

        while (open.TryDequeue(out var vertex, out _))
        {
            if (closed[vertex])
                continue;

            if (maps.Contains(vertices[vertex].MapId))
            {
                reached = vertex;

                break;
            }

            closed[vertex] = true;

            foreach (var edge in Graph.EdgesFrom(vertex))
            {
                if (closed[edge.To] || !MayEnter(edge.To, rules.Facts))
                    continue;

                var transition = Followed(vertex, edge, rules);
                var cost = costs[vertex] + ChangeCost;

                if (transition < 0 || cost >= costs[edge.To])
                    continue;

                costs[edge.To] = cost;
                through[edge.To] = transition;
                open.Enqueue(edge.To, cost);
            }

            if (_npcWaysFrom.TryGetValue(vertices[vertex].MapId, out var ways))
            {
                foreach (var way in ways)
                {
                    var to = _npcWayTo[way];
                    var index = WorldStep.OfNpcWay(way);
                    var cost = costs[vertex] + ChangeCost;

                    if (closed[to] || cost >= costs[to] || !MayEnter(to, rules.Facts) || rules.Banned.Contains(index) || rules.NoNpcWays.Contains(way))
                        continue;

                    costs[to] = cost;
                    through[to] = index;
                    origins![to] = vertex;
                    open.Enqueue(to, cost);
                }
            }

            if (origins is not null && zaaps.Count > 0 && _positions[vertex] is { AllowsTeleport: true } place && !rules.NoTeleportFrom.Contains(vertices[vertex].MapId))
            {
                var cost = costs[vertex] + (ChangeCost * rules.TeleportChanges) + 1;

                // Vertices come in order of cost, and every teleport costs the same: a zaap this one reaches, or one
                // already as near, no later vertex brings nearer, so it leaves the list. One the character cannot pay
                // from here stays, for a vertex nearer it.
                for (var i = zaaps.Count - 1; i >= 0; i--)
                {
                    var zaap = zaaps[i];

                    if (!closed[zaap] && cost < costs[zaap])
                    {
                        if (TeleportCost(place, _positions[zaap]!.Value) > rules.Kamas)
                            continue;

                        costs[zaap] = cost;
                        through[zaap] = WorldStep.Teleport;
                        origins[zaap] = vertex;
                        open.Enqueue(zaap, cost);
                    }

                    zaaps[i] = zaaps[^1];
                    zaaps.RemoveAt(zaaps.Count - 1);
                }
            }
        }

        path = reached < 0 ? null : PathTo(from, reached, through, origins);

        return path is not null;
    }

    /// <summary>Finds the cells a step's map is left from: where to walk before asking for the next map or using a door, or the cell to step on.</summary>
    /// <param name="map">The step's map.</param>
    /// <param name="step">The step.</param>
    /// <param name="rules">What the character may follow: a criterion on the cell is answered for each.</param>
    /// <returns>The cells, none when no cell meets the transition's criterion, nor for a teleport or an NPC's way, which leave from where the character stands.</returns>
    /// <remarks>
    /// <para>
    /// A map action is left from its own cell. A door is used from the cells the client uses an element from
    /// (<see cref="ElementCells"/>), around the element's cell in the map's data; the transition names another cell for 50
    /// of the graph's interactive transitions, mostly one that cannot be walked on. A scroll is left from any walkable cell
    /// of the zone on the map's edge in its direction that the client marks as leaving it in that direction, or along a
    /// diagonal next to it but on a corner, and from the transition's own cell, which lies on that edge. The client left
    /// from such cells, one marked only along a diagonal among them; the server took a cell of the edge marked nowhere, and
    /// ignored a corner marked as leaving by the other edge (client.md).
    /// </para>
    /// <para>
    /// Where several scrolls leave the zone in one direction, to several zones or maps, a cell leads where the nearest of
    /// those whose criterion it may meet does; a criterion on the cell, such as <c>#(PC=4|PC=5)</c>, names the cells a
    /// scroll with an action is left from.
    /// </para>
    /// </remarks>
    public IReadOnlyList<int> ExitCells(MapData map, WorldStep step, WorldPathRules rules)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(rules);

        if (step.IsTeleport || step.IsNpcWay)
            return [];

        var cells = map.Cells.Span;
        var transition = step.Transition;
        var facts = PlaceOf(step.From, rules.Facts);

        if (transition.Type is WorldTransitionType.MapAction)
            return Answer(transition, facts with { Cell = transition.CellId }) is CriterionAnswer.True ? [transition.CellId] : [];

        if (transition.Type is WorldTransitionType.Interactive)
        {
            var element = map.TryGetInteractive(transition.ElementId, out var interactive) && interactive.CellId != MapInteractive.NoCell
                ? interactive.CellId
                : transition.CellId;

            return [.. ElementCells.UsedFrom(cells, element, Graph.Vertices.Span[step.From].Zone)
                .Where(cell => Answer(transition, facts with { Cell = cell }) is CriterionAnswer.True)];
        }

        var zone = Graph.Vertices.Span[step.From].Zone;
        var direction = transition.Direction;
        var rivals = Rivals(step);
        var exits = new List<int>();

        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            var mapCell = cells[cell];

            if (!mapCell.IsWalkable || mapCell.Zone != zone || MapGeometry.Neighbour(cell, direction) >= 0)
                continue;

            if (cell != transition.CellId && !MapGeometry.LeavesTowards(mapCell, cell, direction))
                continue;

            var at = facts with { Cell = cell };

            if (Answer(transition, at) is CriterionAnswer.True && Leads(step, rivals, cell, at))
                exits.Add(cell);
        }

        return exits;
    }

    private bool MayEnter(int vertex, CriterionFacts facts)
    {
        return facts.Subscribed is true || _basic[vertex];
    }

    private List<int> ZaapsOf(WorldPathRules rules)
    {
        var zaaps = new List<int>();

        // A subscriber's only, prudently: no account without a subscription was measured teleporting (D72, D83).
        if (rules.Facts.Subscribed is not true)
            return zaaps;

        foreach (var map in rules.KnownZaaps)
        {
            if (_onlyVertices.TryGetValue(map, out var vertex) && vertex is not NoVertex && _positions[vertex] is not null)
                zaaps.Add(vertex);
        }

        return zaaps;
    }

    private int Followed(int vertex, WorldEdge edge, WorldPathRules rules)
    {
        var transitions = Graph.TransitionsOf(edge);

        for (var i = 0; i < transitions.Length; i++)
        {
            var index = edge.FirstTransition + i;
            var transition = transitions[i];

            if (IsFollowed(transition) && !rules.Banned.Contains(index)
                && (transition.CriterionId is WorldTransition.None || Answer(transition, PlaceOf(vertex, rules.Facts)) is CriterionAnswer.True))
            {
                return index;
            }
        }

        return -1;
    }

    private CriterionAnswer Answer(WorldTransition transition, CriterionFacts facts)
    {
        return transition.CriterionId is WorldTransition.None ? CriterionAnswer.True : _criteria[transition.CriterionId]!.Evaluate(facts);
    }

    private CriterionFacts PlaceOf(int vertex, CriterionFacts facts)
    {
        return facts with
        {
            MapId = Graph.Vertices.Span[vertex].MapId,
            SubAreaId = _subAreas[vertex] < 0 ? null : _subAreas[vertex],
            AreaId = _areas[vertex] < 0 ? null : _areas[vertex],
            Cell = null,
        };
    }

    private List<(int Index, WorldTransition Transition)> Rivals(WorldStep step)
    {
        var rivals = new List<(int, WorldTransition)>();

        foreach (var edge in Graph.EdgesFrom(step.From))
        {
            var transitions = Graph.TransitionsOf(edge);

            for (var i = 0; i < transitions.Length; i++)
            {
                var index = edge.FirstTransition + i;
                var transition = transitions[i];

                if (index != step.TransitionIndex && (transition.Type is WorldTransitionType.Scroll or WorldTransitionType.ScrollAction)
                    && transition.Direction == step.Transition.Direction)
                {
                    rivals.Add((index, transition));
                }
            }
        }

        return rivals;
    }

    private bool Leads(WorldStep step, List<(int Index, WorldTransition Transition)> rivals, int cell, CriterionFacts at)
    {
        var distance = MapGeometry.Distance(cell, step.Transition.CellId);

        foreach (var (index, rival) in rivals)
        {
            // A rival whose criterion is not known may still be the one the cell leads to.
            if (Answer(rival, at) is CriterionAnswer.False)
                continue;

            var rivalDistance = MapGeometry.Distance(cell, rival.CellId);

            if (rivalDistance < distance || (rivalDistance == distance && index < step.TransitionIndex))
                return false;
        }

        return true;
    }

    private WorldPath PathTo(int from, int to, int[] through, int[]? origins)
    {
        var steps = new List<WorldStep>();
        var transitions = Graph.Transitions.Span;
        var edges = Graph.Edges.Span;

        for (var vertex = to; vertex != from;)
        {
            var index = through[vertex];

            if (index <= WorldStep.Teleport)
            {
                var origin = origins![vertex];

                steps.Add(new WorldStep(origin, vertex, index, default));
                vertex = origin;

                continue;
            }

            var source = SourceOf(edges, index);

            steps.Add(new WorldStep(source, vertex, index, transitions[index]));
            vertex = source;
        }

        steps.Reverse();

        return new WorldPath(from, steps);
    }
}
