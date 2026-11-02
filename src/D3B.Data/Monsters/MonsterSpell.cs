using System.Runtime.InteropServices;

namespace D3B.Data.Monsters;

/// <summary>A spell of a monster, and its grade at each of the monster's grades, from the client's <c>spells</c> and <c>spellGrades</c>.</summary>
/// <remarks>
/// The client writes each spell's grades as text, a pair of the spell's grade and the monster grade's level for each of the
/// monster's grades; the pack keeps the spell's grade of each, from <see cref="MonsterCatalog.SpellGrades"/>, 0 where the
/// client names none (client.md). Unmanaged and packed, as <see cref="MonsterGrade"/>.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct MonsterSpell
{
    /// <summary>Gets the spell.</summary>
    public int SpellId { get; }

    /// <summary>Gets the index of the spell's grade at the monster's grade 1 in <see cref="MonsterCatalog.SpellGrades"/>.</summary>
    public int FirstGrade { get; }

    /// <summary>Gets how many grades follow: the monster's.</summary>
    public byte GradeCount { get; }

    /// <summary>Initializes a new instance of the <see cref="MonsterSpell"/> struct.</summary>
    /// <param name="spellId">The spell.</param>
    /// <param name="firstGrade">The index of its grade at the monster's grade 1.</param>
    /// <param name="gradeCount">How many grades follow.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative.</exception>
    public MonsterSpell(int spellId, int firstGrade, byte gradeCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstGrade);

        SpellId = spellId;
        FirstGrade = firstGrade;
        GradeCount = gradeCount;
    }
}
