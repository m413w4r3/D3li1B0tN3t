using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace D3B.Bot.Harvesting;

/// <summary>Which resources each map holds, as the host's bots saw them: one per host, which every bot shares (D128).</summary>
/// <remarks>
/// The pack does not say which maps hold which resources: the server says what each element of a map is as a character
/// enters it (client.md). A bot tells what it saw (<see cref="Seen"/>), harvested resources included, since they grow
/// back; a tour leaves out the maps seen holding none of its resources. Nothing is kept from one run of the host to the
/// next, as for the banks (D79): what a tour learns costs one tour.
/// </remarks>
public sealed class HarvestSightings
{
    private readonly ConcurrentDictionary<long, MapSighting> _maps = new();

    /// <summary>Gets how many maps the host's bots saw.</summary>
    public int Count => _maps.Count;

    /// <summary>Remembers what a map holds.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="resources">The resources its elements gather, by item.</param>
    /// <param name="at">When, by the bot's clock.</param>
    public void Seen(long mapId, IEnumerable<int> resources, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(resources);

        _maps[mapId] = new MapSighting(resources.ToFrozenSet(), at, []);
    }

    /// <summary>Remembers what a map holds, each of its elements as it was, which says what still grows back there (D145).</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="elements">Its elements that gather a resource.</param>
    /// <param name="at">When, by the bot's clock.</param>
    /// <remarks>
    /// An element harvested by another, unseen, was harvested before a bot first saw it so: each keeps when that was, so that
    /// the next sightings do not put off when it should grow back (9g5).
    /// </remarks>
    public void SeenElements(long mapId, IReadOnlyCollection<SightedElement> elements, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var before = _maps.TryGetValue(mapId, out var previous) && !previous.Elements.IsDefault ? previous : null;
        var kept = elements.Select(element => element with { DisabledSince = element.IsEnabled ? null : DisabledSince(before, element, at) });

        _maps[mapId] = new MapSighting(elements.Select(element => element.ItemId).ToFrozenSet(), at, [.. kept]);
    }

    /// <summary>Finds what a map was seen holding.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="sighting">What it held, when it was seen.</param>
    /// <returns>Whether a bot of the host saw it.</returns>
    public bool TryGet(long mapId, [NotNullWhen(true)] out MapSighting? sighting)
    {
        return _maps.TryGetValue(mapId, out sighting);
    }

    /// <summary>Tells whether a map was seen holding none of some resources.</summary>
    /// <param name="mapId">The map.</param>
    /// <param name="wanted">The resources, by item.</param>
    /// <returns>Whether a bot saw it, holding none of them.</returns>
    public bool IsEmptyOf(long mapId, IReadOnlySet<int> wanted)
    {
        ArgumentNullException.ThrowIfNull(wanted);

        return _maps.TryGetValue(mapId, out var sighting) && !sighting.Resources.Overlaps(wanted);
    }

    private static DateTimeOffset DisabledSince(MapSighting? before, SightedElement element, DateTimeOffset at)
    {
        if (before is null)
            return at;

        foreach (var seen in before.Elements)
        {
            if (seen.ElementId == element.ElementId && seen.SkillId == element.SkillId && !seen.IsEnabled)
                return seen.DisabledSince ?? before.At;
        }

        return at;
    }
}
