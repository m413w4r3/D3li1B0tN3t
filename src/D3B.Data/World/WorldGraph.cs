using MemoryPack;

namespace D3B.Data.World;

/// <summary>The client's world graph: the zones of maps, and the transitions between them.</summary>
/// <remarks>
/// <para>
/// Read from the client's own <c>world-graph</c> (client.md): a vertex per zone of a map, an edge per pair of vertices
/// one leads to the other, and on each edge the transitions that follow it. Zaaps are not in it: they come from
/// <see cref="WorldAtlas.Waypoints"/>.
/// </para>
/// <para>
/// Vertices are sorted by map then zone, and edges by source then destination, so that both are found by binary search
/// and an edge's neighbours are contiguous. Loaded once and shared: nothing in it changes.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial class WorldGraph
{
    /// <summary>The name of the pack entry holding the graph.</summary>
    public const string EntryName = "world/graph";

    [MemoryPackIgnore]
    private readonly int[] _firstEdges;

    /// <summary>Gets the vertices, in ascending order of map then zone.</summary>
    public ReadOnlyMemory<WorldVertex> Vertices { get; }

    /// <summary>Gets the edges, in ascending order of source then destination vertex.</summary>
    public ReadOnlyMemory<WorldEdge> Edges { get; }

    /// <summary>Gets the transitions, each edge's together and in the order of the edges.</summary>
    public ReadOnlyMemory<WorldTransition> Transitions { get; }

    /// <summary>Gets the criteria transitions name by index, such as <c>Qf=1234</c>: conditions in the client's syntax, not parsed yet.</summary>
    public ReadOnlyMemory<string> Criteria { get; }

    /// <summary>Initializes a new instance of the <see cref="WorldGraph"/> class.</summary>
    /// <param name="vertices">The vertices, in strictly ascending order of map then zone.</param>
    /// <param name="edges">The edges, in strictly ascending order of source then destination, each naming its own transitions.</param>
    /// <param name="transitions">The transitions, each edge's together and in the order of the edges, with nothing between.</param>
    /// <param name="criteria">The criteria the transitions name by index.</param>
    /// <exception cref="ArgumentException">The vertices or edges are out of order, or an index points outside what it names.</exception>
    [MemoryPackConstructor]
    public WorldGraph(ReadOnlyMemory<WorldVertex> vertices, ReadOnlyMemory<WorldEdge> edges, ReadOnlyMemory<WorldTransition> transitions, ReadOnlyMemory<string> criteria)
    {
        var vertexSpan = vertices.Span;

        for (var i = 1; i < vertexSpan.Length; i++)
        {
            if (Compare(vertexSpan[i - 1], vertexSpan[i]) >= 0)
                throw new ArgumentException($"Vertex {i} does not follow vertex {i - 1} in ascending order of map then zone.", nameof(vertices));
        }

        var edgeSpan = edges.Span;
        var firstEdges = new int[vertexSpan.Length + 1];
        var nextTransition = 0;

        for (var i = 0; i < edgeSpan.Length; i++)
        {
            var edge = edgeSpan[i];

            if (edge.From >= vertexSpan.Length || edge.To >= vertexSpan.Length)
                throw new ArgumentException($"Edge {i} names a vertex beyond the {vertexSpan.Length} the graph has.", nameof(edges));

            if (i > 0 && (edge.From, edge.To).CompareTo((edgeSpan[i - 1].From, edgeSpan[i - 1].To)) <= 0)
                throw new ArgumentException($"Edge {i} does not follow edge {i - 1} in ascending order of source then destination.", nameof(edges));

            if (edge.FirstTransition != nextTransition)
                throw new ArgumentException($"The transitions of edge {i} do not start where those of the edge before end.", nameof(edges));

            nextTransition += edge.TransitionCount;
            firstEdges[edge.From + 1] = i + 1;
        }

        if (nextTransition != transitions.Length)
            throw new ArgumentException($"The edges name {nextTransition} transitions, where the graph has {transitions.Length}.", nameof(transitions));

        // A vertex without edges starts where the vertex before it ends.
        for (var i = 1; i < firstEdges.Length; i++)
            firstEdges[i] = Math.Max(firstEdges[i], firstEdges[i - 1]);

        foreach (var transition in transitions.Span)
        {
            if (transition.CriterionId >= criteria.Length)
                throw new ArgumentException($"A transition names criterion {transition.CriterionId}, where the graph has {criteria.Length}.", nameof(transitions));
        }

        _firstEdges = firstEdges;
        Vertices = vertices;
        Edges = edges;
        Transitions = transitions;
        Criteria = criteria;
    }

    private static int Compare(WorldVertex left, WorldVertex right)
    {
        return (left.MapId, left.Zone).CompareTo((right.MapId, right.Zone));
    }

    /// <summary>Tells whether a map has a vertex in the graph: about two thirds of the client's maps do.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>Whether any zone of the map is a vertex.</returns>
    public bool Contains(long mapId)
    {
        var index = LowerBound(mapId, 0);

        return index < Vertices.Length && Vertices.Span[index].MapId == mapId;
    }

    /// <summary>Finds the vertex of a zone of a map.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="zone">The zone: the <see cref="Maps.MapCell.Zone"/> of a cell of the map.</param>
    /// <param name="vertex">The vertex's index in <see cref="Vertices"/>, when found.</param>
    /// <returns>Whether the graph holds that zone of that map.</returns>
    public bool TryGetVertex(long mapId, int zone, out int vertex)
    {
        var index = LowerBound(mapId, zone);
        var found = index < Vertices.Length && Vertices.Span[index].MapId == mapId && Vertices.Span[index].Zone == zone;

        vertex = found ? index : -1;

        return found;
    }

    /// <summary>Gets the edges leaving a vertex.</summary>
    /// <param name="vertex">The vertex's index in <see cref="Vertices"/>.</param>
    /// <returns>Its edges, in ascending order of destination.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The graph has no such vertex.</exception>
    public ReadOnlySpan<WorldEdge> EdgesFrom(int vertex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(vertex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(vertex, Vertices.Length);

        return Edges.Span[_firstEdges[vertex].._firstEdges[vertex + 1]];
    }

    /// <summary>Gets the transitions of an edge.</summary>
    /// <param name="edge">An edge of the graph.</param>
    /// <returns>Its transitions.</returns>
    public ReadOnlySpan<WorldTransition> TransitionsOf(WorldEdge edge)
    {
        return Transitions.Span.Slice(edge.FirstTransition, edge.TransitionCount);
    }

    /// <summary>Gets the criterion a transition must meet.</summary>
    /// <param name="transition">A transition of the graph.</param>
    /// <returns>The criterion, or <see langword="null"/> when anyone may follow it.</returns>
    public string? CriterionOf(WorldTransition transition)
    {
        return transition.CriterionId is WorldTransition.None ? null : Criteria.Span[transition.CriterionId];
    }

    private int LowerBound(long mapId, int zone)
    {
        var vertices = Vertices.Span;
        int low = 0, high = vertices.Length;

        while (low < high)
        {
            var middle = low + ((high - low) >> 1);

            if ((vertices[middle].MapId, vertices[middle].Zone).CompareTo((mapId, zone)) < 0)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }
}
