using System.Diagnostics.CodeAnalysis;
using D3B.Data.Packs;

namespace D3B.Data.Maps;

/// <summary>The maps of a pack, each read when first asked for and kept while it stays among the most recently used.</summary>
/// <remarks>
/// A pack holds about 17,700 maps, each its own entry: reading them all would take memory for maps no bot is on. A
/// <see cref="PackCache"/> keeps the maps last asked for within a budget; safe for any number of threads.
/// </remarks>
public sealed class GameMaps
{
    /// <summary>The budget used when none is given: some 3,000 maps, a few MB.</summary>
    public const long DefaultCacheCapacity = 16L * 1024 * 1024;

    private readonly GamePack _pack;
    private readonly PackCache _cache;

    /// <summary>Initializes a new instance of the <see cref="GameMaps"/> class.</summary>
    /// <param name="pack">The pack holding the maps.</param>
    /// <param name="cacheCapacity">The most the kept maps may weigh together, in bytes of decompressed entries.</param>
    public GameMaps(GamePack pack, long cacheCapacity = DefaultCacheCapacity)
    {
        ArgumentNullException.ThrowIfNull(pack);

        _pack = pack;
        _cache = new PackCache(pack, cacheCapacity);
    }

    /// <summary>Tells whether the pack holds a map.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>Whether it is there.</returns>
    public bool Contains(long mapId)
    {
        return _pack.Contains(MapData.EntryName(mapId));
    }

    /// <summary>Finds a map.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="map">The map, when the pack holds it.</param>
    /// <returns>Whether the pack holds it.</returns>
    /// <exception cref="PackFormatException">The map's entry is damaged.</exception>
    public bool TryGet(long mapId, [NotNullWhen(true)] out MapData? map)
    {
        var name = MapData.EntryName(mapId);

        map = _pack.Contains(name) ? _cache.Get<MapData>(name) : null;

        return map is not null;
    }

    /// <summary>Gets a map.</summary>
    /// <param name="mapId">The map.</param>
    /// <returns>The map.</returns>
    /// <exception cref="KeyNotFoundException">The pack holds no such map.</exception>
    /// <exception cref="PackFormatException">The map's entry is damaged.</exception>
    public MapData Get(long mapId)
    {
        return TryGet(mapId, out var map) ? map : throw new KeyNotFoundException($"The pack holds no map {mapId}.");
    }
}
