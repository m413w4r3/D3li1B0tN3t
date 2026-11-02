using Avalonia;
using D3B.App.Controls;
using D3B.Bot.Activities;
using D3B.Data.World;

namespace D3B.App.ViewModels;

/// <summary>Places a zone's maps on its mini-map from the atlas: the maps' coordinates, and those around them (D127, D131).</summary>
internal static class ZoneScenes
{
    /// <summary>How far around the zone a landmark or the character is still drawn, in maps.</summary>
    private const int Reach = 6;

    /// <summary>Places a zone's maps.</summary>
    /// <param name="zone">The maps, each in its part or where a tour stands in it.</param>
    /// <param name="atlas">The atlas.</param>
    /// <param name="tour">Whether the maps are coloured by where the tour stands in them, rather than by their part.</param>
    /// <param name="landmark">A map to mark, such as the nearest bank's.</param>
    /// <param name="character">The map the character stands on.</param>
    /// <returns>The scene, or <see langword="null"/> when no map of the zone has coordinates.</returns>
    public static ZoneScene? Of(IReadOnlyList<ZoneMap> zone, WorldAtlas atlas, bool tour, long? landmark = null, long? character = null)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(atlas);

        var squares = new Dictionary<(int X, int Y), ZoneSquareKind>();
        var probable = new HashSet<(int X, int Y)>();
        sbyte? worldMap = null;

        foreach (var map in zone)
        {
            if (!atlas.TryGetPosition(map.MapId, out var position))
                continue;

            // Insides share their building's coordinates: the map of the world map wins them.
            if (position.WorldMap >= 0)
                worldMap ??= position.WorldMap;

            // Coloured by their parts, the maps left out show as such, and the probable ones are marked over their part's colour.
            var kind = tour ? KindOf(map.State)
                : map.State is ZoneMapState.Empty ? ZoneSquareKind.Empty
                : map.State is ZoneMapState.Isolated ? ZoneSquareKind.Isolated
                : map.Group % 2 is 0 ? ZoneSquareKind.GroupA : ZoneSquareKind.GroupB;

            if (!tour && map.State is ZoneMapState.Probable)
                probable.Add((position.X, position.Y));

            if (!squares.TryGetValue((position.X, position.Y), out var known) || Rank(kind) > Rank(known))
                squares[(position.X, position.Y)] = kind;
        }

        if (squares.Count is 0)
            return null;

        var (left, right) = (squares.Keys.Min(key => key.X), squares.Keys.Max(key => key.X));
        var (top, bottom) = (squares.Keys.Min(key => key.Y), squares.Keys.Max(key => key.Y));

        PixelPoint? Near(long? mapId)
        {
            if (mapId is not { } id || !atlas.TryGetPosition(id, out var position) || position.WorldMap != (worldMap ?? position.WorldMap))
                return null;

            return position.X >= left - Reach && position.X <= right + Reach && position.Y >= top - Reach && position.Y <= bottom + Reach
                ? new PixelPoint(position.X, position.Y)
                : null;
        }

        var marked = Near(landmark);
        var standing = Near(character);

        foreach (var point in new[] { marked, standing })
        {
            if (point is { } at)
                (left, right, top, bottom) = (Math.Min(left, at.X), Math.Max(right, at.X), Math.Min(top, at.Y), Math.Max(bottom, at.Y));
        }

        foreach (var position in atlas.Positions.Span)
        {
            if (position.WorldMap == worldMap && position.X >= left && position.X <= right && position.Y >= top && position.Y <= bottom)
                squares.TryAdd((position.X, position.Y), ZoneSquareKind.Context);
        }

        return new ZoneScene([.. squares.OrderBy(square => square.Key.Y).ThenBy(square => square.Key.X).Select(square => new ZoneSquare(square.Key.X, square.Key.Y, square.Value, probable.Contains(square.Key)))])
        {
            Landmark = marked,
            Character = standing,
        };
    }

    private static ZoneSquareKind KindOf(ZoneMapState state)
    {
        return state switch
        {
            ZoneMapState.Visited => ZoneSquareKind.Visited,
            ZoneMapState.Empty => ZoneSquareKind.Empty,
            ZoneMapState.Unreachable => ZoneSquareKind.Unreachable,
            ZoneMapState.Probable => ZoneSquareKind.Probable,
            ZoneMapState.Isolated => ZoneSquareKind.Isolated,
            ZoneMapState.Growing => ZoneSquareKind.Growing,
            _ => ZoneSquareKind.ToVisit,
        };
    }

    private static int Rank(ZoneSquareKind kind)
    {
        // Of maps sharing coordinates, the one a tour still goes through shows over those gone through or empty, a probable one first.
        return kind switch
        {
            ZoneSquareKind.Probable => 5,
            ZoneSquareKind.ToVisit => 4,
            ZoneSquareKind.Unreachable => 3,
            ZoneSquareKind.Visited or ZoneSquareKind.Growing or ZoneSquareKind.GroupA or ZoneSquareKind.GroupB => 2,
            ZoneSquareKind.Empty or ZoneSquareKind.Isolated => 1,
            _ => 0,
        };
    }
}
