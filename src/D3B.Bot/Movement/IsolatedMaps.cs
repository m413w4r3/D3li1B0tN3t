using System.Collections.Frozen;
using D3B.Data.World;

namespace D3B.Bot.Movement;

/// <summary>The maps of a zone that a character reaches from their sub-area's main part only far round: a tour leaves them out, or goes through them last, as its settings say (D149, D245).</summary>
/// <remarks>
/// A sub-area may fall apart through its own maps, the world graph's: its main part holds the most maps, and a map off it
/// lying more than <see cref="MostDetour"/> changes away from it, teleports aside, is isolated. Harvesting's zone and the
/// fighting activity's ground share it.
/// </remarks>
public static class IsolatedMaps
{
    /// <summary>The most changes of maps a map off its sub-area's main part may lie from it, and still be gone through without asking (D149).</summary>
    public const int MostDetour = 4;

    /// <summary>Gets the maps of a zone reachable from their sub-area's main part only by leaving it, more than <see cref="MostDetour"/> changes away, teleports aside.</summary>
    /// <param name="graph">The world graph, holding the maps a character may travel to.</param>
    /// <param name="maps">The zone's maps, each with the group it falls apart in or not, such as its sub-area.</param>
    /// <returns>The maps isolated; none of a group whose maps hold together.</returns>
    public static FrozenSet<long> Of(WorldGraph graph, IReadOnlyDictionary<long, int> maps)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(maps);

        // Measured on the pack of 3.7.12.12: 67 of the 186 sub-areas listing resources fall apart through their own maps,
        // and of the 588 maps off their main part, 416 are reached only by a teleport or a door's criterion, 128 by 5 to 35
        // changes.
        var vertices = graph.Vertices.Span;
        var groups = new Dictionary<int, List<int>>();

        for (var vertex = 0; vertex < vertices.Length; vertex++)
        {
            if (!maps.TryGetValue(vertices[vertex].MapId, out var group))
                continue;

            if (!groups.TryGetValue(group, out var members))
                groups[group] = members = [];

            members.Add(vertex);
        }

        var isolated = new HashSet<long>();

        foreach (var members in groups.Values)
            isolated.UnionWith(In(graph, members));

        return isolated.ToFrozenSet();
    }

    private static HashSet<long> In(WorldGraph graph, List<int> members)
    {
        var vertices = graph.Vertices.Span;
        var index = new Dictionary<int, int>(members.Count);

        for (var i = 0; i < members.Count; i++)
            index[members[i]] = i;

        var parent = Enumerable.Range(0, members.Count).ToArray();

        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        foreach (var vertex in members)
        {
            foreach (var edge in graph.EdgesFrom(vertex))
            {
                if (index.TryGetValue(edge.To, out var to))
                    parent[Find(index[vertex])] = Find(to);
            }
        }

        // The main part holds the most maps, the lowest map first among equals, so that the same pack gives the same part.
        var parts = new Dictionary<int, HashSet<long>>();

        for (var i = 0; i < members.Count; i++)
        {
            var root = Find(i);

            if (!parts.TryGetValue(root, out var partMaps))
                parts[root] = partMaps = [];

            partMaps.Add(vertices[members[i]].MapId);
        }

        if (parts.Count < 2)
            return [];

        var main = parts.OrderByDescending(part => part.Value.Count).ThenBy(part => part.Value.Min()).First();
        var distance = new Dictionary<int, int>();
        var queue = new Queue<int>();

        for (var i = 0; i < members.Count; i++)
        {
            if (Find(i) == main.Key)
            {
                distance[members[i]] = 0;
                queue.Enqueue(members[i]);
            }
        }

        while (queue.Count > 0)
        {
            var vertex = queue.Dequeue();

            if (distance[vertex] >= MostDetour)
                continue;

            foreach (var edge in graph.EdgesFrom(vertex))
            {
                if (distance.TryAdd(edge.To, distance[vertex] + 1))
                    queue.Enqueue(edge.To);
            }
        }

        var reached = new HashSet<long>();
        var off = new HashSet<long>();

        foreach (var vertex in members)
        {
            var map = vertices[vertex].MapId;

            if (distance.ContainsKey(vertex))
                reached.Add(map);
            else if (!main.Value.Contains(map))
                off.Add(map);
        }

        off.ExceptWith(reached);

        return off;
    }
}
