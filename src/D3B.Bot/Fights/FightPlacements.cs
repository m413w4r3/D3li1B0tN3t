using System.Collections.Concurrent;
using System.Collections.Immutable;
using D3B.Data.World;

namespace D3B.Bot.Fights;

/// <summary>The placement cells of every fight the host's bots saw begin, with the map they came from, each set the server gave kept apart (D275).</summary>
/// <remarks>
/// One per host, which <see cref="FightPlacementLearner"/> teaches from every fight any bot begins, whoever starts it; D3B's
/// database keeps them from one run to the next. A map may give more than one set: each is its own, counted. Where the
/// map left lies, its sub-area, area and coordinates, comes from the pack's atlas.
/// </remarks>
public sealed class FightPlacements
{
    private readonly ConcurrentDictionary<(long FromMapId, long MapId, string Red, string Blue), FightPlacement> _placements = new();

    /// <summary>Raised when a fight began with a set of cells, with that set as it now stands.</summary>
    /// <remarks>Raised on the thread that saw it, a relay's read loop: a handler must not wait.</remarks>
    public event Action<FightPlacement>? Learned;

    /// <summary>Gets how many sets of cells are known, on every map.</summary>
    public int Count => _placements.Count;

    /// <summary>Takes back what was seen before, such as what the database kept, without raising <see cref="Learned"/>.</summary>
    /// <param name="placement">The set of cells.</param>
    public void Remember(FightPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        _placements[KeyOf(placement.FromMapId, placement.MapId, placement.RedCells, placement.BlueCells)] = placement;
    }

    /// <summary>Gets the sets of cells known of the fights begun from a map.</summary>
    /// <param name="fromMapId">The map left.</param>
    /// <returns>The sets, the most seen first.</returns>
    public IReadOnlyList<FightPlacement> From(long fromMapId)
    {
        return [.. _placements.Values.Where(placement => placement.FromMapId == fromMapId).OrderByDescending(placement => placement.Seen)];
    }

    /// <summary>Counts a fight that began with a set of cells.</summary>
    /// <param name="fromMapId">The map the character was on as the fight began.</param>
    /// <param name="mapId">The map fought on.</param>
    /// <param name="redCells">The challengers' cells, red.</param>
    /// <param name="blueCells">The defenders' cells, blue.</param>
    /// <param name="atlas">The pack's atlas, which places the map left; none, its place unknown.</param>
    /// <param name="at">When it began.</param>
    /// <returns>The set as it now stands.</returns>
    public FightPlacement Seen(long fromMapId, long mapId, IEnumerable<int> redCells, IEnumerable<int> blueCells, WorldAtlas? atlas, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(redCells);
        ArgumentNullException.ThrowIfNull(blueCells);

        ImmutableArray<int> red = [.. redCells.Distinct().Order()];
        ImmutableArray<int> blue = [.. blueCells.Distinct().Order()];
        MapPosition position = default;
        var placed = atlas is not null && atlas.TryGetPosition(fromMapId, out position);
        var subArea = placed ? position.SubAreaId : (int?)null;
        var area = placed && atlas!.TryGetSubArea(position.SubAreaId, out var known) ? known.AreaId : (int?)null;
        var placement = _placements.AddOrUpdate(
            KeyOf(fromMapId, mapId, red, blue),
            _ => new FightPlacement(fromMapId, subArea, area, placed ? position.X : null, placed ? position.Y : null, mapId, red, blue, 1, at, at),
            (_, before) => before with { Seen = before.Seen + 1, LastSeen = at > before.LastSeen ? at : before.LastSeen });

        Learned?.Invoke(placement);

        return placement;
    }

    private static (long, long, string, string) KeyOf(long fromMapId, long mapId, ImmutableArray<int> red, ImmutableArray<int> blue)
    {
        return (fromMapId, mapId, string.Join(',', red), string.Join(',', blue));
    }
}
