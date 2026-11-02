using System.Runtime.InteropServices;

namespace D3B.Data.Spells;

/// <summary>The wall between two bombs of a kind, from the client's <c>SpellBombWallsDataRoot</c>.</summary>
/// <remarks>Unmanaged and packed, as <see cref="SpellLevel"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct SpellBombWall
{
    /// <summary>Gets the wall's identifier, as a bomb names it.</summary>
    public int Id { get; }

    /// <summary>Gets the spell the wall casts.</summary>
    public int SpellId { get; }

    /// <summary>Gets the client's <c>color</c>, an RGB colour.</summary>
    public int Color { get; }

    /// <summary>Gets whether the two bombs must stand in a line.</summary>
    public bool Linear { get; }

    /// <summary>Gets the least distance between the two bombs.</summary>
    public int MinHop { get; }

    /// <summary>Gets the most distance between the two bombs.</summary>
    public int MaxHop { get; }

    /// <summary>Initializes a new instance of the <see cref="SpellBombWall"/> struct.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="spellId">Its spell.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="linear">Whether the bombs stand in a line.</param>
    /// <param name="minHop">The least distance between the bombs.</param>
    /// <param name="maxHop">The most distance between the bombs.</param>
    public SpellBombWall(int id, int spellId, int color, bool linear, int minHop, int maxHop)
    {
        Id = id;
        SpellId = spellId;
        Color = color;
        Linear = linear;
        MinHop = minHop;
        MaxHop = maxHop;
    }
}
