using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Interactives;

namespace D3B.Bot.Harvesting;

/// <summary>A tour of the zone: the maps gone through, and those the way to failed, until every map is gone through once (D128).</summary>
/// <remarks>
/// <para>
/// A tour leaves out the maps the host's bots saw holding none of its resources (<see cref="HarvestSightings"/>), and those
/// never seen whose elements' graphics are all known to offer none (<see cref="HarvestZone.Foresee"/>, D138). It goes
/// through every other once, then the next begins.
/// </para>
/// <para>
/// It goes first to the probable maps, those seen holding a resource wanted or drawing a graphic that offers one, then to
/// the others of the world map, then to the insides. The activity goes to the nearest map still to go through, so a tour
/// sweeps the probable maps first, and the maps gone through longest ago come first in the next.
/// </para>
/// <para>
/// A map whose wanted elements all still grow back, by what the host learned of their resources, waits until the first
/// should have (<see cref="Regrowths"/>, D145); a resource whose growing back is not known yet never holds a map back.
/// </para>
/// </remarks>
internal sealed class HarvestTour
{
    private readonly HashSet<long> _visited = [];
    private readonly HashSet<long> _unreachable = [];
    private readonly HarvestSightings _sightings;
    private readonly GraphicSkills _graphics;
    private readonly Regrowths? _regrowths;
    private readonly TimeProvider _time;

    /// <summary>Gets the tour's number, from 1.</summary>
    public int Round { get; private set; } = 1;

    /// <summary>Gets whether anything was harvested in this tour.</summary>
    public bool HasHarvested { get; private set; }

    /// <summary>Gets how many maps this tour went through.</summary>
    public int Visited => _visited.Count;

