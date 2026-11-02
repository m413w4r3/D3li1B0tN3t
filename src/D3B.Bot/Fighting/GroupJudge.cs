using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Data;
using D3B.Data.Breeds;
using D3B.Data.Maps;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Engine.Effects;
using D3B.Engine.Fights;
using D3B.Engine.Tactics;
using D3B.Protocol.Game;

namespace D3B.Bot.Fighting;

/// <summary>Which group of a map the fighting activity attacks: those its settings allow, and of those the ones the AI judges the character beats (D198, D236).</summary>
/// <remarks>
/// Measured on the captures (9g): every monster of a map's groups is a grade of the pack's at that grade's level, so a group
/// is imagined as a fight from the pack alone, its monsters at their grades' life, points, characteristics and spells. The
/// character is imagined from the characteristics the server gives it out of fights, which those of its first turn in a
/// fight equal (D263), at its life; before the server gave them, a group is judged by levels, none of its monsters above
/// the character's.
/// </remarks>
public static class GroupJudge
{
    // The character imagined beside the group: the odds weigh no position, only who reaches whom in a turn.
    private const long Self = 1;

    private const int ActionPoints = 1;
    private const int MovementPoints = 23;

    // Measured on the owner's 54 groups the character was known for (D249): of the 13 the group would beat it in under a
    // quarter of the turns it needs, 11 were won; of the 20 under the turns it needs, 17. Only the first are turned away.
    private const double Margin = 0.25;

    /// <summary>Tells whether the settings allow attacking a group.</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <param name="group">The group.</param>
    /// <param name="level">The character's level.</param>
    /// <param name="monsters">The pack's monsters, which give a monster's family (D247).</param>
    /// <returns>Whether they allow it.</returns>
    public static bool Allows(FightSettings settings, MapMonsterGroup group, int level, MonsterCatalog monsters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(monsters);

        var size = group.Monsters.Length;

        if (size < settings.LeastMonsters || size > settings.MostMonsters || group.Cell is null)
            return false;

        if (group.Monsters.Any(monster => settings.Excluded.Contains(monster.MonsterId)))
            return false;

        return settings.Mode is FightMode.Chosen
            ? group.Monsters.Any(monster => ChosenMonsters.Holds(settings, monsters, monster.MonsterId))
            : group.Monsters.All(monster => monster.Level <= level);
    }

    /// <summary>Tells whether the AI expects the character to beat a group: the group would not take its life in under a quarter of the turns it takes to beat the group, over the fight's course (D249).</summary>
    /// <param name="group">The group.</param>
    /// <param name="state">The character's state, out of a fight.</param>
    /// <param name="map">The map the group stands on.</param>
    /// <param name="tables">The pack's game tables.</param>
    /// <param name="tactics">The fight's AI.</param>
    /// <param name="lostAt">The share of the turns to win in which the group the character last lost to was to take its life (<see cref="Share"/>): a group no less risky is turned away too (D267); 0 before any loss.</param>
    /// <returns>Whether it does; before the server gave the character's characteristics, whether no monster of the group is above the character's level.</returns>
    public static bool Beats(MapMonsterGroup group, CharacterState state, FightMap map, GameTables tables, FightTactics tactics, double lostAt = 0)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(state);

