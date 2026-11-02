using System.Runtime.InteropServices;

namespace D3B.Data.Breeds;

/// <summary>A breed, a character's class, from the client's <c>BreedsDataRoot</c>: its name, spells, roles and the cost of its characteristics.</summary>
/// <remarks>
/// Its spells, roles and costs are slices of <see cref="BreedCatalog.Spells"/>, <see cref="BreedCatalog.BreedRoles"/>
/// and <see cref="BreedCatalog.StatCosts"/>. Its descriptions, looks and colours are left out. Unmanaged and packed:
/// MemoryPack copies an array of it as raw bytes, so its layout is part of the pack format.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Breed
{
    /// <summary>Gets the breed's identifier, as the server names a character's.</summary>
    public int Id { get; init; }

    /// <summary>Gets the text naming it, by identifier: the client's <c>shortNameId</c>, which it writes as text.</summary>
    public int NameId { get; init; }

    /// <summary>Gets the index of its first spell in <see cref="BreedCatalog.Spells"/>.</summary>
    public int FirstSpell { get; init; }

    /// <summary>Gets how many spells it has: 22 for each breed of client 3.7.12.12.</summary>
    public byte SpellCount { get; init; }

    /// <summary>Gets the index of its first role in <see cref="BreedCatalog.BreedRoles"/>.</summary>
    public int FirstRole { get; init; }

    /// <summary>Gets how many roles it has.</summary>
    public byte RoleCount { get; init; }

    /// <summary>Gets the index of its first cost in <see cref="BreedCatalog.StatCosts"/>.</summary>
    public int FirstStatCost { get; init; }

    /// <summary>Gets how many costs it has.</summary>
    public byte StatCostCount { get; init; }
}
