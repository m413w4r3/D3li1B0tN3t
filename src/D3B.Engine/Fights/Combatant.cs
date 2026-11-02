using System.Collections.Immutable;
using D3B.Data.Monsters;

namespace D3B.Engine.Fights;

/// <summary>A fighter as a fight's rules see it: where it stands, its team, what it has left, what it is, and its states.</summary>
/// <remarks>
/// The rules read it and never change it. Its characteristics are the server's, by id (client.md): AP 1, MP 23, range 19,
/// summons 26, dodge 78 and lock 79; its states, its spells' modifiers and the buffs it holds come from the buffs the server
/// gives it. A value the bot does not know is left at its default, and a rule asking it says so.
/// </remarks>
public sealed record Combatant
{
    /// <summary>The <see cref="Cell"/> of a fighter on no cell, such as a dead one.</summary>
    public const int NoCell = -1;

    /// <summary>Gets the fighter's id.</summary>
    public required long Id { get; init; }

    /// <summary>Gets the fighter's team, as the server numbers it.</summary>
    public int Team { get; init; }

    /// <summary>Gets the cell the fighter stands on, or <see cref="NoCell"/>.</summary>
    public int Cell { get; init; } = NoCell;

    /// <summary>Gets whether the fighter lives.</summary>
    public bool IsAlive { get; init; } = true;

    /// <summary>Gets whether the fighter is seen: every fighter captured was.</summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>Gets the AP the fighter has left.</summary>
    public int ActionPoints { get; init; }

    /// <summary>Gets the MP the fighter has left.</summary>
    public int MovementPoints { get; init; }

    /// <summary>Gets the fighter's range bonus, characteristic 19.</summary>
    public int RangeBonus { get; init; }

    /// <summary>Gets the fighter's lock, characteristic 79: how it tackles.</summary>
    public int Lock { get; init; }

    /// <summary>Gets the fighter's dodge, characteristic 78: how it escapes a tackle.</summary>
    public int Dodge { get; init; }

    /// <summary>Gets the fighter's life.</summary>
    public int Life { get; init; }

    /// <summary>Gets the fighter's most life.</summary>
    public int MaxLife { get; init; }

    /// <summary>Gets the most summons the fighter may have at once, characteristic 26, or null when not known.</summary>
    public int? MaxSummons { get; init; }

    /// <summary>Gets a character's breed, or null for a monster or a summon.</summary>
    public int? BreedId { get; init; }

    /// <summary>Gets a monster's or a summon's monster, or null for a character.</summary>
    public int? MonsterId { get; init; }

    /// <summary>Gets a monster's or a summon's grade, or null when not known.</summary>
    public int? Grade { get; init; }

    /// <summary>Gets that monster's properties, from the pack: whether it moves and tackles, among others.</summary>
    public MonsterProperties MonsterProperties { get; init; }

    /// <summary>Gets who summoned the fighter, or null when it is no summon.</summary>
    public long? SummonerId { get; init; }

    /// <summary>Gets the states the fighter is in.</summary>
    public ImmutableArray<int> States { get; init; } = [];

    /// <summary>Gets the changes buffs make to the fighter's spells.</summary>
    public ImmutableArray<SpellModifier> Modifiers { get; init; } = [];

    /// <summary>Gets the boosts, deboosts, states and spell modifiers the fighter holds, by the spell that gave each, oldest first.</summary>
    /// <remarks>
    /// Its characteristics, states and modifiers already hold what they do: these say which spell to take them back from
    /// when an effect dispels it, or another buff of it replaces one past its level's stacks (D227).
    /// </remarks>
    public ImmutableArray<HeldBuff> Buffs { get; init; } = [];

    /// <summary>Gets the fighter's level: a character's, or a monster's grade's.</summary>
    public int Level { get; init; }

    /// <summary>Gets the shares of the damage it receives the fighter takes, in percent, one for each buff multiplying it: effect 1163.</summary>
    public ImmutableArray<int> ReceivedDamageShares { get; init; } = [];

    /// <summary>Gets the fighter's characteristics as the server gave them, by id, which the effects' formulas read.</summary>
    /// <remarks>Those the rules of a cast read are the properties above; a characteristic left out counts as 0.</remarks>
    public ImmutableSortedDictionary<int, int> Characteristics { get; init; } = ImmutableSortedDictionary<int, int>.Empty;

    /// <summary>Gets whether the fighter is a summon.</summary>
    public bool IsSummon => SummonerId is not null;

    /// <summary>Gets whether the fighter is a player's character, no summon.</summary>
    public bool IsCharacter => BreedId is not null && SummonerId is null;

    /// <summary>Gets whether the fighter is a summon that cannot move, such as a turret.</summary>
    public bool IsStatic => IsSummon && MonsterId is not null && (MonsterProperties & MonsterProperties.CanMove) is 0;

    /// <summary>Gets one of the fighter's characteristics.</summary>
    /// <param name="id">The characteristic's id.</param>
    /// <returns>Its value, or 0 when the server gave none.</returns>
    public int Characteristic(int id)
    {
        return Characteristics.GetValueOrDefault(id);
    }

    /// <summary>Tells whether the fighter is in a state.</summary>
    /// <param name="state">The state's id.</param>
    /// <returns>Whether it is.</returns>
    public bool HasState(int state)
    {
        return States.Contains(state);
    }
}
