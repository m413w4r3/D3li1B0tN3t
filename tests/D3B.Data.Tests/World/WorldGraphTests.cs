using D3B.Data.Tests.Packs;
using D3B.Data.World;

namespace D3B.Data.Tests.World;

public sealed class WorldGraphTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void AVertexIsFoundByItsMapAndZone()
    {
        var graph = TestWorld.Graph();

        Assert.True(graph.TryGetVertex(100, 2, out var vertex));
        Assert.Equal(1, vertex);
        Assert.False(graph.TryGetVertex(100, 3, out _));
        Assert.False(graph.TryGetVertex(150, 1, out _));
        Assert.True(graph.Contains(300));
        Assert.False(graph.Contains(400));
    }

    [Fact]
    public void EdgesAreListedByTheirSourceVertex()
    {
        var graph = TestWorld.Graph();

        Assert.Equal([2], graph.EdgesFrom(0).ToArray().Select(edge => edge.To));
        Assert.Equal([3], graph.EdgesFrom(1).ToArray().Select(edge => edge.To));
        Assert.Empty(graph.EdgesFrom(2).ToArray());
        Assert.Equal([0], graph.EdgesFrom(3).ToArray().Select(edge => edge.To));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.EdgesFrom(4).Length);
    }

    [Fact]
    public void AnEdgeGivesItsTransitionsAndTheirCriteria()
    {
        var graph = TestWorld.Graph();
        var transitions = graph.TransitionsOf(graph.EdgesFrom(1)[0]).ToArray();

        Assert.Equal([WorldTransitionType.Interactive, WorldTransitionType.MapAction], transitions.Select(transition => transition.Type));
        Assert.Equal(470_001, transitions[0].ElementId);
        Assert.Null(graph.CriterionOf(transitions[0]));
        Assert.Equal("PO>1", graph.CriterionOf(transitions[1]));
    }

    [Fact]
    public void VerticesOutOfOrderAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new WorldGraph(new WorldVertex[] { new(200, 1), new(100, 1) }, ReadOnlyMemory<WorldEdge>.Empty, ReadOnlyMemory<WorldTransition>.Empty, ReadOnlyMemory<string>.Empty));
        Assert.Throws<ArgumentException>(() => new WorldGraph(new WorldVertex[] { new(100, 1), new(100, 1) }, ReadOnlyMemory<WorldEdge>.Empty, ReadOnlyMemory<WorldTransition>.Empty, ReadOnlyMemory<string>.Empty));
    }

    [Fact]
    public void EdgesThatDoNotFitTheGraphAreRejected()
    {
        WorldVertex[] vertices = [new(100, 1), new(200, 1)];
        WorldTransition[] one = [new(WorldTransitionType.Scroll, 0, 293, WorldTransition.None, 200, WorldTransition.None, WorldTransition.None)];

        Assert.Throws<ArgumentException>(() => new WorldGraph(vertices, new WorldEdge[] { new(0, 2, 0, 1) }, one, ReadOnlyMemory<string>.Empty));
        Assert.Throws<ArgumentException>(() => new WorldGraph(vertices, new WorldEdge[] { new(0, 1, 1, 1) }, one, ReadOnlyMemory<string>.Empty));
        Assert.Throws<ArgumentException>(() => new WorldGraph(vertices, new WorldEdge[] { new(0, 1, 0, 2) }, one, ReadOnlyMemory<string>.Empty));
        Assert.Throws<ArgumentException>(() => new WorldGraph(vertices, new WorldEdge[] { new(1, 0, 0, 1), new(0, 1, 1, 1) }, new[] { one[0], one[0] }, ReadOnlyMemory<string>.Empty));
    }

    [Fact]
    public void ACriterionBeyondTheTableIsRejected()
    {
        WorldVertex[] vertices = [new(100, 1), new(200, 1)];
        WorldTransition[] transitions = [new(WorldTransitionType.Scroll, 0, 293, WorldTransition.None, 200, WorldTransition.None, 0)];

        Assert.Throws<ArgumentException>(() => new WorldGraph(vertices, new WorldEdge[] { new(0, 1, 0, 1) }, transitions, ReadOnlyMemory<string>.Empty));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(-1)]
    public void ADirectionOutsideTheEightIsRejected(int direction)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldTransition(WorldTransitionType.Scroll, direction, 293, WorldTransition.None, 200, WorldTransition.None, WorldTransition.None));
    }

    [Fact]
    public void AnUnknownTransitionTypeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldTransition((WorldTransitionType)4, 0, 293, WorldTransition.None, 200, WorldTransition.None, WorldTransition.None));
    }

    [Fact]
    public void AVertexZoneIsTheHighBitsOfALinkedZone()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldVertex(100, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldVertex(100, WorldVertex.MaxZone + 1));
    }

    [Fact]
    public void AGraphReadsBackFromAPack()
    {
        var graph = TestWorld.Graph();

        using var pack = TestPacks.Open(_directory, TestPacks.Write(writer => writer.Add(WorldGraph.EntryName, graph)));

        var read = pack.Read<WorldGraph>(WorldGraph.EntryName);

        Assert.Equal(graph.Vertices.ToArray(), read.Vertices.ToArray());
        Assert.Equal(graph.Edges.ToArray(), read.Edges.ToArray());
        Assert.Equal(graph.Transitions.ToArray(), read.Transitions.ToArray());
        Assert.Equal(graph.Criteria.ToArray(), read.Criteria.ToArray());
        Assert.Equal([0], read.EdgesFrom(3).ToArray().Select(edge => edge.To));
    }
}
