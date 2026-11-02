using D3B.Data.World;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the client's <c>Core.PathFinding.WorldPathfinding.PathFindingData</c> into a <see cref="WorldGraph"/>.</summary>
/// <remarks>
/// <para>
/// The client keys vertices by map then zone, and edges by the <c>m_uid</c> of their source then destination vertex;
/// <c>m_outgoingEdges</c> repeats <c>m_edges</c> as one list per vertex (client.md) and is skipped. The graph is
/// rebuilt with vertices sorted by map and zone, edges by source and destination, and criteria in one table.
/// </para>
/// <para>
/// Each transition is checked against what client 3.7.12.12 holds: a type the extractor does not know, or a skill or
/// element on a transition type that has none, fails the extraction rather than be stored with a guessed meaning.
/// </para>
/// </remarks>
internal static class WorldGraphReader
{
    public static bool IsGraph(UnityTypeTree tree)
    {
        return tree.HasFields("m_vertices", "m_edges");
    }

    public static WorldGraph Read(byte[] data, UnityTypeTree tree)
    {
        var reader = new UnityReader(data, tree);
        var vertices = new List<(long MapId, int Zone, ulong Uid)>();
        var edges = new List<(ulong From, ulong To, List<RawTransition> Transitions)>();

        foreach (var child in tree[0].Children)
        {
            switch (tree[child].Field)
            {
                case "m_vertices":
                    ReadVertices(ref reader, child, vertices);
                    break;
                case "m_edges":
                    ReadEdges(ref reader, child, edges);
                    break;
                default:
                    reader.Skip(child);
                    break;
            }
        }

        reader.ExpectEnd();

        return Build(vertices, edges);
    }

    private static void ReadVertices(ref UnityReader reader, int node, List<(long MapId, int Zone, ulong Uid)> vertices)
    {
        var tree = reader.Tree;
        var (keys, values) = DictionaryParts(tree, node);
        var maps = reader.ReadInt64Array(keys);
        var item = reader.BeginArray(values, out var count);

        if (count != maps.Length)
            throw new InvalidDataException($"{tree.Name}: {maps.Length} maps for {count} sets of vertices.");

        for (var i = 0; i < count; i++)
        {
            var (zoneKeys, zoneValues) = DictionaryParts(tree, item);
            var zones = reader.ReadInt32Array(zoneKeys);
            var vertex = reader.BeginArray(zoneValues, out var zoneCount);

            if (zoneCount != zones.Length)
                throw new InvalidDataException($"{tree.Name}: map {maps[i]} has {zones.Length} zones for {zoneCount} vertices.");

            for (var j = 0; j < zoneCount; j++)
            {
                var (mapId, zone, uid) = ReadVertex(ref reader, vertex);

                if (mapId != maps[i] || zone != zones[j])
                    throw new InvalidDataException($"{tree.Name}: the vertex of map {maps[i]}, zone {zones[j]}, names map {mapId}, zone {zone}.");

                vertices.Add((mapId, zone, uid));
            }

            reader.EndArray(zoneValues);
            reader.AlignAfter(item);
        }

        reader.EndArray(values);
        reader.AlignAfter(node);
    }

    private static void ReadEdges(ref UnityReader reader, int node, List<(ulong From, ulong To, List<RawTransition> Transitions)> edges)
    {
        var tree = reader.Tree;
        var (keys, values) = DictionaryParts(tree, node);
        var sources = reader.ReadInt64Array(keys);
        var item = reader.BeginArray(values, out var count);

        if (count != sources.Length)
            throw new InvalidDataException($"{tree.Name}: {sources.Length} sources for {count} sets of edges.");

        for (var i = 0; i < count; i++)
        {
            var (edgeKeys, edgeValues) = DictionaryParts(tree, item);
            var destinations = reader.ReadInt64Array(edgeKeys);
            var edge = reader.BeginArray(edgeValues, out var edgeCount);

            if (edgeCount != destinations.Length)
                throw new InvalidDataException($"{tree.Name}: vertex {sources[i]} has {destinations.Length} destinations for {edgeCount} edges.");

            for (var j = 0; j < edgeCount; j++)
            {
                var read = ReadEdge(ref reader, edge);

                if (read.From != (ulong)sources[i] || read.To != (ulong)destinations[j])
                    throw new InvalidDataException($"{tree.Name}: the edge from {sources[i]} to {destinations[j]} names {read.From} to {read.To}.");

                edges.Add(read);
            }

            reader.EndArray(edgeValues);
            reader.AlignAfter(item);
        }

        reader.EndArray(values);
        reader.AlignAfter(node);
    }

    /// <summary>The <c>m_keys</c> and <c>m_values</c> of one of the client's serialized dictionaries.</summary>
    private static (int Keys, int Values) DictionaryParts(UnityTypeTree tree, int node)
    {
        var keys = tree.Child(node, "m_keys");
        var values = tree.Child(node, "m_values");

        if (keys is -1 || values is -1 || tree[node].Children.Length is not 2)
            throw tree.Unexpected(node, "a dictionary of m_keys and m_values");

        return (keys, values);
    }

    private static (long MapId, int Zone, ulong Uid) ReadVertex(ref UnityReader reader, int node)
    {
        var tree = reader.Tree;
        long mapId = 0;
        int zone = 0;
        ulong uid = 0;

        foreach (var child in tree[node].Children)
        {
            switch (tree[child].Field)
            {
                case "m_mapId":
                    mapId = reader.ReadInt64(child);
                    break;
                case "m_zoneId":
                    zone = reader.ReadInt32(child);
                    break;
                case "m_uid":
                    uid = reader.ReadUInt64(child);
                    break;
                default:
                    reader.Skip(child);
                    break;
            }
        }

        reader.AlignAfter(node);

        return (mapId, zone, uid);
    }

