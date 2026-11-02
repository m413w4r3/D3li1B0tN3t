using System.Collections.Immutable;
using Avalonia;

namespace D3B.App.Controls;

/// <summary>What a zone's mini-map draws: its maps at their coordinates, those around it, a landmark and the character (D127, D131).</summary>
/// <param name="Squares">The maps, each once per coordinates, the zone's over those around it.</param>
internal sealed record ZoneScene(ImmutableArray<ZoneSquare> Squares)
{
    /// <summary>Gets where a landmark stands, such as the nearest bank, if it is drawn.</summary>
    public PixelPoint? Landmark { get; init; }

    /// <summary>Gets where the character stands, if it is on the world map near the zone.</summary>
    public PixelPoint? Character { get; init; }
}
