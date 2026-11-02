using System.Runtime.InteropServices;

namespace D3B.Data.Spells;

/// <summary>A Rogue's bomb, from the client's <c>SpellBombsDataRoot</c>: the spells it casts, and the wall it makes with its kind.</summary>
/// <remarks>
/// Its identifier is the bomb's monster: 7 of the 10 bombs of client 3.7.12.12 are monsters, and only they name spells the
/// client holds (client.md). Unmanaged and packed, as <see cref="SpellLevel"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct SpellBomb
{
    /// <summary>Gets the bomb's identifier: its monster's.</summary>
    public int Id { get; }

    /// <summary>Gets the spell it casts when another bomb's explosion reaches it.</summary>
    public int ChainReactionSpellId { get; }

    /// <summary>Gets the spell it casts when it explodes.</summary>
    public int ExplosionSpellId { get; }

    /// <summary>Gets its wall, in <see cref="SpellCatalog.BombWalls"/>.</summary>
    public int WallId { get; }

    /// <summary>Gets the client's <c>instantSpellId</c>.</summary>
    public int InstantSpellId { get; }

    /// <summary>Gets the client's <c>comboCoeff</c>.</summary>
    public int ComboCoefficient { get; }

    /// <summary>Initializes a new instance of the <see cref="SpellBomb"/> struct.</summary>
    /// <param name="id">Its monster.</param>
    /// <param name="chainReactionSpellId">The spell of its chain reaction.</param>
    /// <param name="explosionSpellId">The spell of its explosion.</param>
    /// <param name="wallId">Its wall.</param>
    /// <param name="instantSpellId">The client's instantSpellId.</param>
    /// <param name="comboCoefficient">The client's comboCoeff.</param>
    public SpellBomb(int id, int chainReactionSpellId, int explosionSpellId, int wallId, int instantSpellId, int comboCoefficient)
    {
        Id = id;
        ChainReactionSpellId = chainReactionSpellId;
        ExplosionSpellId = explosionSpellId;
        WallId = wallId;
        InstantSpellId = instantSpellId;
        ComboCoefficient = comboCoefficient;
    }
}
