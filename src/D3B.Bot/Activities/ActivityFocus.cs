using System.Collections.Immutable;

namespace D3B.Bot.Activities;

/// <summary>What an activity looks at, which the app's map marks in its activity layer (D131).</summary>
public sealed record ActivityFocus
{
    /// <summary>Gets the map the focus is of: the elements it names are this map's.</summary>
    public long? MapId { get; init; }

    /// <summary>Gets the element the activity goes for now, such as the resource it harvests.</summary>
    public int? TargetElementId { get; init; }

    /// <summary>Gets the elements of the map the activity wants, such as the resources it harvests.</summary>
    public ImmutableArray<int> WantedElementIds { get; init; } = [];

    /// <summary>Gets the map the character heads to next, once it travels: the exit leading there is marked.</summary>
    public long? NextMapId { get; init; }

    /// <summary>Gets the maps the activity goes through, each with where it stands in it, such as a tour's.</summary>
    public ImmutableArray<ZoneMap> Zone { get; init; } = [];
}