    /// <summary>Initializes a new instance of the <see cref="HarvestTour"/> class.</summary>
    /// <param name="sightings">What the host's bots saw of the maps.</param>
    /// <param name="graphics">What the host learned of the maps' graphics.</param>
    /// <param name="regrowths">What the host learned of the resources' growing back, if it learns.</param>
    /// <param name="time">The bots' clock.</param>
    public HarvestTour(HarvestSightings sightings, GraphicSkills graphics, Regrowths? regrowths = null, TimeProvider? time = null)
    {
        _sightings = sightings;
        _graphics = graphics;
        _regrowths = regrowths;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Gets how many maps a tour goes through: those not left out.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>How many.</returns>
    public int Total(HarvestZone zone)
    {
        return zone.Maps.Keys.Count(map => !IsLeftOut(map, zone));
    }

    /// <summary>Gets how many maps of the zone are probable.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>How many.</returns>
    public int Probable(HarvestZone zone)
    {
        return zone.Maps.Keys.Count(map => IsProbable(map, zone));
    }

    /// <summary>Gets the maps still to go through in this tour: the probable ones first, then those of the world map, then the insides.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>The zone's maps neither gone through, nor failed to reach, nor left out, of the first kind that has some.</returns>
    /// <remarks>
    /// Measured on the pack of 3.7.12.12: 114 of the 186 sub-areas listing resources hold insides too, 1,493 maps against
    /// 6,569, mostly houses; but the mines are insides of such sub-areas, so a tour keeps them, last unless probable. Doors
    /// cost a change each way, and a first tour through a forest's houses harvested nothing for minutes, measured.
    /// </remarks>
    public HashSet<long> Remaining(HarvestZone zone)
    {
        var now = _time.GetUtcNow();
        HashSet<long> remaining = [.. zone.Maps.Keys.Where(map => !_visited.Contains(map) && !_unreachable.Contains(map) && !IsLeftOut(map, zone) && !(ReadyAt(map, zone) > now))];

        // The isolated maps a tour goes through come last, whatever they hold: the way there is long (D149).
        HashSet<long> near = [.. remaining.Where(map => !zone.Isolated.Contains(map))];

        if (near.Count is 0)
            return remaining;

        HashSet<long> probable = [.. near.Where(map => IsProbable(map, zone))];

        if (probable.Count > 0)
            return probable;

        return near.Any(map => !zone.Insides.Contains(map)) ? [.. near.Where(map => !zone.Insides.Contains(map))] : near;
    }

    /// <summary>Gets how many maps of the zone still grow back.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>How many.</returns>
    public int Growing(HarvestZone zone)
    {
        var now = _time.GetUtcNow();

        return zone.Maps.Keys.Count(map => !IsLeftOut(map, zone) && ReadyAt(map, zone) > now);
    }

    /// <summary>Gets when the first map this tour still goes through should have grown back, while every one still grows back.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>When, or <see langword="null"/> when none grows back.</returns>
    public DateTimeOffset? NextReady(HarvestZone zone)
    {
        var now = _time.GetUtcNow();
        DateTimeOffset? first = null;

        foreach (var map in zone.Maps.Keys)
        {
            if (_visited.Contains(map) || _unreachable.Contains(map) || IsLeftOut(map, zone) || ReadyAt(map, zone) is not { } ready || ready <= now)
                continue;

            if (first is null || ready < first)
                first = ready;
        }

        return first;
    }
    /// <summary>Gets how many maps of the zone are isolated, and left out unless the settings take them.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>How many.</returns>
    public static int Isolated(HarvestZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return zone.Isolated.Count;
    }

    /// <summary>Marks a map gone through.</summary>
    /// <param name="mapId">The map.</param>
    public void Visit(long mapId)
    {
        _visited.Add(mapId);
    }

    /// <summary>Marks a map the way to failed: left out of the rest of the tour.</summary>
    /// <param name="mapId">The map.</param>
    public void Unreach(long mapId)
    {
        _unreachable.Add(mapId);
    }

    /// <summary>Marks a harvest in this tour.</summary>
    public void Harvested()
    {
        HasHarvested = true;
    }

    /// <summary>Begins the next tour, once this one went through every map.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>Whether a map of the zone may still hold a resource: a tour has something to go through.</returns>
    public bool Next(HarvestZone zone)
    {
        if (Total(zone) is 0)
            return false;

        _visited.Clear();
        _unreachable.Clear();
        HasHarvested = false;
        Round++;

        return true;
    }

    /// <summary>Gets each map of the zone, with where the tour stands in it.</summary>
    /// <param name="zone">The zone.</param>
    /// <returns>The maps, for the app's map.</returns>
    public ImmutableArray<ZoneMap> Maps(HarvestZone zone)
    {
        return [.. zone.Maps.Select(map => new ZoneMap(map.Key, StateOf(map.Key, zone), map.Value))];
    }

    private ZoneMapState StateOf(long mapId, HarvestZone zone)
    {
        if (zone.Isolated.Contains(mapId) && !zone.IncludesIsolated)
            return ZoneMapState.Isolated;

        if (!IsLeftOut(mapId, zone) && ReadyAt(mapId, zone) > _time.GetUtcNow())
            return ZoneMapState.Growing;

        if (_visited.Contains(mapId))
            return ZoneMapState.Visited;

        if (IsLeftOut(mapId, zone))
            return ZoneMapState.Empty;

        if (_unreachable.Contains(mapId))
            return ZoneMapState.Unreachable;

        return IsProbable(mapId, zone) ? ZoneMapState.Probable : ZoneMapState.ToVisit;
    }

    // A map whose wanted elements were all seen harvested is held back until the first should have grown back. Measured:
    // the server never says when; one harvested before it was seen grew back no later than the first sighting of it so
    // reckons, which later sightings must not put off (9g5).
    private DateTimeOffset? ReadyAt(long mapId, HarvestZone zone)
    {
        if (_regrowths is null || !_sightings.TryGet(mapId, out var sighting) || sighting.Elements.IsDefaultOrEmpty)
            return null;

        DateTimeOffset? first = null;

        foreach (var element in sighting.Elements)
        {
            if (!zone.Targets.ContainsKey(element.SkillId))
                continue;

            if (element.IsEnabled || _regrowths.Reckoned(element.SkillId) is not { } takes)
                return null;

            var harvested = _regrowths.TryGetHarvest(mapId, element.ElementId, out var at) ? at : element.DisabledSince ?? sighting.At;
            var ready = harvested + takes;

            if (first is null || ready < first)
                first = ready;
        }

        return first;
    }
    // What a bot saw of a map wins over what its graphics foretell: it is the server's word for that map.
    private bool IsLeftOut(long mapId, HarvestZone zone)
    {
        if (zone.Isolated.Contains(mapId) && !zone.IncludesIsolated)
            return true;

        return _sightings.TryGet(mapId, out var sighting)
            ? !sighting.Resources.Overlaps(zone.Wanted)
            : zone.Foresee(mapId, _graphics) is MapForecast.Without;
    }

    private bool IsProbable(long mapId, HarvestZone zone)
    {
        return _sightings.TryGet(mapId, out var sighting)
            ? sighting.Resources.Overlaps(zone.Wanted)
            : zone.Foresee(mapId, _graphics) is MapForecast.Probable;
    }
}
