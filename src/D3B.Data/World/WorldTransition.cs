using System.Runtime.InteropServices;
using D3B.Data.Maps;

namespace D3B.Data.World;

/// <summary>One way to follow an edge of the world graph: what to do on the edge's source map to reach its destination.</summary>
/// <remarks>Unmanaged and packed, as <see cref="WorldVertex"/>; its criterion is held by the graph, by index.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct WorldTransition
{
    /// <summary>The direction of a transition that has none: most map actions and interactive elements.</summary>
    public const int NoDirection = 255;

    /// <summary>The skill, element or criterion of a transition that has none.</summary>
    public const int None = -1;

    /// <summary>Gets how the transition leaves the map.</summary>
    public WorldTransitionType Type { get; }

    /// <summary>Gets the direction to leave in, from 0 (east) clockwise to 7, or <see cref="NoDirection"/>.</summary>
    /// <remarks>Scrolls always have one of 0, 2, 4 and 6: east, south, west and north.</remarks>
    public byte Direction { get; }

    /// <summary>Gets the cell of the source map the transition starts from: where to walk, or the element to use.</summary>
    public short CellId { get; }

    /// <summary>Gets the skill to use the element with, for an <see cref="WorldTransitionType.Interactive"/> transition, else <see cref="None"/>.</summary>
    public int SkillId { get; }

    /// <summary>Gets the map the client asks to change to: the edge's destination, or the neighbour for a <see cref="WorldTransitionType.ScrollAction"/>.</summary>
    public long MapId { get; }

    /// <summary>Gets the interactive element to use, for an <see cref="WorldTransitionType.Interactive"/> transition, else <see cref="None"/>.</summary>
    public int ElementId { get; }

    /// <summary>Gets the index of the transition's criterion in <see cref="WorldGraph.Criteria"/>, or <see cref="None"/> when anyone may follow it.</summary>
    public int CriterionId { get; }

    /// <summary>Initializes a new instance of the <see cref="WorldTransition"/> struct.</summary>
    /// <param name="type">How the transition leaves the map.</param>
    /// <param name="direction">The direction to leave in, or <see cref="NoDirection"/>.</param>
    /// <param name="cellId">The cell it starts from.</param>
    /// <param name="skillId">The skill to use, or <see cref="None"/>.</param>
    /// <param name="mapId">The map the client asks to change to.</param>
    /// <param name="elementId">The element to use, or <see cref="None"/>.</param>
    /// <param name="criterionId">The index of its criterion, or <see cref="None"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is outside what the field holds.</exception>
    public WorldTransition(WorldTransitionType type, int direction, int cellId, int skillId, long mapId, int elementId, int criterionId)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown transition type.");

        if (direction is not NoDirection and (< 0 or >= MapCell.DirectionCount))
            throw new ArgumentOutOfRangeException(nameof(direction), direction, $"A direction lies between 0 and {MapCell.DirectionCount - 1}, or is {NoDirection} for none.");

        ArgumentOutOfRangeException.ThrowIfNegative(cellId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cellId, MapData.CellCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(skillId, None);
        ArgumentOutOfRangeException.ThrowIfLessThan(elementId, None);
        ArgumentOutOfRangeException.ThrowIfLessThan(criterionId, None);

        Type = type;
        Direction = (byte)direction;
        CellId = (short)cellId;
        SkillId = skillId;
        MapId = mapId;
        ElementId = elementId;
        CriterionId = criterionId;
    }
}