    private static (ulong From, ulong To, List<RawTransition> Transitions) ReadEdge(ref UnityReader reader, int node)
    {
        var tree = reader.Tree;
        ulong from = 0, to = 0;
        var transitions = new List<RawTransition>();

        foreach (var child in tree[node].Children)
        {
            switch (tree[child].Field)
            {
                case "m_from":
                    from = ReadVertex(ref reader, child).Uid;
                    break;
                case "m_to":
                    to = ReadVertex(ref reader, child).Uid;
                    break;
                case "m_transitions":
                    var item = reader.BeginArray(child, out var count);

                    for (var i = 0; i < count; i++)
                        transitions.Add(ReadTransition(ref reader, item));

                    reader.EndArray(child);
                    break;
                default:
                    reader.Skip(child);
                    break;
            }
        }

        reader.AlignAfter(node);

        return (from, to, transitions);
    }

    private static RawTransition ReadTransition(ref UnityReader reader, int node)
    {
        var tree = reader.Tree;
        var transition = new RawTransition();

        foreach (var child in tree[node].Children)
        {
            switch (tree[child].Field)
            {
                case "m_type":
                    transition.Type = reader.ReadInt32(child);
                    break;
                case "m_direction":
                    transition.Direction = reader.ReadInt32(child);
                    break;
                case "m_skillId":
                    transition.SkillId = reader.ReadInt32(child);
                    break;
                case "m_criterion":
                    transition.Criterion = reader.ReadString(child);
                    break;
                case "m_transitionMapId":
                    transition.MapId = reader.ReadInt64(child);
                    break;
                case "m_cellId":
                    transition.CellId = reader.ReadInt32(child);
                    break;
                case "m_id":
                    transition.Id = reader.ReadInt64(child);
                    break;
                default:
                    reader.Skip(child);
                    break;
            }
        }

        reader.AlignAfter(node);

        return transition;
    }

    private static WorldGraph Build(List<(long MapId, int Zone, ulong Uid)> vertices, List<(ulong From, ulong To, List<RawTransition> Transitions)> edges)
    {
        vertices.Sort((left, right) => (left.MapId, left.Zone).CompareTo((right.MapId, right.Zone)));

        var indices = new Dictionary<ulong, int>(vertices.Count);

        for (var i = 0; i < vertices.Count; i++)
        {
            if (!indices.TryAdd(vertices[i].Uid, i))
                throw new InvalidDataException($"The world graph has two vertices of uid {vertices[i].Uid}.");
        }

        int IndexOf(ulong uid)
        {
            return indices.TryGetValue(uid, out var index) ? index : throw new InvalidDataException($"An edge of the world graph names vertex {uid}, which it does not hold.");
        }

        var criteria = edges.SelectMany(edge => edge.Transitions).Select(transition => transition.Criterion).Where(criterion => criterion.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var criterionIds = criteria.Select((criterion, index) => (criterion, index)).ToDictionary(pair => pair.criterion, pair => pair.index, StringComparer.Ordinal);
        var sorted = edges.Select(edge => (From: IndexOf(edge.From), To: IndexOf(edge.To), edge.Transitions)).OrderBy(edge => edge.From).ThenBy(edge => edge.To).ToList();
        var worldEdges = new WorldEdge[sorted.Count];
        var transitions = new List<WorldTransition>();

        for (var i = 0; i < sorted.Count; i++)
        {
            var (from, to, raw) = sorted[i];

            if (raw.Count is 0)
                throw new InvalidDataException($"The world graph's edge from map {vertices[from].MapId} to map {vertices[to].MapId} has no transition.");

            worldEdges[i] = new WorldEdge(from, to, transitions.Count, raw.Count);

            foreach (var transition in raw)
                transitions.Add(transition.ToWorld(vertices[from].MapId, criterionIds));
        }

        return new WorldGraph(vertices.Select(vertex => new WorldVertex(vertex.MapId, vertex.Zone)).ToArray(), worldEdges, transitions.ToArray(), criteria);
    }

    private sealed class RawTransition
    {
        public int Type { get; set; }

        public int Direction { get; set; }

        public int SkillId { get; set; }

        public string Criterion { get; set; } = string.Empty;

        public long MapId { get; set; }

        public int CellId { get; set; }

        public long Id { get; set; }

        public WorldTransition ToWorld(long sourceMapId, Dictionary<string, int> criterionIds)
        {
            var type = (WorldTransitionType)Type;

            if (Type is < byte.MinValue or > byte.MaxValue || !Enum.IsDefined(type))
                throw new InvalidDataException($"A transition from map {sourceMapId} has type {Type}, which the extractor does not know: measure it, then add it to WorldTransitionType.");

            var interactive = type is WorldTransitionType.Interactive;

            // Only interactive transitions name a skill and an element; another type naming one is not what was measured.
            if (interactive ? SkillId < 0 || Id < 0 : SkillId is not WorldTransition.None || Id is not WorldTransition.None)
                throw new InvalidDataException($"A {type} transition from map {sourceMapId} names skill {SkillId} and element {Id}.");

            try
            {
                return new WorldTransition(type, Direction, CellId, SkillId, MapId, checked((int)Id), Criterion.Length is 0 ? WorldTransition.None : criterionIds[Criterion]);
            }
            catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
            {
                throw new InvalidDataException($"A {type} transition from map {sourceMapId} does not fit what the extractor measured: {exception.Message}", exception);
            }
        }
    }
}
