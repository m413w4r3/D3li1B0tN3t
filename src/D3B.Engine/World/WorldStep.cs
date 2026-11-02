using D3B.Data.World;

namespace D3B.Engine.World;

/// <summary>One map change of a path through the world: the transition to follow from a vertex of the graph to the next, a teleport, or an NPC's way.</summary>
/// <param name="From">The vertex the step leaves, by index in <see cref="WorldGraph.Vertices"/>.</param>
/// <param name="To">The vertex it leads to.</param>
/// <param name="TransitionIndex">The transition to follow, by index in <see cref="WorldGraph.Transitions"/>, <see cref="Teleport"/>, or an NPC's way (<see cref="OfNpcWay"/>).</param>
/// <param name="Transition">That transition, or the default for a teleport or an NPC's way.</param>
public readonly record struct WorldStep(int From, int To, int TransitionIndex, WorldTransition Transition)
{
    /// <summary>The <see cref="TransitionIndex"/> of a teleport from where the character stands to a zaap it knows, which the graph does not hold (D83).</summary>
    public const int Teleport = -1;

    /// <summary>The <see cref="TransitionIndex"/> of the first of the pathfinder's NPC ways, the others following below it (D253).</summary>
    public const int FirstNpcWay = -2;

    /// <summary>Gets whether the step teleports the character to the zaap of the map it leads to.</summary>
    public bool IsTeleport => TransitionIndex == Teleport;

    /// <summary>Gets whether the step goes through an NPC's dialog (<see cref="WorldPathfinder.NpcWays"/>).</summary>
    public bool IsNpcWay => TransitionIndex <= FirstNpcWay;

    /// <summary>Gets the NPC's way the step goes through, by index in <see cref="WorldPathfinder.NpcWays"/>, when <see cref="IsNpcWay"/>.</summary>
    public int NpcWay => FirstNpcWay - TransitionIndex;

    /// <summary>Gets the <see cref="TransitionIndex"/> of an NPC's way, which <see cref="WorldPathRules.Banned"/> names it by too.</summary>
    /// <param name="way">The way, by index in <see cref="WorldPathfinder.NpcWays"/>.</param>
    /// <returns>Its index, below <see cref="FirstNpcWay"/> but for the first.</returns>
    public static int OfNpcWay(int way)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(way);

        return FirstNpcWay - way;
    }
}
