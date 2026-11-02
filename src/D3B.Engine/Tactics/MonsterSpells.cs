using System.Collections.Concurrent;
using System.Collections.Immutable;
using D3B.Data.Monsters;
using D3B.Data.Spells;

namespace D3B.Engine.Tactics;

/// <summary>The spells' levels a monster casts at a grade, as the pack gives them: a monster's, and a summon's the search imagines.</summary>
/// <remarks>Built once per pack and shared, from any thread; each monster's grade is read once.</remarks>
public sealed class MonsterSpells
{
    private readonly SpellCatalog _spells;
    private readonly MonsterCatalog _monsters;
    private readonly ConcurrentDictionary<(int MonsterId, int Grade), ImmutableArray<SpellLevel>> _levels = new();

    /// <summary>Initializes a new instance of the <see cref="MonsterSpells"/> class.</summary>
    /// <param name="spells">The pack's spells.</param>
    /// <param name="monsters">The pack's monsters.</param>
    public MonsterSpells(SpellCatalog spells, MonsterCatalog monsters)
    {
        ArgumentNullException.ThrowIfNull(spells);
        ArgumentNullException.ThrowIfNull(monsters);

        _spells = spells;
        _monsters = monsters;
    }

    /// <summary>Gets the levels a monster casts at a grade.</summary>
    /// <param name="monsterId">The monster.</param>
    /// <param name="grade">Its grade.</param>
    /// <returns>Each of its spells at the grade the pack gives it there; none for a monster or grade the pack lacks.</returns>
    public ImmutableArray<SpellLevel> Of(int monsterId, int grade)
    {
        return _levels.GetOrAdd((monsterId, grade), Read);
    }

    private ImmutableArray<SpellLevel> Read((int MonsterId, int Grade) key)
    {
        if (!_monsters.TryGetMonster(key.MonsterId, out var monster))
            return [];

        var levels = ImmutableArray.CreateBuilder<SpellLevel>();

        foreach (var spell in _monsters.SpellsOf(monster))
        {
            if (_monsters.SpellGradeOf(spell, key.Grade) is var spellGrade and > 0 && _spells.TryGetLevel(spell.SpellId, spellGrade, out var level))
                levels.Add(level);
        }

        return levels.DrainToImmutable();
    }
}
