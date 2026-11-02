using D3B.Data.World;

namespace D3B.Engine.World;

/// <summary>Finds the insides a map's doors lead into, such as a bank's behind the map in front of it.</summary>
/// <remarks>
/// Measured on the world graph of 3.7.12.12 (client.md): from the map in front of each of the 10 banks, doors lead to 1 to
/// 5 maps. The bank's inside, and other buildings' at times, lie at that map's coordinates on no world map; a few doors
/// lead to other maps of the world. Which inside holds the banker, only the server says, by the NPCs it places there (D29).
/// </remarks>
public static class WorldInsides
{
    private const sbyte NoWorldMap = -1;

    /// <summary>Finds the maps a map's doors lead into, at its coordinates and on no world map, whatever their criteria.</summary>
    /// <param name="graph">The world graph.</param>
    /// <param name="atlas">The atlas placing each map.</param>
    /// <param name="mapId">The map the doors are on.</param>
    /// <returns>The insides, each once, in the order of the map's zones and of the graph's edges; none when the atlas does not place the map.</returns>
    /// <remarks>
    /// A door's criterion is left to the path that goes through it (D76): a quest closes the door of the bank of Astrub
    /// onto one of its two insides, the same element leading to both.
    /// </remarks>
    public static IReadOnlyList<long> Of(WorldGraph graph, WorldAtlas atlas, long mapId)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(atlas);

        var insides = new List<long>();

        if (!atlas.TryGetPosition(mapId, out var outside))
            return insides;

        for (var zone = 0; zone <= WorldVertex.MaxZone; zone++)
        {
            if (!graph.TryGetVertex(mapId, zone, out var vertex))
                continue;

            foreach (var edge in graph.EdgesFrom(vertex))
            {
                var to = graph.Vertices.Span[edge.To].MapId;

                if (!insides.Contains(to) && IsInside(atlas, outside, to) && HasDoor(graph, edge))
                    insides.Add(to);
            }
        }

        return insides;
    }

    private static bool IsInside(WorldAtlas atlas, MapPosition outside, long mapId)
    {
        return atlas.TryGetPosition(mapId, out var inside) && inside.WorldMap == NoWorldMap && inside.X == outside.X && inside.Y == outside.Y;
    }

    private static bool HasDoor(WorldGraph graph, WorldEdge edge)
    {
        foreach (var transition in graph.TransitionsOf(edge))
        {
            if (transition.Type is WorldTransitionType.Interactive && transition.SkillId == WorldPathfinder.DoorSkillId)
                return true;
        }

        return false;
    }
}
