namespace D3B.Engine.Criteria;

/// <summary>What a bot knows that a criterion may ask: the character's level, objects, professions and subscription, where it acts from, and a fighter's states.</summary>
/// <remarks>
/// A fact left <see langword="null"/> is not known, and a condition asking it answers <see cref="CriterionAnswer.Unknown"/>,
/// but for <see cref="Cell"/>, which a bot chooses: see there.
/// </remarks>
public sealed record CriterionFacts
{
    /// <summary>Gets the character's level.</summary>
    public int? Level { get; init; }

    /// <summary>Gets how many of each object the character holds, by item id; an item it lacks is absent.</summary>
    public IReadOnlyDictionary<int, int>? Items { get; init; }

    /// <summary>Gets the level of each profession the character has, by job id; a profession it lacks is absent.</summary>
    public IReadOnlyDictionary<int, int>? JobLevels { get; init; }

    /// <summary>Gets whether the character's account is subscribed.</summary>
    public bool? Subscribed { get; init; }

    /// <summary>Gets the map the character acts on.</summary>
    public long? MapId { get; init; }

    /// <summary>Gets that map's sub-area.</summary>
    public int? SubAreaId { get; init; }

    /// <summary>Gets that sub-area's area.</summary>
    public int? AreaId { get; init; }

    /// <summary>Gets the cell the character acts from, or <see langword="null"/> when it is not chosen yet.</summary>
    /// <remarks>
    /// A path through the world is planned before the cell to leave a map from is chosen: a condition on the cell then
    /// counts as met, and the cell is chosen on the map among those where the whole criterion is (client.md).
    /// </remarks>
    public int? Cell { get; init; }

    /// <summary>Gets the states a fighter is in, which a spell level's criterion asks of its caster (<c>HS</c>).</summary>
    public IReadOnlyCollection<int>? States { get; init; }
}
