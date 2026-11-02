using D3B.App.Controls;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.Maps;

namespace D3B.App.ViewModels.Maps;

/// <summary>The ways off a map the world graph holds, for the map to draw (D121).</summary>
/// <remarks>
/// Criteria are not answered: the map shows where the graph leads, whoever may follow it. Several scrolls by one edge
/// lead to different maps, the server sending a cell to the nearest's (D71): each cell of the edge goes with the scroll
/// whose own cell is nearest it.
/// </remarks>
internal static class MapExits
{
    /// <summary>Gets a map's ways off.</summary>
    /// <param name="map">The map, from the pack.</param>
    /// <param name="graph">The world graph.</param>
    /// <returns>Its scrolls by edge and destination, its map actions by cell, and its doors by element; none for a map off the graph.</returns>
    public static IReadOnlyList<MapExit> Of(MapData map, WorldGraph graph)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(graph);

        var scrolls = new List<(int Zone, WorldTransition Transition, long To)>();
        var actions = new SortedDictionary<int, SortedSet<long>>();
        var doors = new SortedDictionary<int, (int Cell, SortedSet<long> To)>();
        var vertices = graph.Vertices.Span;

        for (var zone = 0; zone <= WorldVertex.MaxZone; zone++)
        {
            if (!graph.TryGetVertex(map.Id, zone, out var vertex))
                continue;

            foreach (var edge in graph.EdgesFrom(vertex))
            {
                // Where the character lands, which a scroll with an action does not name itself.
                var to = vertices[edge.To].MapId;

                foreach (var transition in graph.TransitionsOf(edge))
                {
                    switch (transition.Type)
                    {
                        case WorldTransitionType.Scroll or WorldTransitionType.ScrollAction:
                            scrolls.Add((zone, transition, to));
                            break;
                        case WorldTransitionType.MapAction:
                            Add(actions, transition.CellId, to);
                            break;
                        default:
                            AddDoor(map, doors, transition, to);
                            break;
                    }
                }
            }
        }

        var exits = Scrolls(map, scrolls);

        exits.AddRange(actions.Select(action => new MapExit(MapExitKind.MapAction, [action.Key], [.. action.Value])));
        exits.AddRange(doors.Select(door => new MapExit(MapExitKind.Door, [door.Value.Cell], [.. door.Value.To]) { ElementId = door.Key }));

        return exits;
    }

    private static void Add(SortedDictionary<int, SortedSet<long>> exits, int cell, long to)
    {
        if (!exits.TryGetValue(cell, out var destinations))
            exits.Add(cell, destinations = []);

        destinations.Add(to);
    }

    private static void AddDoor(MapData map, SortedDictionary<int, (int Cell, SortedSet<long> To)> doors, WorldTransition transition, long to)
    {
        if (!doors.TryGetValue(transition.ElementId, out var door))
        {
            // The element's own cell, which the transition names only when the pack places it on none.
            var cell = map.TryGetInteractive(transition.ElementId, out var element) && element.CellId != MapInteractive.NoCell
                ? element.CellId
                : transition.CellId;

            doors.Add(transition.ElementId, door = (cell, []));
        }

        door.To.Add(to);
    }

    private static List<MapExit> Scrolls(MapData map, List<(int Zone, WorldTransition Transition, long To)> scrolls)
    {
        var cells = map.Cells.Span;
        var byEdge = new SortedDictionary<(int Direction, long To), SortedSet<int>>();

        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            var mapCell = cells[cell];

            if (!mapCell.IsWalkable)
                continue;

            for (var direction = 0; direction < MapCell.DirectionCount; direction += 2)
            {
                if (MapGeometry.Neighbour(cell, direction) >= 0)
                    continue;

                var nearest = -1;

                for (var i = 0; i < scrolls.Count; i++)
                {
                    var (zone, transition, _) = scrolls[i];

                    if (zone != mapCell.Zone || transition.Direction != direction)
                        continue;

                    if (cell != transition.CellId && !MapGeometry.LeavesTowards(mapCell, cell, direction))
                        continue;

                    if (nearest < 0 || MapGeometry.Distance(cell, transition.CellId) < MapGeometry.Distance(cell, scrolls[nearest].Transition.CellId))
                        nearest = i;
                }

                if (nearest < 0)
                    continue;

                var key = (direction, scrolls[nearest].To);

                if (!byEdge.TryGetValue(key, out var edge))
                    byEdge.Add(key, edge = []);

                edge.Add(cell);
            }
        }

        return [.. byEdge.Select(edge => new MapExit(MapExitKind.Scroll, [.. edge.Value], [edge.Key.To]) { Direction = edge.Key.Direction })];
    }
}
