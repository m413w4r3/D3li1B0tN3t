using System.Collections.Immutable;
using D3B.Data.Maps;

namespace D3B.App.Controls;

/// <summary>What the map draws at one moment: a map of the pack, its exits, and what stands on it (D121, D122).</summary>
/// <remarks>
/// Made anew each time the bot's state or the other bots' cells change; the map and its exits stay the same objects
/// while the character stays on the map, so the cells' geometry is built once per map.
/// </remarks>
/// <param name="Map">The map, from the pack.</param>
/// <param name="Exits">Its ways off, from the world graph.</param>
internal sealed record MapScene(MapData Map, IReadOnlyList<MapExit> Exits)
{
    /// <summary>Gets the map's interactive elements.</summary>
    public ImmutableArray<MapElementMark> Elements { get; init; } = [];

    /// <summary>Gets the cells the map's non-player characters stand on.</summary>
    public ImmutableArray<int> Npcs { get; init; } = [];

    /// <summary>Gets the cells the other bots of D3B on the map stand on.</summary>
    public ImmutableArray<int> Bots { get; init; } = [];

    /// <summary>Gets the cell the character stands on, or the one its walk started from, once the server placed it.</summary>
    public int? Character { get; init; }

    /// <summary>Gets the path the character walks, every cell from the first; empty when it stands still.</summary>
    public ImmutableArray<int> Path { get; init; } = [];

    /// <summary>Gets the challengers' placement cells, during a fight's placement: red, as the client shows them (D125).</summary>
    public ImmutableArray<int> ChallengerCells { get; init; } = [];

    /// <summary>Gets the defenders' placement cells, during a fight's placement: blue.</summary>
    public ImmutableArray<int> DefenderCells { get; init; } = [];

    /// <summary>Gets the cells the character's living allies stand on in its fight, its summons among them.</summary>
    public ImmutableArray<int> Allies { get; init; } = [];

    /// <summary>Gets the cells the character's living enemies stand on in its fight.</summary>
    public ImmutableArray<int> Enemies { get; init; } = [];

    /// <summary>Gets what the bot's activity looks at on the map, if it says (D131).</summary>
    public MapActivity? Activity { get; init; }
}
