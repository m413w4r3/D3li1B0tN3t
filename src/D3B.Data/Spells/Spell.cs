using System.Runtime.InteropServices;

namespace D3B.Data.Spells;

/// <summary>A spell, from the client's <c>SpellsDataRoot</c>: its name, flags and levels.</summary>
/// <remarks>
/// Its levels, by grade, are a slice of <see cref="SpellCatalog.LevelIds"/>. Its description and the scripts of its
/// animations are left out: they say nothing a fight needs (client.md). Unmanaged and packed, as <see cref="SpellLevel"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Spell
{
    /// <summary>Gets the spell's identifier.</summary>
    public int Id { get; init; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; init; }

    /// <summary>Gets the client's <c>typeId</c>.</summary>
    public ushort TypeId { get; init; }

    /// <summary>Gets the client's <c>order</c>.</summary>
    public byte Order { get; init; }

    /// <summary>Gets the client's <c>iconId</c>, which <see cref="Icons.IconCatalog.Spells"/> holds for the breeds' spells and their variants, and not for the others.</summary>
    public short IconId { get; init; }

    /// <summary>Gets its properties.</summary>
    public SpellProperties Properties { get; init; }

    /// <summary>Gets the index of its grade 1 in <see cref="SpellCatalog.LevelIds"/>.</summary>
    public int FirstLevel { get; init; }

    /// <summary>Gets how many levels it has, one a grade.</summary>
    public byte LevelCount { get; init; }

    /// <summary>Gets the zone the client previews when the spell is aimed, or one of <see cref="SpellZoneShape.None"/>: 504 spells of client 3.7.12.12 have one.</summary>
    public SpellZone PreviewZone { get; init; }
}
