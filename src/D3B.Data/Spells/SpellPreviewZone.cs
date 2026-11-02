using System.Runtime.InteropServices;

namespace D3B.Data.Spells;

/// <summary>A zone the client previews for a spell level, from its <c>PreviewSpellZoneDescr</c>: 469 on 317 levels of client 3.7.12.12.</summary>
/// <remarks>Its masks are text, held in <see cref="SpellCatalog.Strings"/>. Unmanaged and packed, as <see cref="SpellLevel"/>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct SpellPreviewZone
{
    /// <summary>Gets the client's <c>id</c>.</summary>
    public uint Id { get; init; }

    /// <summary>Gets the zone shown.</summary>
    public SpellZone Display { get; init; }

    /// <summary>Gets the client's <c>isPreviewZoneHidden</c>.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Gets the index of the client's <c>casterMask</c> in <see cref="SpellCatalog.Strings"/>.</summary>
    public int CasterMask { get; init; }

    /// <summary>Gets the zone that activates it.</summary>
    public SpellZone Activation { get; init; }

    /// <summary>Gets the index of the client's <c>activationMask</c> in <see cref="SpellCatalog.Strings"/>.</summary>
    public int ActivationMask { get; init; }
}
