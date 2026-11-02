using System.Collections.Immutable;
using D3B.Data.Spells;

namespace D3B.Engine.Tactics;

/// <summary>What the AI knows of the fighters beyond where they stand: the spells each may cast, the points each starts a turn with, and the waits of the spells of the one playing.</summary>
/// <remarks>
/// The bot gives it from the fight's state and the pack (D199, D203): the character's spells as the server listed them and its
/// level unlocks them, a monster's at its grade, and the cooldowns the server last gave the character (D193). A fighter
/// left out casts nothing the AI knows of, and starts its turn with the points it holds.
/// </remarks>
public sealed record FightKnowledge
{
    /// <summary>Gets nothing known.</summary>
    public static FightKnowledge Empty { get; } = new();

    /// <summary>Gets the spells' levels each fighter may cast, by fighter.</summary>
    public ImmutableDictionary<long, ImmutableArray<SpellLevel>> Spells { get; init; } = ImmutableDictionary<long, ImmutableArray<SpellLevel>>.Empty;

    /// <summary>Gets the AP and MP each fighter starts its turn with, by fighter.</summary>
    public ImmutableDictionary<long, (int ActionPoints, int MovementPoints)> TurnPoints { get; init; } = ImmutableDictionary<long, (int ActionPoints, int MovementPoints)>.Empty;

    /// <summary>Gets the rounds each spell of the fighter playing still waits, as the server said, by spell.</summary>
    public ImmutableDictionary<int, int> Cooldowns { get; init; } = ImmutableDictionary<int, int>.Empty;

    /// <summary>Gets what is known, every fighter's spells but some left out.</summary>
    /// <param name="spellIds">The spells left out.</param>
    /// <returns>The knowledge without them.</returns>
    public FightKnowledge Without(IReadOnlySet<int> spellIds)
    {
        ArgumentNullException.ThrowIfNull(spellIds);

        return this with { Spells = Spells.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.RemoveAll(level => spellIds.Contains(level.SpellId))) };
    }

    /// <summary>Gets the spells a fighter may cast.</summary>
    /// <param name="fighterId">The fighter.</param>
    /// <returns>Its spells' levels, none when not known.</returns>
    public ImmutableArray<SpellLevel> SpellsOf(long fighterId)
    {
        return Spells.TryGetValue(fighterId, out var spells) ? spells : [];
    }
}
