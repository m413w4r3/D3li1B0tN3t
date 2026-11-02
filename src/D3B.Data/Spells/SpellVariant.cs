using System.Runtime.InteropServices;

namespace D3B.Data.Spells;

/// <summary>A spell and its variant, of which a character uses one, from the client's <c>SpellVariantsDataRoot</c>.</summary>
/// <remarks>
/// Every variant of client 3.7.12.12 pairs two spells: 22 for each of 19 breeds, and 13 of breed 19, which the breeds lack,
/// the spells every character has, such as <i>Maîtrise des Invocations</i> and <i>Liberté des Invocations</i> (client.md).
/// Unmanaged and packed, as <see cref="SpellLevel"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct SpellVariant
{
    /// <summary>The <see cref="BreedId"/> of the spells every character has.</summary>
    public const int CommonBreed = 19;

    /// <summary>Gets the variant's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the breed whose spells these are, or <see cref="CommonBreed"/>.</summary>
    public int BreedId { get; }

    /// <summary>Gets the client's first spell of the two.</summary>
    public int SpellId { get; }

    /// <summary>Gets the client's second spell of the two.</summary>
    public int VariantSpellId { get; }

    /// <summary>Initializes a new instance of the <see cref="SpellVariant"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="breedId">Its breed.</param>
    /// <param name="spellId">The first spell.</param>
    /// <param name="variantSpellId">The second spell.</param>
    public SpellVariant(int id, int breedId, int spellId, int variantSpellId)
    {
        Id = id;
        BreedId = breedId;
        SpellId = spellId;
        VariantSpellId = variantSpellId;
    }
}
