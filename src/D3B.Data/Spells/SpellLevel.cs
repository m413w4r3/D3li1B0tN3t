using System.Runtime.InteropServices;

namespace D3B.Data.Spells;

/// <summary>A grade of a spell, from the client's <c>SpellLevelsDataRoot</c>: what casting it costs, where it reaches, and what it does.</summary>
/// <remarks>
/// <para>
/// Every field of the client's, its flags among them with the bits the client's code gives them. A spell cast names its
/// level by <see cref="Id"/>, measured on every cast of the captures (client.md). The criterion on states is text, held
/// in <see cref="SpellCatalog.Strings"/>.
/// </para>
/// <para>
/// Its effects, then its critical effects, follow one another in <see cref="SpellCatalog.Effects"/>; its preview zones are a
/// slice of <see cref="SpellCatalog.PreviewZones"/>. Unmanaged and packed, as <see cref="SpellEffect"/>.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct SpellLevel
{
    /// <summary>The <see cref="StatesCriterion"/> of a level with none.</summary>
    public const int NoCriterion = -1;

    /// <summary>Gets the level's identifier, as a spell cast names it.</summary>
    public int Id { get; init; }

    /// <summary>Gets its spell.</summary>
    public int SpellId { get; init; }

    /// <summary>Gets its grade, from 1 up: a spell's levels have grades 1 to their count, in order.</summary>
    public byte Grade { get; init; }

    /// <summary>Gets the client's <c>spellBreed</c>.</summary>
    public ushort Breed { get; init; }

    /// <summary>Gets what casting it costs in AP.</summary>
    public short ApCost { get; init; }

    /// <summary>Gets the least range it is cast at.</summary>
    public byte MinRange { get; init; }

    /// <summary>Gets the most range it is cast at, before the caster's range bonus when <see cref="SpellLevelProperties.RangeCanBeBoosted"/>.</summary>
    public byte Range { get; init; }

    /// <summary>Gets the chance of a critical hit, in percent, or 0 for none.</summary>
    public short CriticalHitProbability { get; init; }

    /// <summary>Gets how many times its effects stack on a fighter, 0 or -1 for no limit.</summary>
    public sbyte MaxStack { get; init; }

    /// <summary>Gets how many times a turn it is cast, 0 or -1 for no limit.</summary>
    public sbyte MaxCastPerTurn { get; init; }

    /// <summary>Gets how many times a turn it is cast on one target, 0 for no limit.</summary>
    public byte MaxCastPerTarget { get; init; }

    /// <summary>Gets the client's <c>maxGlobalCastPerTurn</c>.</summary>
    public byte MaxGlobalCastPerTurn { get; init; }

    /// <summary>Gets the client's <c>maxGlobalCastPerTarget</c>.</summary>
    public byte MaxGlobalCastPerTarget { get; init; }

    /// <summary>Gets how many turns pass between two casts, 0 for none; 63 on 193 levels of client 3.7.12.12.</summary>
    public byte MinCastInterval { get; init; }

    /// <summary>Gets how many turns pass before the first cast.</summary>
    public sbyte InitialCooldown { get; init; }

    /// <summary>Gets the client's <c>globalCooldown</c>.</summary>
    public sbyte GlobalCooldown { get; init; }

    /// <summary>Gets the character level it needs.</summary>
    public short MinPlayerLevel { get; init; }

    /// <summary>Gets the index of its criterion on the caster's states in <see cref="SpellCatalog.Strings"/>, or <see cref="NoCriterion"/>.</summary>
    public int StatesCriterion { get; init; }

    /// <summary>Gets the client's <c>ignorePrisonGlyph</c>.</summary>
    public bool IgnoresPrisonGlyph { get; init; }

    /// <summary>Gets its properties.</summary>
    public SpellLevelProperties Properties { get; init; }

    /// <summary>Gets the index of its first effect in <see cref="SpellCatalog.Effects"/>; its critical effects follow its effects.</summary>
    public int FirstEffect { get; init; }

    /// <summary>Gets how many effects it has: 465 at most in client 3.7.12.12.</summary>
    public ushort EffectCount { get; init; }

    /// <summary>Gets how many critical effects it has, after its effects.</summary>
    public ushort CriticalEffectCount { get; init; }

    /// <summary>Gets the index of its first preview zone in <see cref="SpellCatalog.PreviewZones"/>.</summary>
    public int FirstPreviewZone { get; init; }

    /// <summary>Gets how many preview zones it has.</summary>
    public byte PreviewZoneCount { get; init; }
}
