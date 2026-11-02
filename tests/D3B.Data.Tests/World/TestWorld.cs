using D3B.Data.World;

namespace D3B.Data.Tests.World;

internal static class TestWorld
{
    private static readonly string[] _criteria = ["PO>1", "Qf=1234"];
    private static readonly int[] _harvestables = [289, 303];
    private static readonly int[] _monsters = [47, 31, 34];

    /// <summary>Map 100 has two zones; zone 1 scrolls east to 200, zone 2 takes a door to 300, which scrolls back to 100.</summary>
    public static WorldGraph Graph()
    {
        WorldVertex[] vertices = [new(100, 1), new(100, 2), new(200, 1), new(300, 1)];
        WorldEdge[] edges = [new(0, 2, 0, 1), new(1, 3, 1, 2), new(3, 0, 3, 1)];
        WorldTransition[] transitions =
        [
            new(WorldTransitionType.Scroll, 0, 293, WorldTransition.None, 200, WorldTransition.None, WorldTransition.None),
            new(WorldTransitionType.Interactive, WorldTransition.NoDirection, 300, 184, 300, 470_001, WorldTransition.None),
            new(WorldTransitionType.MapAction, WorldTransition.NoDirection, 312, WorldTransition.None, 300, WorldTransition.None, 0),
            new(WorldTransitionType.ScrollAction, 4, 280, WorldTransition.None, 999, WorldTransition.None, 1),
        ];

        return new WorldGraph(vertices, edges, transitions, _criteria);
    }

    /// <summary>Sub-area 10 grows wheat and ash, where abraknydes live; sub-area 11 grows nothing, where blue and green larvae live.</summary>
    public static WorldAtlas Atlas()
    {
        return new WorldAtlas(
            new MapPosition[] { new(100, 4, -18, 10, 1, 7001, 0x0400_0000), new(200, 5, -18, 10, 1, 7002, 0), new(300, 0, 0, 11, -1, 7003, 0) },
            new SubArea[] { new(10, 8001, 1, 20, 100, false, 0, 2, 0, 1), new(11, 8002, 1, 30, SubArea.NoZaap, true, 2, 0, 1, 2) },
            new Area[] { new(1, 9001, 0) },
            new Waypoint[] { new(5, 100, 10, true) },
            _harvestables,
            _monsters);
    }
}
