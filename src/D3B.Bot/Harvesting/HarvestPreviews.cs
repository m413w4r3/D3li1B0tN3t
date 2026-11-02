using D3B.Bot.Banks;
using D3B.Bot.Interactives;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Settings;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.World;
using D3B.Engine.World;

namespace D3B.Bot.Harvesting;

/// <summary>What harvesting settings lead to, for the app's profile editor: the zone's maps, its resources, its nearest bank (D127).</summary>
/// <remarks>
/// <para>
/// One per host. The zone's maps are those a tour goes through, the probable ones apart, from what the host's bots saw and
/// learned of the graphics (D138). The path to the nearest bank is found from each bank's front to the zone, a door more
/// into the bank, for a character of whom nothing is known: criteria not met, no zaap. The app asks as the settings
/// change: the last preview is kept for the same settings and characters, while the bots have learned nothing more.
/// </para>
/// <para>
/// When the activity chooses the place, each character playing the settings is told the sub-area it would go to now, by
/// its levels as its bot last saw them, and the first one's is drawn (D166). The preview knows neither the zaaps a character
/// knows nor its criteria: of the sub-areas a character could reach from a zaap or a bank, it takes the richest.
/// </para>
/// </remarks>
public sealed class HarvestPreviews
{
    private readonly GameWorld _world;
    private readonly GameTables _tables;
    private readonly GameMaps _maps;
    private readonly HarvestSightings _sightings;
    private readonly GraphicSkills _graphics;
    private readonly GraphicPlaces _places;
    private readonly Lazy<WorldPathfinder> _pathfinder;
    private readonly Lazy<HashSet<int>> _reachable;
    private readonly Lock _lock = new();
    private string? _key;
    private SettingsPreview? _last;

    /// <summary>Initializes a new instance of the <see cref="HarvestPreviews"/> class.</summary>
    /// <param name="world">The installed build's world.</param>
    /// <param name="tables">The installed build's tables: the professions' skills and the banks.</param>
    /// <param name="maps">The installed build's maps: the graphics of the zone's elements.</param>
    /// <param name="sightings">What the host's bots saw of the maps.</param>
    /// <param name="graphics">What the host learned of the maps' graphics.</param>
    /// <param name="places">Where each graphic is drawn, which places the resources of <c>Base</c> (D150).</param>
    public HarvestPreviews(GameWorld world, GameTables tables, GameMaps maps, HarvestSightings sightings, GraphicSkills graphics, GraphicPlaces places)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(sightings);
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(places);

