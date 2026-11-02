using System.Collections.Immutable;
using D3B.Engine.Fights;
using D3B.Protocol.Game;

namespace D3B.Bot.Fights;

/// <summary>A fighter of the character's fight, as the server showed it and the actions it announced changed it.</summary>
public sealed record Fighter
{
    /// <summary>Gets the fighter's id: a character's own, or for a monster or a summon a small negative number.</summary>
    public required long Id { get; init; }

    /// <summary>Gets the fighter's team.</summary>
    public FightTeam Team { get; init; }

    /// <summary>Gets the cell the fighter stands on, when the server gave one.</summary>
    public int? Cell { get; init; }

    /// <summary>Gets whether the fighter lives: a dead one stays in the fight, on no cell.</summary>
    public bool IsAlive { get; init; } = true;

    /// <summary>Gets the fighter's life.</summary>
    public int Life { get; init; }

    /// <summary>Gets the fighter's most life, which a loss for the rest of the fight lowers.</summary>
    public int MaxLife { get; init; }

    /// <summary>Gets the AP the fighter has left, as the server last gave its characteristics.</summary>
    public int ActionPoints { get; init; }

    /// <summary>Gets the MP the fighter has left, as the server last gave its characteristics.</summary>
    public int MovementPoints { get; init; }

    /// <summary>Gets the fighter's level, a character's or a monster's.</summary>
    public int? Level { get; init; }

    /// <summary>Gets a character's breed, by id in the client's table.</summary>
    public int? BreedId { get; init; }

    /// <summary>Gets a monster's or a summon's id in the client's table.</summary>
    public int? MonsterId { get; init; }

    /// <summary>Gets a monster's or a summon's grade.</summary>
    public int? Grade { get; init; }

    /// <summary>Gets who summoned the fighter, when it is a summon.</summary>
    public long? SummonerId { get; init; }

    /// <summary>Gets the fighter carrying this one, while it is carried: it stands on no cell.</summary>
    public long? CarrierId { get; init; }

    /// <summary>Gets the fighter's characteristics, by id, as the server last gave them: each its whole value.</summary>
    public ImmutableSortedDictionary<int, int> Characteristics { get; init; } = ImmutableSortedDictionary<int, int>.Empty;

    /// <summary>Gets the fighter's buffs, in the order the server gave them.</summary>
    public ImmutableArray<FightBuff> Buffs { get; init; } = [];

    /// <summary>Gets the rounds each of the fighter's spells waits before it may be cast again, by spell, as the server last said.</summary>
    /// <remarks>The server says it of the character and the summons it controls alone (D193).</remarks>
    public ImmutableSortedDictionary<int, int> Cooldowns { get; init; } = ImmutableSortedDictionary<int, int>.Empty;

    /// <summary>Gets the spells the fighter chose to cast this fight, which a spell's interval and casts a turn count.</summary>
    public CastHistory Casts { get; init; } = CastHistory.Empty;

    /// <summary>Gets the states the fighter is in, from its buffs.</summary>
    public ImmutableArray<int> States => [.. Buffs.Where(buff => buff.Kind is FightBuffKind.State).Select(buff => buff.Parameter).Distinct()];

    /// <summary>Gets the changes the fighter's buffs make to its spells.</summary>
    public ImmutableArray<SpellModifier> Modifiers => [.. Buffs.Where(buff => buff.Kind is FightBuffKind.SpellModifier && Enum.IsDefined((SpellModifierKind)buff.ActionId)).Select(buff => new SpellModifier((SpellModifierKind)buff.ActionId, buff.Value, buff.Parameter))];
}
