using System.Runtime.InteropServices;

namespace D3B.Data.Spells;

/// <summary>A state a fighter may be in, from the client's <c>SpellStatesDataRoot</c>, such as one a spell sets or a criterion names.</summary>
/// <remarks>
/// Its flags say what it does; its effects, numbers the client lists for some states, are a slice of
/// <see cref="SpellStateCatalog.StateEffects"/>, their meaning 9d's to measure. Unmanaged and packed, as <see cref="SpellLevel"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct SpellState
{
    /// <summary>Gets the state's identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets what it does.</summary>
    public SpellStateProperties Properties { get; }

    /// <summary>Gets the index of its first effect in <see cref="SpellStateCatalog.StateEffects"/>.</summary>
    public int FirstEffect { get; }

    /// <summary>Gets how many effects it has.</summary>
    public byte EffectCount { get; }

    /// <summary>Gets the client's <c>spellLevelId</c>, a spell level, or 0: set on 206 states of client 3.7.12.12.</summary>
    public int SpellLevelId { get; }

    /// <summary>Initializes a new instance of the <see cref="SpellState"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="nameId">The text naming it.</param>
    /// <param name="properties">What it does.</param>
    /// <param name="firstEffect">The index of its first effect.</param>
    /// <param name="effectCount">How many effects it has.</param>
    /// <param name="spellLevelId">The client's spellLevelId.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative.</exception>
    public SpellState(int id, int nameId, SpellStateProperties properties, int firstEffect, byte effectCount, int spellLevelId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstEffect);

        Id = id;
        NameId = nameId;
        Properties = properties;
        FirstEffect = firstEffect;
        EffectCount = effectCount;
        SpellLevelId = spellLevelId;
    }
}