        _world = world;
        _tables = tables;
        _maps = maps;
        _sightings = sightings;
        _graphics = graphics;
        _places = places;
        _pathfinder = new Lazy<WorldPathfinder>(() => new WorldPathfinder(world.Graph, world.Atlas, NpcWays.Measured), LazyThreadSafetyMode.ExecutionAndPublication);
        _reachable = new Lazy<HashSet<int>>(() => ReachableSubAreas(world, tables), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    // The sub-areas some map of which a character reaches from a zaap or a bank, whatever the criteria: not a test's or a tutorial's.
    private static HashSet<int> ReachableSubAreas(GameWorld world, GameTables tables)
    {
        var graph = world.Graph;
        var vertices = graph.Vertices.Span;
        var zaaps = new HashSet<long>();

        foreach (var subArea in world.Atlas.SubAreas.Span)
        {
            if (subArea.AssociatedZaapMapId != SubArea.NoZaap)
                zaaps.Add(subArea.AssociatedZaapMapId);
        }

        foreach (var bank in tables.Banks.Banks.Span)
            zaaps.Add(bank.MapId);

        var seen = new bool[vertices.Length];
        var open = new Queue<int>();

        for (var vertex = 0; vertex < vertices.Length; vertex++)
        {
            if (zaaps.Contains(vertices[vertex].MapId))
            {
                seen[vertex] = true;
                open.Enqueue(vertex);
            }
        }

        while (open.TryDequeue(out var vertex))
        {
            foreach (var edge in graph.EdgesFrom(vertex))
            {
                if (!seen[edge.To])
                {
                    seen[edge.To] = true;
                    open.Enqueue(edge.To);
                }
            }
        }

        var reachable = new HashSet<int>();

        for (var vertex = 0; vertex < vertices.Length; vertex++)
        {
            if (seen[vertex] && world.Atlas.TryGetPosition(vertices[vertex].MapId, out var position))
                reachable.Add(position.SubAreaId);
        }

        return reachable;
    }

    private string KeyOf(HarvestSettings settings, IReadOnlyList<PreviewPlayer> players)
    {
        // What the bots saw and learned only grows: their counts tell a preview reckoned before.
        var who = string.Join(';', players.Select(player => $"{player.Name}:{player.Level}:{string.Join(',', player.JobLevels.OrderBy(job => job.Key).Select(job => $"{job.Key}={job.Value}"))}"));

        return $"{settings.Mode}|{string.Join(',', settings.Jobs)}|{string.Join(',', settings.Excluded)}|{settings.Order}|{string.Join(',', settings.Resources)}|"
            + $"{settings.Place}|{settings.SafeLevel}|{settings.Area}|{string.Join(',', settings.SubAreas)}|{settings.IsolatedMaps}|{who}|{_sightings.Count}|{_graphics.Count}";
    }

    /// <summary>Reckons what settings lead to.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="players">The characters that play them, as their bots last saw them, for the place the activity would choose for each (D166).</param>
    /// <returns>The preview, or nothing while the settings name no resource or no place.</returns>
    public SettingsPreview? Of(HarvestSettings settings, IReadOnlyList<PreviewPlayer>? players = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        players ??= [];

        var key = KeyOf(settings, players);

        lock (_lock)
        {
            if (key == _key)
                return _last;
        }

        var preview = Reckon(settings, players);

        lock (_lock)
        {
            (_key, _last) = (key, preview);
        }

        return preview;
    }

    private PlaceCandidate? PlaceFor(IEnumerable<HarvestTarget> targets, PreviewPlayer player, bool safeLevel, bool isolatedMaps)
    {
        var reachable = _reachable.Value;

        return AutomaticPlace.Rank(targets, player.Level, player.JobLevels, safeLevel, isolatedMaps, _world.Atlas, _world.Graph, _places, _graphics)
            .FirstOrDefault(candidate => reachable.Contains(candidate.SubAreaId));
    }

    private SettingsPreview? Reckon(HarvestSettings settings, IReadOnlyList<PreviewPlayer> players)
    {
        var atlas = _world.Atlas;
        var notes = new List<PreviewPlayerNote>();
        int[]? chosen = null;

        if (settings.Place is HarvestPlace.Automatic)
        {
            var targets = HarvestZone.TargetsOf(settings, _tables.Jobs).Values;

            if (targets.Length is 0)
                return null;

            foreach (var player in players)
            {
                if (player.Level <= 0 && player.JobLevels.Count is 0)
                {
                    notes.Add(new PreviewPlayerNote(player.Name, GameText.Of(HarvestTexts.Text("PlaceUnseen"))));

                    continue;
                }

                if (PlaceFor(targets, player, settings.SafeLevel, settings.IsolatedMaps) is not { } place)
                {
                    notes.Add(new PreviewPlayerNote(player.Name, GameText.Of(HarvestTexts.Text("PlaceNone"))));

                    continue;
                }

                notes.Add(new PreviewPlayerNote(player.Name, GameText.Counted(HarvestTexts.Text("PlaceOne"), HarvestTexts.Text("PlaceMany"), place.Maps, GameTextValue.SubArea(place.SubAreaId))));
                chosen ??= [place.SubAreaId];
            }

            if (chosen is null)
            {
                return new SettingsPreview
                {
                    Figures = [new PreviewFigure(GameText.Of(HarvestTexts.Text("FigureAutomatic")), GameText.Of(HarvestTexts.Text("FigureAutomaticLabel")))],
                    PlayerNotes = [.. notes],
                    Message = GameText.Of(HarvestTexts.Text("MessageAutomatic")),
                };
            }
        }

        var zone = chosen is null
            ? HarvestZone.Of(settings, atlas, _world.Graph, _tables.Jobs, _maps, HarvestZone.PlacedFor(settings, _tables.Jobs, _places, _graphics))
            : HarvestZone.Of(settings, atlas, _world.Graph, _tables.Jobs, _maps, chosen: chosen);

        if (zone.Targets.Count is 0 || zone.SubAreas.Count is 0)
            return null;

        var tour = new HarvestTour(_sightings, _graphics);
        var maps = tour.Total(zone);
        var probable = tour.Probable(zone);
        var listed = zone.PresentIn(atlas, _graphics).Count;
        var isolated = HarvestTour.Isolated(zone);
        var resources = settings.Mode is HarvestMode.ByProfession
            ? new PreviewFigure(
                GameText.Of(HarvestTexts.Text("FigureNumber"), GameTextValue.Number(listed)),
                GameText.Counted(HarvestTexts.Text("FigureResourcesOne"), HarvestTexts.Text("FigureResourcesMany"), listed))
            : new PreviewFigure(
                GameText.Of(HarvestTexts.Text("FigureOutOf"), GameTextValue.Number(listed), GameTextValue.Number(settings.Resources.Distinct().Count())),
                GameText.Of(HarvestTexts.Text("FigureResourcesPresent")));
        var (bank, changes) = NearestBank.Of(_world.Graph, _tables.Banks, _pathfinder.Value, [.. zone.Maps.Keys]);
        var figures = new List<PreviewFigure>
        {
            new(GameText.Of(HarvestTexts.Text("FigureNumber"), GameTextValue.Number(maps)), GameText.Counted(HarvestTexts.Text("FigureMapsOne"), HarvestTexts.Text("FigureMapsMany"), maps)),
            new(GameText.Of(HarvestTexts.Text("FigureNumber"), GameTextValue.Number(probable)), GameText.Counted(HarvestTexts.Text("FigureProbableOne"), HarvestTexts.Text("FigureProbableMany"), probable)),
            resources,
            bank is null
                ? new(GameText.Of(HarvestTexts.Text("FigureNone")), GameText.Of(HarvestTexts.Text("FigureNoBank")))
                : new(GameText.Counted(HarvestTexts.Text("FigureBankOne"), HarvestTexts.Text("FigureBankMany"), changes), GameText.Of(HarvestTexts.Text("FigureBank"))),
        };

        if (isolated > 0)
        {
            figures.Add(new(
                GameText.Of(HarvestTexts.Text("FigureNumber"), GameTextValue.Number(isolated)),
                settings.IsolatedMaps
                    ? GameText.Counted(HarvestTexts.Text("FigureIsolatedLastOne"), HarvestTexts.Text("FigureIsolatedLastMany"), isolated)
                    : GameText.Counted(HarvestTexts.Text("FigureIsolatedOne"), HarvestTexts.Text("FigureIsolatedMany"), isolated)));
        }

        return new SettingsPreview
        {
            Figures = [.. figures],
            Zone = tour.Maps(zone),
            LandmarkMapId = bank,
            PlayerNotes = [.. notes],
        };
    }
}
