using System.Collections.Frozen;
using D3B.Data.World;
using D3B.Engine.Criteria;

namespace D3B.Engine.World;

/// <summary>What a path through the world may follow, for one character: what its criteria are answered on, the transitions and NPC ways it avoids, and the zaaps it may teleport to.</summary>
public sealed record WorldPathRules
{
    private readonly int _teleportChanges = 1;

    /// <summary>Gets what the character's criteria are answered on: the place is each transition's own map, filled in by the pathfinder.</summary>
    /// <remarks>
    /// Its <see cref="CriterionFacts.Subscribed"/> also says where the character may go: a sub-area the client closes to
    /// accounts without a subscription is entered only when it is <see langword="true"/>, and the character teleports only
    /// then (D83).
    /// </remarks>
    public required CriterionFacts Facts { get; init; }

    /// <summary>Gets the transitions not to follow, by index in <see cref="WorldGraph.Transitions"/>, such as one that just failed, and the NPC ways by <see cref="WorldStep.OfNpcWay"/>.</summary>
    public IReadOnlySet<int> Banned { get; init; } = FrozenSet<int>.Empty;

    /// <summary>Gets the maps of the zaaps the character knows, which it may teleport to; none, the default, keeps the path from teleporting.</summary>
    public IReadOnlySet<long> KnownZaaps { get; init; } = FrozenSet<long>.Empty;

    /// <summary>Gets the maps the server refused a teleport from, whatever their flag says (<see cref="Data.World.MapPosition.AllowsTeleport"/>): no teleport is taken from them (D248).</summary>
    public IReadOnlySet<long> NoTeleportFrom { get; init; } = FrozenSet<long>.Empty;

    /// <summary>Gets the NPC ways the character was refused, by index in <see cref="WorldPathfinder.NpcWays"/>: none is taken again (D253).</summary>
    public IReadOnlySet<int> NoNpcWays { get; init; } = FrozenSet<int>.Empty;

    /// <summary>Gets the kamas the character carries, which must cover a teleport's cost (<see cref="WorldPathfinder.TeleportCost"/>).</summary>
    public long Kamas { get; init; }

    /// <summary>Gets how many map changes a teleport counts as: one is taken only when it saves at least as many (D167); 1, the default, as soon as it saves one.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is below 1.</exception>
    public int TeleportChanges
    {
        get => _teleportChanges;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _teleportChanges = value;
        }
    }
}
