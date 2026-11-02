using System.Runtime.InteropServices;

namespace D3B.Data.Monsters;

/// <summary>A grade of a monster, from the client's <c>MonsterGradeData</c>: its level and characteristics.</summary>
/// <remarks>
/// A monster's grades are numbered from 1, in order, in every monster of client 3.7.12.12. The server names a monster
/// fighter by its monster and grade, and gave each of the 54 the captures hold the grade's level, life, AP and MP
/// (client.md). Unmanaged and packed: MemoryPack copies an array of it as raw bytes, so its layout is part of the pack
/// format.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MonsterGrade
{
    /// <summary>The <see cref="StartingSpellLevelId"/> of a grade that casts nothing as it comes into a fight.</summary>
    public const int NoStartingSpell = 0;

    /// <summary>Gets the grade, from 1.</summary>
    public byte Grade { get; init; }

    /// <summary>Gets the monster's level at this grade.</summary>
    public ushort Level { get; init; }

    /// <summary>Gets the client's <c>xp</c>.</summary>
    public int Experience { get; init; }

    /// <summary>Gets the spell level the monster casts as it comes into a fight, or <see cref="NoStartingSpell"/>.</summary>
    public int StartingSpellLevelId { get; init; }

    /// <summary>Gets its characteristics.</summary>
    public MonsterCharacteristics Characteristics { get; init; }

    /// <summary>Gets the client's <c>bonusCharacteristics</c>, set on 277 grades of 105 monsters of client 3.7.12.12, summons.</summary>
    public MonsterCharacteristics Bonus { get; init; }
}
