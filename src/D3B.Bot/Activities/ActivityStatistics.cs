using System.Collections.Immutable;

namespace D3B.Bot.Activities;

/// <summary>What an activity brought so far, in figures D3B keeps from one run to the next (D146).</summary>
/// <remarks>Any activity may give them: the harvest's are the items it gathered, its banks and the fights that cut it.</remarks>
public sealed record ActivityStatistics
{
    /// <summary>Gets the items it brought, by item, with how many.</summary>
    public ImmutableDictionary<int, long> Items { get; init; } = ImmutableDictionary<int, long>.Empty;

    /// <summary>Gets how many times it banked.</summary>
    public int Banks { get; init; }

    /// <summary>Gets the fights that cut its steps, by kind (D144).</summary>
    public ImmutableDictionary<FightKind, int> Fights { get; init; } = ImmutableDictionary<FightKind, int>.Empty;
}
