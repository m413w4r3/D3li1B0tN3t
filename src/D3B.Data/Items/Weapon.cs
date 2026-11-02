using System.Runtime.InteropServices;
using D3B.Data.Spells;

namespace D3B.Data.Items;

/// <summary>A weapon, from the client's <c>WeaponData</c>: an item of <see cref="ItemCatalog"/> that a character strikes with in a fight.</summary>
/// <remarks>
/// <para>
/// A weapon's blow is spell 0, as the character's own list names it: the client asks for spell 0 on a cell, and the server
/// announces the blow in a sequence of its own, with no spell nor level (client.md). What the blow costs and where it reaches
/// are the weapon's fields, kept as the client holds them; what it does is the weapon's effects, a slice of
/// <see cref="WeaponCatalog.Effects"/>, and where it hits is its type's zone (<see cref="WeaponType"/>).
/// </para>
/// <para>Unmanaged and packed, as <see cref="Item"/>.</para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Weapon
{
    /// <summary>Gets the weapon's item, in <see cref="ItemCatalog"/>.</summary>
    public int ItemId { get; init; }

    /// <summary>Gets the item's type, whose zone the blow hits (<see cref="WeaponCatalog.TryGetType"/>).</summary>
    public ushort TypeId { get; init; }

    /// <summary>Gets what a blow costs in AP: 2 to 7 on most weapons of client 3.7.12.12.</summary>
    public short ApCost { get; init; }

    /// <summary>Gets the least range a blow is struck at.</summary>
    public byte MinRange { get; init; }

    /// <summary>Gets the most range a blow is struck at.</summary>
    public byte Range { get; init; }

    /// <summary>Gets the chance of a critical hit, in percent, or 0 for none.</summary>
    public short CriticalHitProbability { get; init; }

    /// <summary>Gets the client's <c>criticalHitBonus</c>: what a critical hit adds to the blow's damage.</summary>
    public short CriticalHitBonus { get; init; }

    /// <summary>Gets how many blows a turn the weapon strikes, 0 for no limit.</summary>
    public sbyte MaxCastPerTurn { get; init; }

    /// <summary>Gets the client's <c>castInLine</c>, <c>castInDiagonal</c> and <c>castTestLos</c>, as a spell level's bits.</summary>
    public SpellLevelProperties Properties { get; init; }

    /// <summary>Gets the index of its first effect in <see cref="WeaponCatalog.Effects"/>.</summary>
    public int FirstEffect { get; init; }

    /// <summary>Gets how many effects it has: 21 at most in client 3.7.12.12.</summary>
    public byte EffectCount { get; init; }
}
