using BenchmarkDotNet.Attributes;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.World;
using D3B.Engine.Criteria;
using D3B.Engine.World;

namespace D3B.Engine.Benchmarks.World;

/// <summary>Finds paths through the installed build's world graph, from a zaap's map: near, to the farthest map reachable, and to one that is not, on foot and teleporting.</summary>
/// <remarks>
/// The graph is the client's (D27), read from the installed build's pack, which is never committed: <c>D3B_PACK</c> names
/// a pack, else the pack of the build installed where the launcher puts it, as <c>scripts/extract.cs</c> writes it. The
/// maps are chosen from the graph, so that every build has its own: a zaap's, one 3 map changes away, the
/// farthest, and the first the search cannot reach. Teleporting, the character knows every zaap and carries what any
/// costs (D83).
/// </remarks>
[MemoryDiagnoser]
public class WorldPathfinderBenchmarks
{
    private GamePack? _pack;
    private WorldPathfinder _pathfinder = null!;
    private WorldPathRules _rules = null!;
    private WorldPathRules _teleporting = null!;
    private int _from;
    private HashSet<long> _near = [];
    private HashSet<long> _far = [];
    private HashSet<long> _unreachable = [];
    private HashSet<long> _unreachableTeleporting = [];
    private WorldStep _step;
    private MapData _map = null!;

    private static string PackPath()
    {
        var path = Environment.GetEnvironmentVariable("D3B_PACK");

        if (string.IsNullOrEmpty(path))
        {
            var client = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ankama", "Dofus-dofus3");

            path = PackLocations.PathOf(PackLocations.DefaultRoot, ClientBuild.Read(client));
        }

        return File.Exists(path) ? path : throw new FileNotFoundException($"No pack at {path}: set D3B_PACK, or extract the installed build's with scripts/extract.cs.", path);
    }

    // The fewest map changes to every vertex through transitions without a criterion, -1 where none leads: the pathfinder
    // follows them all, and finds paths at least as short.
    private static int[] Distances(WorldGraph graph, int from)
    {
        var distances = Enumerable.Repeat(-1, graph.Vertices.Length).ToArray();
        var queue = new Queue<int>([from]);

        distances[from] = 0;

        while (queue.TryDequeue(out var vertex))
        {
            foreach (var edge in graph.EdgesFrom(vertex))
            {
                if (distances[edge.To] >= 0 || !graph.TransitionsOf(edge).ToArray().Any(IsFree))
                    continue;

                distances[edge.To] = distances[vertex] + 1;
                queue.Enqueue(edge.To);
            }
        }

        return distances;
    }

    private static bool IsFree(WorldTransition transition)
    {
        return transition.CriterionId is WorldTransition.None && transition.Type is not WorldTransitionType.Interactive;
    }

    [GlobalSetup]
    public void Setup()
    {
        _pack = GamePack.Open(PackPath());

        var world = new GameWorld(_pack);
        var maps = new GameMaps(_pack);
        var graph = world.Graph;

        _pathfinder = new WorldPathfinder(graph, world.Atlas);
        _rules = new WorldPathRules { Facts = new CriterionFacts { Level = 200, Subscribed = true, Items = new Dictionary<int, int>(), JobLevels = new Dictionary<int, int>() } };
        _teleporting = _rules with { KnownZaaps = world.Atlas.Waypoints.ToArray().Select(zaap => zaap.MapId).ToHashSet(), Kamas = 1_000_000 };

        // The zaap the most maps are reached from: some stand where only doors lead.
        _from = world.Atlas.Waypoints.ToArray()
            .Select(zaap => graph.TryGetVertex(zaap.MapId, 1, out var vertex) ? vertex : -1)
            .Where(vertex => vertex >= 0)
            .MaxBy(vertex => Distances(graph, vertex).Count(distance => distance >= 0));

        var distances = Distances(graph, _from);
        var farthest = distances.Max();

        _near = [graph.Vertices.Span[Array.IndexOf(distances, 3)].MapId];
        _far = [graph.Vertices.Span[Array.IndexOf(distances, farthest)].MapId];
        _unreachable = [Enumerable.Range(0, graph.Vertices.Length)
            .Where(vertex => distances[vertex] < 0)
            .Select(vertex => graph.Vertices.Span[vertex].MapId)
            .First(map => !_pathfinder.TryFind(_from, new HashSet<long> { map }, _rules, out _))];
        _unreachableTeleporting = [Enumerable.Range(0, graph.Vertices.Length)
            .Select(vertex => graph.Vertices.Span[vertex].MapId)
            .First(map => !_pathfinder.TryFind(_from, new HashSet<long> { map }, _teleporting, out _))];

        if (!_pathfinder.TryFind(_from, _near, _rules, out var path))
            throw new InvalidOperationException("The map 3 changes away cannot be reached.");

        _step = path.Steps[0];
        _map = maps.Get(graph.Vertices.Span[_step.From].MapId);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pack?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int Near()
    {
        return Find(_near);
    }

    [Benchmark]
    public int Far()
    {
        return Find(_far);
    }

    [Benchmark]
    public int Unreachable()
    {
        return Find(_unreachable);
    }

    [Benchmark]
    public int FarTeleporting()
    {
        return Find(_far, _teleporting);
    }

    [Benchmark]
    public int UnreachableTeleporting()
    {
        return Find(_unreachableTeleporting, _teleporting);
    }

    [Benchmark]
    public int ExitCells()
    {
        return _pathfinder.ExitCells(_map, _step, _rules).Count;
    }

    private int Find(HashSet<long> maps)
    {
        return Find(maps, _rules);
    }

    private int Find(HashSet<long> maps, WorldPathRules rules)
    {
        return _pathfinder.TryFind(_from, maps, rules, out var path) ? path.Steps.Count : -1;
    }
}
