using System.Runtime.InteropServices;

namespace D3B.Data.Items;

/// <summary>One of a weapon's effects, from the client's <c>possibleEffects</c>: a blow's damage, or a characteristic the weapon gives its wearer.</summary>
/// <remarks>
/// The effect is an effect of <see cref="Effects.EffectCatalog"/>: those used in fights are what a blow does, the others
/// what the weapon gives as it is worn, which the server counts in the character's characteristics (client.md). The
/// client's other fields hold the same on every weapon's effect of client 3.7.12.12 and are left out: the extractor fails
/// when one no longer does. Unmanaged and packed, as <see cref="Item"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct WeaponEffect
{
    /// <summary>Gets the effect, in <see cref="Effects.EffectCatalog"/>.</summary>
    public ushort EffectId { get; init; }

    /// <summary>Gets the client's <c>diceNum</c>: the least roll.</summary>
    public int DiceNum { get; init; }

    /// <summary>Gets the client's <c>diceSide</c>: the most roll, or 0 when the effect has one value.</summary>
    public int DiceSide { get; init; }

    /// <summary>Gets the client's <c>value</c>, 0 on most weapons' effects.</summary>
    public int Value { get; init; }
}
