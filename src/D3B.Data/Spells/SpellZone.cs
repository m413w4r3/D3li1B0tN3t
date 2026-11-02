using System.Runtime.InteropServices;

namespace D3B.Data.Spells;

/// <summary>The cells an effect reaches around the cell a spell is cast on, from the client's <c>SpellZoneDescr</c>.</summary>
/// <remarks>
/// Dofus 2 wrote a zone as text; Dofus 3 holds its parts as numbers, kept as they are. Which cells a shape covers is
/// <c>D3B.Engine</c>'s <c>SpellZones</c> (D187), and how damage decreases away from its centre 9d's. Unmanaged and packed:
/// its cells, listed for <see cref="SpellZoneShape.Custom"/> zones and the whole map's, are a slice of
/// <see cref="SpellCatalog.ZoneCells"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct SpellZone
{
    /// <summary>Gets the zone's shape.</summary>
    public SpellZoneShape Shape { get; init; }

    /// <summary>Gets the client's <c>param1</c>, which its code reads as the zone's size.</summary>
    public byte Size { get; init; }

    /// <summary>Gets the client's <c>param2</c>, which its code reads as the zone's least size.</summary>
    public byte MinSize { get; init; }

    /// <summary>Gets by how many percent damage decreases each step away from the centre: 10 on most zones.</summary>
    public sbyte DamageDecreaseStepPercent { get; init; }

    /// <summary>Gets how many steps at most damage decreases: 4 on most zones.</summary>
    public sbyte MaxDamageDecreaseApplyCount { get; init; }

    /// <summary>Gets whether the zone stops at the first fighter it meets.</summary>
    public bool StopsAtTarget { get; init; }

    /// <summary>Gets the direction the zone is forced in, from 0 to 7, or -1 for none.</summary>
    public sbyte ForcedDirection { get; init; }

    /// <summary>Gets whether a fighter carried in the zone is reached too.</summary>
    public bool IncludesCarried { get; init; }

    /// <summary>Gets whether only the cells in the caster's line of sight are reached.</summary>
    public bool OnlyInLineOfSight { get; init; }

    /// <summary>Gets the index of the zone's first listed cell in <see cref="SpellCatalog.ZoneCells"/>.</summary>
    public int FirstCell { get; init; }

    /// <summary>Gets how many cells the zone lists: 161 at most in client 3.7.12.12, on effects alone.</summary>
    public byte CellCount { get; init; }
}
