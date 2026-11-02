namespace D3B.Data.Maps;

/// <summary>The properties of a map cell, each a boolean of the client's cell data.</summary>
/// <remarks>
/// Each flag names the client field it comes from. Walkability and line of sight are what the client's fields say;
/// what the others mean is Dofus 2's meaning, not measured on Dofus 3 yet (client.md).
/// </remarks>
[Flags]
public enum MapCellProperties : ushort
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary>A character may stand on the cell and walk through it: <c>mov</c>.</summary>
    Walkable = 1 << 0,

    /// <summary>The cell lets lines of sight through: <c>los</c>.</summary>
    LineOfSight = 1 << 1,

    /// <summary>The cell cannot be walked on in a fight, whatever <see cref="Walkable"/> says: <c>nonWalkableDuringFight</c>, set on maps of the <c>MapMetadata</c> layout only.</summary>
    NonWalkableDuringFight = 1 << 2,

    /// <summary>The cell cannot be walked on outside fights, whatever <see cref="Walkable"/> says: <c>nonWalkableDuringRP</c>, set on maps of the <c>MapMetadata</c> layout only.</summary>
    NonWalkableDuringRoleplay = 1 << 3,

    /// <summary><c>farmCell</c>: in Dofus 2, a cell of a paddock.</summary>
    Farm = 1 << 4,

    /// <summary><c>visible</c>, set on every cell of client 3.7.12.12.</summary>
    Visible = 1 << 5,

    /// <summary><c>havenbagCell</c>: in Dofus 2, a cell where a haven bag may be opened.</summary>
    Havenbag = 1 << 6,

    /// <summary><c>roleplayMonstersMovementBlocked</c>: roaming monsters do not walk through the cell.</summary>
    MonstersBlocked = 1 << 7,

    /// <summary><c>red</c>: in Dofus 2, a placement cell of the red team when a fight starts on the map.</summary>
    Red = 1 << 8,

    /// <summary><c>blue</c>: in Dofus 2, a placement cell of the blue team when a fight starts on the map.</summary>
    Blue = 1 << 9,
}
