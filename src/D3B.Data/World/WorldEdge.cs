using System.Runtime.InteropServices;

namespace D3B.Data.World;

/// <summary>An edge of the world graph: the transitions that lead from one vertex to another.</summary>
/// <remarks>Unmanaged and packed, as <see cref="WorldVertex"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct WorldEdge
{
    /// <summary>Gets the index of the vertex the edge leaves, in <see cref="WorldGraph.Vertices"/>.</summary>
    public int From { get; }

    /// <summary>Gets the index of the vertex the edge reaches.</summary>
    public int To { get; }

    /// <summary>Gets the index of the edge's first transition in <see cref="WorldGraph.Transitions"/>.</summary>
    public int FirstTransition { get; }

    /// <summary>Gets how many transitions the edge has, at least one: any of them leads from one vertex to the other.</summary>
    public int TransitionCount { get; }

    /// <summary>Initializes a new instance of the <see cref="WorldEdge"/> struct.</summary>
    /// <param name="from">The index of the vertex the edge leaves.</param>
    /// <param name="to">The index of the vertex it reaches.</param>
    /// <param name="firstTransition">The index of its first transition.</param>
    /// <param name="transitionCount">How many transitions it has.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is negative, or the edge has no transition.</exception>
    public WorldEdge(int from, int to, int firstTransition, int transitionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(from);
        ArgumentOutOfRangeException.ThrowIfNegative(to);
        ArgumentOutOfRangeException.ThrowIfNegative(firstTransition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(transitionCount);

        From = from;
        To = to;
        FirstTransition = firstTransition;
        TransitionCount = transitionCount;
    }
}