        return OddsOf(group, state, map, tables, tactics) is { } odds
            ? Winnable(odds, lostAt)
            : group.Monsters.All(monster => monster.Level <= (state.Level ?? 0));
    }

    /// <summary>Tells whether odds let the character attack a group: the group would not take its life in under a quarter of the turns it needs to beat it (D249), nor in a share no greater than the group it last lost to (D267).</summary>
    /// <param name="odds">The odds of the fight against the group.</param>
    /// <param name="lostAt">The share of the group the character last lost to (<see cref="Share"/>), 0 before any loss.</param>
    /// <returns>Whether they do.</returns>
    public static bool Winnable(FightOdds odds, double lostAt = 0)
    {
        return odds.TurnsToLose >= odds.TurnsToWin * Margin && (lostAt <= 0 || odds.TurnsToLose > odds.TurnsToWin * lostAt);
    }

    /// <summary>Gets in what share of the turns the character needs to beat a group the group would take its life: under 1, the group first.</summary>
    /// <param name="odds">The odds of the fight against the group.</param>
    /// <returns>The share; infinite when the character would lose no life.</returns>
    public static double Share(FightOdds odds)
    {
        return odds.TurnsToWin > 0 ? odds.TurnsToLose / odds.TurnsToWin : double.PositiveInfinity;
    }

    /// <summary>Gets the odds the AI gives the character against a group, over the fight's course (D249).</summary>
    /// <param name="group">The group.</param>
    /// <param name="state">The character's state, out of a fight.</param>
    /// <param name="map">The map the group stands on.</param>
    /// <param name="tables">The pack's game tables.</param>
    /// <param name="tactics">The fight's AI.</param>
    /// <returns>The odds, or <see langword="null"/> when the group is judged by levels: before the server gave the character's characteristics, or on a map too small to imagine it.</returns>
    public static FightOdds? OddsOf(MapMonsterGroup group, CharacterState state, FightMap map, GameTables tables, FightTactics tactics)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(tactics);

        if (Character(state, tables.Breeds) is not { } self || state.Level is not { } level)
            return null;

        var cells = Cells(map, group.Monsters.Length + 1);

        if (cells.Count <= group.Monsters.Length)
            return null;

        var character = FightCombatants.Of(self with { Cell = cells[0] }, tables.Monsters);
        var fighters = ImmutableArray.CreateBuilder<Combatant>();
        var spells = ImmutableDictionary.CreateBuilder<long, ImmutableArray<SpellLevel>>();
        var points = ImmutableDictionary.CreateBuilder<long, (int ActionPoints, int MovementPoints)>();

        fighters.Add(character);
        spells[Self] = CharacterSpells.Of(state.Spells, self.BreedId, level, tables.Spells, tables.Breeds);
        points[Self] = (character.ActionPoints, character.MovementPoints);

        for (var index = 0; index < group.Monsters.Length; index++)
        {
            var monster = group.Monsters[index];
            var id = -1L - index;

            if (!tables.Monsters.TryGetMonster(monster.MonsterId, out var found) || !tables.Monsters.TryGetGrade(monster.MonsterId, monster.Grade, out var grade))
                return new FightOdds(double.PositiveInfinity, 0);

            var combatant = Imagined(id, cells[index + 1], found, grade);

            fighters.Add(combatant);
            spells[id] = SpellsOf(tables, found, grade);
            points[id] = (combatant.ActionPoints, combatant.MovementPoints);
        }

        var fight = new ImaginedFight { Map = map, Fighters = fighters.ToImmutable(), ActorId = Self, Round = 1 };
        var knowledge = new FightKnowledge { Spells = spells.ToImmutable(), TurnPoints = points.ToImmutable() };

        // Over the fight's course, the group's members dying one after another, rather than all striking every turn (D249).
        return tactics.OddsOverTheFight(fight, knowledge);
    }

    /// <summary>Gets the character as a fighter, from the characteristics the server gives it out of fights (D263).</summary>
    /// <param name="state">The character's state, out of a fight.</param>
    /// <param name="breeds">The pack's breeds: the character's is the one whose spells the server listed.</param>
    /// <returns>The fighter, or <see langword="null"/> before the server gave the characteristics and the life.</returns>
    public static Fighter? Character(CharacterState state, BreedCatalog breeds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(breeds);

        if (state.Characteristics.IsEmpty || state.Life is not { } life || state.MaxLife is not { } most || most <= 0)
            return null;

        return new Fighter
        {
            Id = Self,
            Team = FightTeam.Challenger,
            Life = life,
            MaxLife = most,
            ActionPoints = state.Characteristics.GetValueOrDefault(ActionPoints),
            MovementPoints = state.Characteristics.GetValueOrDefault(MovementPoints),
            Level = state.Level,
            BreedId = BreedOf(state, breeds),
            Characteristics = state.Characteristics,
        };
    }

    private static int? BreedOf(CharacterState state, BreedCatalog breeds)
    {
        // No message the readable protocol names gives the breed out of fights; the spells the server lists do.
        int? breed = null;
        var most = 0;

        foreach (var candidate in breeds.Breeds.Span)
        {
            var held = 0;

            foreach (var spellId in breeds.SpellsOf(candidate))
            {
                if (state.Spells.ContainsKey(spellId))
                    held++;
            }

            if (held > most)
                (breed, most) = (candidate.Id, held);
        }

        return breed;
    }

    private static List<int> Cells(FightMap map, int count)
    {
        var cells = new List<int>(count);

        for (var cell = 0; cell < MapData.CellCount && cells.Count < count; cell++)
        {
            if (map.IsWalkable(cell))
                cells.Add(cell);
        }

        return cells;
    }

    private static Combatant Imagined(long id, int cell, Monster monster, MonsterGrade grade)
    {
        var stats = grade.Characteristics;
        var life = stats.LifePoints + stats.Vitality;

        // The characteristics the effects' formulas read, from the grade (D195): elements' damage, bonuses and resistances,
        // which follow each other from earth's, earth, fire, water, air, then neutral.
        var characteristics = ImmutableSortedDictionary.CreateRange(new Dictionary<int, int>
        {
            [FightCharacteristic.Strength] = stats.Strength,
            [FightCharacteristic.Intelligence] = stats.Intelligence,
            [FightCharacteristic.Chance] = stats.Chance,
            [FightCharacteristic.Agility] = stats.Agility,
            [FightCharacteristic.AllDamageBonus] = stats.DamageBonus,
            [FightCharacteristic.EarthResistPercent] = stats.ReductionEarth,
            [FightCharacteristic.EarthResistPercent + 1] = stats.ReductionFire,
            [FightCharacteristic.EarthResistPercent + 2] = stats.ReductionWater,
            [FightCharacteristic.EarthResistPercent + 3] = stats.ReductionAir,
            [FightCharacteristic.EarthResistPercent + 4] = stats.ReductionNeutral,
            [FightCharacteristic.EarthReduction] = stats.ReductionEarthFlat,
            [FightCharacteristic.EarthReduction + 1] = stats.ReductionFireFlat,
            [FightCharacteristic.EarthReduction + 2] = stats.ReductionWaterFlat,
            [FightCharacteristic.EarthReduction + 3] = stats.ReductionAirFlat,
            [FightCharacteristic.EarthReduction + 4] = stats.ReductionNeutralFlat,
        });

        return new Combatant
        {
            Id = id,
            Team = (int)FightTeam.Defender,
            Cell = cell,
            Life = life,
            MaxLife = life,
            ActionPoints = stats.ActionPoints,
            MovementPoints = stats.MovementPoints,
            MonsterId = monster.Id,
            MonsterProperties = monster.Properties,
            Level = grade.Level,
            Characteristics = characteristics,
        };
    }

    private static ImmutableArray<SpellLevel> SpellsOf(GameTables tables, Monster monster, MonsterGrade grade)
    {
        var levels = ImmutableArray.CreateBuilder<SpellLevel>();

        foreach (var spell in tables.Monsters.SpellsOf(monster))
        {
            if (tables.Monsters.SpellGradeOf(spell, grade.Grade) is var spellGrade and > 0 && tables.Spells.TryGetLevel(spell.SpellId, spellGrade, out var level))
                levels.Add(level);
        }

        return levels.DrainToImmutable();
    }
}
