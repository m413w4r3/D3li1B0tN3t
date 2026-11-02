using System.Runtime.InteropServices;

namespace D3B.Data.Monsters;

/// <summary>A monster, from the client's <c>MonstersDataRoot</c>: its name, race, flags, grades and spells.</summary>
/// <remarks>
/// <para>
/// Its grades, spells and characteristic ratios are slices of <see cref="MonsterCatalog.Grades"/>,
/// <see cref="MonsterCatalog.Spells"/> and <see cref="MonsterCatalog.Ratios"/>. A ratio, with its characteristic's formula
/// in <see cref="Characteristics.CharacteristicCatalog"/>, scales the monster to a level its grades lack; the server gives
/// a fighter's characteristics, so a fight needs neither (client.md).
/// </para>
/// <para>
/// Its drops, looks and animations, achievements and idols are left out. Unmanaged and packed, as
/// <see cref="MonsterGrade"/>.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Monster
{
    /// <summary>Gets the monster's identifier, as the server names a monster fighter.</summary>
    public int Id { get; init; }

    /// <summary>Gets the text naming it, by identifier.</summary>
    public int NameId { get; init; }

    /// <summary>Gets the client's <c>gfxId</c>, which names its icon in <see cref="Icons.IconCatalog.Monsters"/>, held for the monsters a sub-area lists.</summary>
    public ushort GfxId { get; init; }

    /// <summary>Gets its race, in <see cref="MonsterCatalog.Races"/>.</summary>
    public short Race { get; init; }

    /// <summary>Gets its properties.</summary>
    public MonsterProperties Properties { get; init; }

    /// <summary>Gets the index of its grade 1 in <see cref="MonsterCatalog.Grades"/>.</summary>
    public int FirstGrade { get; init; }

    /// <summary>Gets how many grades it has.</summary>
    public byte GradeCount { get; init; }

    /// <summary>Gets the index of its first spell in <see cref="MonsterCatalog.Spells"/>.</summary>
    public int FirstSpell { get; init; }

    /// <summary>Gets how many spells it has.</summary>
    public byte SpellCount { get; init; }

    /// <summary>Gets the index of its first characteristic ratio in <see cref="MonsterCatalog.Ratios"/>.</summary>
    public int FirstRatio { get; init; }

    /// <summary>Gets how many characteristic ratios it has: 10, or none.</summary>
    public byte RatioCount { get; init; }

    /// <summary>Gets the client's <c>scaleGradeRef</c>, a grade.</summary>
    public byte ScaleGradeRef { get; init; }

    /// <summary>Gets the client's <c>summonCost</c>, from 0 to 3.</summary>
    public short SummonCost { get; init; }

    /// <summary>Gets the client's <c>isBounty</c>.</summary>
    public bool IsBounty { get; init; }

    /// <summary>Gets the client's <c>aggressiveZoneSize</c>.</summary>
    public byte AggressiveZoneSize { get; init; }

    /// <summary>Gets the client's <c>aggressiveLevelDiff</c>.</summary>
    public short AggressiveLevelDiff { get; init; }

    /// <summary>Gets the client's <c>aggressiveAttackDelay</c>.</summary>
    public short AggressiveAttackDelay { get; init; }

    /// <summary>Gets the client's <c>correspondingMiniBossId</c>, a monster, or 0.</summary>
    public ushort CorrespondingMiniBossId { get; init; }

    /// <summary>Gets the client's <c>speedAdjust</c>.</summary>
    public sbyte SpeedAdjust { get; init; }
}
