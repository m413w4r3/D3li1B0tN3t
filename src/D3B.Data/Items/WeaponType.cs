using System.Runtime.InteropServices;

namespace D3B.Data.Items;

/// <summary>A type of weapon, such as a sword or a hammer, and the zone its blows hit, from the client's <c>ItemTypesDataRoot</c>.</summary>
/// <remarks>
/// The client writes the zone as text, its <c>rawZone</c>, such as <c>P</c> for the cell struck or <c>X1,0,10,1</c>: a
/// shape's character, then numbers its code reads. It is kept as it is, in <see cref="WeaponCatalog.Zones"/>, until blows
/// of each shape are measured (client.md). Unmanaged and packed, as <see cref="Item"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct WeaponType
{
    /// <summary>Gets the type's identifier, as <see cref="ItemType.Id"/>.</summary>
    public int Id { get; init; }

    /// <summary>Gets the index of its zone's text in <see cref="WeaponCatalog.Zones"/>.</summary>
    public int Zone { get; init; }
}
