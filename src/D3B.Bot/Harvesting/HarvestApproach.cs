using System.Collections.Immutable;

namespace D3B.Bot.Harvesting;

/// <summary>A passive plan for reaching a cell from which a map element may be used.</summary>
/// <param name="ElementId">The element on the map.</param>
/// <param name="ElementCell">The element's cell, or -1 when it is unknown.</param>
/// <param name="UseCell">The chosen cell, or -1 when no path was confirmed.</param>
/// <param name="Path">The path from the character's current cell, including both ends; empty when blocked.</param>
/// <param name="Distance">The number of path steps, or zero when blocked.</param>
/// <param name="Status">Whether the plan is confirmed or blocked.</param>
/// <param name="Reason">The outcome reason.</param>
public sealed record HarvestApproach(
    int ElementId,
    int ElementCell,
    int UseCell,
    ImmutableArray<int> Path,
    int Distance,
    string Status,
    string Reason);
