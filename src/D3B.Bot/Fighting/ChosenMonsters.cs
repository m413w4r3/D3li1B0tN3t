using D3B.Data.Monsters;

namespace D3B.Bot.Fighting;

/// <summary>The monsters a fighting profile chooses: those it names, and those of the families it names, a family being a monster's race, as the bestiary names them (D236, D247).</summary>
public static class ChosenMonsters
{
    /// <summary>Tells whether settings choose a monster, by name or by its family.</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <param name="monsters">The pack's monsters, which give a monster's family.</param>
    /// <param name="monsterId">The monster.</param>
    /// <returns>Whether they choose it.</returns>
    public static bool Holds(FightSettings settings, MonsterCatalog monsters, int monsterId)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(monsters);

        return settings.Monsters.Contains(monsterId) || (settings.Families.Count > 0 && FamilyOf(monsters, monsterId) is { } family && settings.Families.Contains(family));
    }

    /// <summary>Tells whether settings choose any monster, by name or by family.</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <returns>Whether they do.</returns>
    public static bool Any(FightSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.Monsters.Count > 0 || settings.Families.Count > 0;
    }

    /// <summary>Gets a monster's family: its race, such as the bouftous or the animalades.</summary>
    /// <param name="monsters">The pack's monsters.</param>
    /// <param name="monsterId">The monster.</param>
    /// <returns>Its family, or <see langword="null"/> for a monster unknown or of a race the pack does not hold.</returns>
    public static int? FamilyOf(MonsterCatalog monsters, int monsterId)
    {
        ArgumentNullException.ThrowIfNull(monsters);

        return monsters.TryGetMonster(monsterId, out var monster) && monsters.TryGetRace(monster.Race, out var race) ? race.Id : null;
    }
}
