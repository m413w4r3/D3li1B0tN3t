using System.Collections.Immutable;

namespace D3B.App.Controls;

/// <summary>A way off the map, as the world graph holds it: the cells it leaves from and the maps it leads to (D121).</summary>
/// <param name="Kind">How it leaves the map.</param>
/// <param name="Cells">The cells it leaves from: an edge's, a map action's, or a door's own.</param>
/// <param name="Destinations">The maps it leads to, each once.</param>
internal sealed record MapExit(MapExitKind Kind, ImmutableArray<int> Cells, ImmutableArray<long> Destinations)
{
    /// <summary>Gets the edge a scroll leaves by: 0 east, 2 south, 4 west or 6 north; -1 for a map action or a door.</summary>
    public int Direction { get; init; } = -1;

    /// <summary>Gets the door's element, or -1.</summary>
    public int ElementId { get; init; } = -1;

    /// <summary>Gets what the map writes beside a scroll's edge, where it leads, in the app's language; empty for none.</summary>
    public string Label { get; init; } = "";
}
