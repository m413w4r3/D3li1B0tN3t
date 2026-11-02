using MemoryPack;

namespace D3B.Data.Monsters;

/// <summary>The monsters, their grades and spells, and their races.</summary>
/// <remarks>
/// Each table is sorted by identifier and found by binary search; a monster's grades, spells and ratios are slices of
/// arrays the catalog holds (<see cref="Tables"/>). Loaded once and shared: nothing in it changes.
/// </remarks>
[MemoryPackable]
public sealed partial class MonsterCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "monsters/catalog";

    /// <summary>Gets the monsters, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Monster> Monsters { get; }

    /// <summary>Gets the monsters' grades, each monster's in order of grade, in the order of the monsters.</summary>
    public ReadOnlyMemory<MonsterGrade> Grades { get; }

    /// <summary>Gets the monsters' spells, each monster's together, in the order of the monsters.</summary>
    public ReadOnlyMemory<MonsterSpell> Spells { get; }

    /// <summary>Gets the spells' grades at each of their monster's grades, each spell's together, in the order of the spells.</summary>
    public ReadOnlyMemory<byte> SpellGrades { get; }

    /// <summary>Gets the monsters' characteristic ratios, each monster's together, in the order of the monsters.</summary>
    public ReadOnlyMemory<MonsterRatio> Ratios { get; }

    /// <summary>Gets the races, in ascending order of identifier.</summary>
    public ReadOnlyMemory<MonsterRace> Races { get; }

    /// <summary>Gets the super-races, in ascending order of identifier.</summary>
    public ReadOnlyMemory<MonsterRace> SuperRaces { get; }

    /// <summary>Initializes a new instance of the <see cref="MonsterCatalog"/> class.</summary>
    /// <param name="monsters">The monsters, in strictly ascending order of identifier, each naming its own grades, spells and ratios.</param>
    /// <param name="grades">The grades, each monster's together, in the order of the monsters.</param>
    /// <param name="spells">The spells, each monster's together, in the order of the monsters, each naming its own grades.</param>
    /// <param name="spellGrades">The spells' grades, each spell's together, in the order of the spells.</param>
    /// <param name="ratios">The ratios, each monster's together, in the order of the monsters.</param>
    /// <param name="races">The races, in strictly ascending order of identifier.</param>
    /// <param name="superRaces">The super-races, in strictly ascending order of identifier.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order, or a slice does not follow the one before.</exception>
    [MemoryPackConstructor]
    public MonsterCatalog(
        ReadOnlyMemory<Monster> monsters,
        ReadOnlyMemory<MonsterGrade> grades,
        ReadOnlyMemory<MonsterSpell> spells,
        ReadOnlyMemory<byte> spellGrades,
        ReadOnlyMemory<MonsterRatio> ratios,
        ReadOnlyMemory<MonsterRace> races,
        ReadOnlyMemory<MonsterRace> superRaces)
    {
        Tables.CheckAscending(monsters.Span, static monster => monster.Id, nameof(monsters));
        Tables.CheckAscending(races.Span, static race => race.Id, nameof(races));
        Tables.CheckAscending(superRaces.Span, static race => race.Id, nameof(superRaces));
        Tables.CheckSlices(monsters.Span, static monster => (monster.FirstGrade, monster.GradeCount), grades.Length, nameof(grades));
        Tables.CheckSlices(monsters.Span, static monster => (monster.FirstSpell, monster.SpellCount), spells.Length, nameof(spells));
        Tables.CheckSlices(monsters.Span, static monster => (monster.FirstRatio, monster.RatioCount), ratios.Length, nameof(ratios));
        Tables.CheckSlices(spells.Span, static spell => (spell.FirstGrade, spell.GradeCount), spellGrades.Length, nameof(spellGrades));

        Monsters = monsters;
        Grades = grades;
        Spells = spells;
        SpellGrades = spellGrades;
        Ratios = ratios;
        Races = races;
        SuperRaces = superRaces;
    }

    /// <summary>Finds a monster, such as one the server names a fighter by.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="monster">The monster, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetMonster(int id, out Monster monster)
    {
        return Tables.TryFind(Monsters.Span, id, static monster => monster.Id, out monster);
    }

    /// <summary>Finds a grade of a monster.</summary>
    /// <param name="monsterId">The monster.</param>
    /// <param name="grade">The grade, from 1.</param>
    /// <param name="found">The grade, when found.</param>
    /// <returns>Whether the catalog holds the monster and the monster that grade.</returns>
    public bool TryGetGrade(int monsterId, int grade, out MonsterGrade found)
    {
        if (TryGetMonster(monsterId, out var monster) && grade >= 1 && grade <= monster.GradeCount)
        {
            found = GradesOf(monster)[grade - 1];

            return true;
        }

        found = default;

        return false;
    }

    /// <summary>Gets a monster's grades.</summary>
    /// <param name="monster">A monster of the catalog.</param>
    /// <returns>Its grades, grade 1 first.</returns>
    public ReadOnlySpan<MonsterGrade> GradesOf(in Monster monster)
    {
        return Grades.Span.Slice(monster.FirstGrade, monster.GradeCount);
    }

    /// <summary>Gets a monster's spells.</summary>
    /// <param name="monster">A monster of the catalog.</param>
    /// <returns>Its spells, in the client's order.</returns>
    public ReadOnlySpan<MonsterSpell> SpellsOf(in Monster monster)
    {
        return Spells.Span.Slice(monster.FirstSpell, monster.SpellCount);
    }

    /// <summary>Gets a monster spell's grade at one of the monster's grades.</summary>
    /// <param name="spell">A spell of a monster of the catalog.</param>
    /// <param name="grade">The monster's grade, from 1.</param>
    /// <returns>The spell's grade, or 0 where the client names none.</returns>
    public byte SpellGradeOf(in MonsterSpell spell, int grade)
    {
        return grade >= 1 && grade <= spell.GradeCount ? SpellGrades.Span[spell.FirstGrade + grade - 1] : (byte)0;
    }

    /// <summary>Gets a monster's characteristic ratios.</summary>
    /// <param name="monster">A monster of the catalog.</param>
    /// <returns>Its ratios.</returns>
    public ReadOnlySpan<MonsterRatio> RatiosOf(in Monster monster)
    {
        return Ratios.Span.Slice(monster.FirstRatio, monster.RatioCount);
    }

    /// <summary>Finds a race.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="race">The race, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetRace(int id, out MonsterRace race)
    {
        return Tables.TryFind(Races.Span, id, static race => race.Id, out race);
    }

    /// <summary>Finds a super-race.</summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="race">The super-race, when found.</param>
    /// <returns>Whether the catalog holds it.</returns>
    public bool TryGetSuperRace(int id, out MonsterRace race)
    {
        return Tables.TryFind(SuperRaces.Span, id, static race => race.Id, out race);
    }
}
