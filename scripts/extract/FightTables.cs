using D3B.Data.Breeds;
using D3B.Data.Characteristics;
using D3B.Data.Effects;
using D3B.Data.Monsters;
using D3B.Data.Spells;

namespace D3B.Scripts.Extract;

/// <summary>The catalogs a fight needs, read together, whose texts the pack keeps.</summary>
internal sealed class FightTables
{
    public SpellCatalog Spells { get; }

    public SpellStateCatalog States { get; }

    public EffectCatalog Effects { get; }

    public MonsterCatalog Monsters { get; }

    public BreedCatalog Breeds { get; }

    public CharacteristicCatalog Characteristics { get; }

    public FightTables(SpellCatalog spells, SpellStateCatalog states, EffectCatalog effects, MonsterCatalog monsters, BreedCatalog breeds, CharacteristicCatalog characteristics)
    {
        Spells = spells;
        States = states;
        Effects = effects;
        Monsters = monsters;
        Breeds = breeds;
        Characteristics = characteristics;
    }

    /// <summary>Gets the texts the catalogs name: the names of spells, states, monsters, races, breeds, roles and characteristics, and the effects' descriptions.</summary>
    public IEnumerable<int> Named()
    {
        foreach (var spell in Spells.Spells.Span.ToArray())
            yield return spell.NameId;

        foreach (var state in States.States.Span.ToArray())
            yield return state.NameId;

        foreach (var effect in Effects.Effects.Span.ToArray())
            yield return effect.DescriptionId;

        foreach (var monster in Monsters.Monsters.Span.ToArray())
            yield return monster.NameId;

        foreach (var race in Monsters.Races.Span.ToArray().Concat(Monsters.SuperRaces.Span.ToArray()))
            yield return race.NameId;

        foreach (var breed in Breeds.Breeds.Span.ToArray())
            yield return breed.NameId;

        foreach (var role in Breeds.Roles.Span.ToArray())
            yield return role.NameId;

        foreach (var characteristic in Characteristics.Characteristics.Span.ToArray())
            yield return characteristic.NameId;
    }
}
