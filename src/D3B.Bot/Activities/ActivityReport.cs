using System.Collections.Immutable;
using D3B.Bot.Localization;

namespace D3B.Bot.Activities;

/// <summary>What an activity says of itself as it runs, for the app to show on the bot's card and its map (D131).</summary>
/// <remarks>
/// An activity replaces its report whole, from the thread that runs it, and the app reads it every 250 ms (D94): it is
/// immutable. Its texts name the game's values by their ids, which the app names in its language (D112).
/// </remarks>
public sealed record ActivityReport
{
    /// <summary>Gets what the activity does now, such as harvesting an ash.</summary>
    public required GameText Doing { get; init; }

    /// <summary>Gets how far the activity is in what it goes through, such as the maps of a tour, if it says.</summary>
    public ActivityProgress? Progress { get; init; }

    /// <summary>Gets what the activity counts, such as each resource harvested, in the order to show them.</summary>
    public ImmutableArray<ActivityCount> Counts { get; init; } = [];

    /// <summary>Gets what the activity warns of, such as a resource above the character's profession.</summary>
    public ImmutableArray<GameText> Warnings { get; init; } = [];

    /// <summary>Gets what the activity looks at, for the map, if it says.</summary>
    public ActivityFocus? Focus { get; init; }
}
