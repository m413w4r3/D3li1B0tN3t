using D3B.Bot.Activities;
using D3B.Bot.Banks;
using D3B.Bot.Localization;
using D3B.Bot.Movement;
using D3B.Bot.Settings;
using D3B.Data;
using D3B.Data.World;
using D3B.Engine.World;

namespace D3B.Bot.Fighting;

/// <summary>What fighting settings lead to, for the app's profile editor: the ground's maps, the monsters living there and their levels, its nearest bank, and the place chosen for each character (D245).</summary>
/// <remarks>
/// One per host, as harvesting's (D127). When the activity chooses the place, each character playing the settings is told
/// the sub-area it would hunt in now, by its level as its bot last saw it, the first one's drawn; the editor knows a
/// character's level and not its account's subscription, so the place is a subscribed character's. With no character's
/// level known, the monsters chosen give one, the highest of their grades', at which the place is drawn (D246). The last
/// preview is kept for the same settings and characters.
/// </remarks>
public sealed class FightPreviews
{
    private readonly GameWorld _world;
    private readonly GameTables _tables;
    private readonly Lazy<WorldPathfinder> _pathfinder;
    private readonly Lock _lock = new();
    private string? _key;
    private SettingsPreview? _last;

    /// <summary>Initializes a new instance of the <see cref="FightPreviews"/> class.</summary>
    /// <param name="world">The installed build's world.</param>
    /// <param name="tables">The installed build's tables: the monsters and the banks.</param>
    public FightPreviews(GameWorld world, GameTables tables)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tables);

        _world = world;
        _tables = tables;
        _pathfinder = new Lazy<WorldPathfinder>(() => new WorldPathfinder(world.Graph, world.Atlas, NpcWays.Measured), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static string KeyOf(FightSettings settings, IReadOnlyList<PreviewPlayer> players)
    {
        var who = string.Join(';', players.Select(player => $"{player.Name}:{player.Level}"));

        return $"{settings.Mode}|{string.Join(',', settings.Monsters)}|{string.Join(',', settings.Families)}|{string.Join(',', settings.Excluded)}|{settings.Place}|{settings.Area}|{string.Join(',', settings.SubAreas)}|{settings.IsolatedMaps}|{who}";
    }

    /// <summary>Reckons what settings lead to.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="players">The characters that play them, as their bots last saw them, for the place the activity would choose for each.</param>
    /// <returns>The preview, or nothing while the settings name no place.</returns>
    public SettingsPreview? Of(FightSettings settings, IReadOnlyList<PreviewPlayer>? players = null)
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

    private SettingsPreview? Reckon(FightSettings settings, IReadOnlyList<PreviewPlayer> players)
    {
        var atlas = _world.Atlas;
        var monsters = _tables.Monsters;
        var notes = new List<PreviewPlayerNote>();
        HuntingGround? ground = null;
        int? forLevel = null;

        if (settings.Place is FightPlace.Automatic)
        {
            foreach (var player in players)
            {
                if (player.Level <= 0)
                {
                    notes.Add(new PreviewPlayerNote(player.Name, GameText.Of(FightTexts.Text("PlaceUnseen"))));

                    continue;
                }

                var chosen = HuntingGround.Of(settings, atlas, _world.Graph, monsters, player.Level, subscribed: true);

                if (chosen.SubAreas is not [var subArea] || chosen.Maps.Count is 0)
                {
                    notes.Add(new PreviewPlayerNote(player.Name, GameText.Of(FightTexts.Text("PlaceNone"))));

                    continue;
                }

                notes.Add(new PreviewPlayerNote(player.Name, GameText.Counted(FightTexts.Text("PlaceOne"), FightTexts.Text("PlaceMany"), chosen.Maps.Count, GameTextValue.SubArea(subArea))));
                ground ??= chosen;
            }

            if (ground is null && !players.Any(player => player.Level > 0) && LevelOfChosen(settings) is { } level)
            {
                var shown = HuntingGround.Of(settings, atlas, _world.Graph, monsters, level, subscribed: true);

                if (shown.SubAreas.Count > 0 && shown.Maps.Count > 0)
                {
                    ground = shown;
                    forLevel = level;
                }
            }

            if (ground is null)
            {
                return new SettingsPreview
                {
                    Figures = [new PreviewFigure(GameText.Of(FightTexts.Text("FigureAutomatic")), GameText.Of(FightTexts.Text("FigureAutomaticLabel")))],
                    PlayerNotes = [.. notes],
                    Message = GameText.Of(FightTexts.Text("MessageAutomatic")),
                };
            }
        }
        else
        {
            // The level counts only for a place the activity chooses.
            ground = HuntingGround.Of(settings, atlas, _world.Graph, monsters, 1, subscribed: true);

            if (ground.SubAreas.Count is 0)
                return null;
        }

        var hunted = HuntedIn(settings, ground);
        var maps = ground.Maps.Count;
        var left = ground.Isolated.Count(map => !ground.Maps.ContainsKey(map));
        var (bank, changes) = NearestBank.Of(_world.Graph, _tables.Banks, _pathfinder.Value, [.. ground.Maps.Keys]);
        var figures = new List<PreviewFigure>();

        if (forLevel is { } at)
            figures.Add(new(GameText.Of(FightTexts.Text("FigureForLevel"), GameTextValue.Number(at)), GameText.Of(FightTexts.Text("FigureForLevelLabel"))));

        figures.AddRange(
        [
            new(GameText.Of(FightTexts.Text("FigureNumber"), GameTextValue.Number(maps)), GameText.Counted(FightTexts.Text("FigureMapsOne"), FightTexts.Text("FigureMapsMany"), maps)),
            settings.Mode is FightMode.Chosen
                ? new(
                    GameText.Of(FightTexts.Text("FigureOutOf"), GameTextValue.Number(hunted.Count), GameTextValue.Number(Chosen(settings).Count)),
                    GameText.Of(FightTexts.Text("FigureChosenLiving")))
                : new(
                    GameText.Of(FightTexts.Text("FigureNumber"), GameTextValue.Number(hunted.Count)),
                    GameText.Counted(FightTexts.Text("FigureMonstersOne"), FightTexts.Text("FigureMonstersMany"), hunted.Count)),
            LevelsOf(hunted),
            bank is null
                ? new(GameText.Of(FightTexts.Text("FigureNone")), GameText.Of(FightTexts.Text("FigureNoBank")))
                : new(GameText.Counted(FightTexts.Text("FigureBankOne"), FightTexts.Text("FigureBankMany"), changes), GameText.Of(FightTexts.Text("FigureBank"))),
        ]);

        if (ground.Isolated.Count > 0)
        {
            figures.Add(new(
                GameText.Of(FightTexts.Text("FigureNumber"), GameTextValue.Number(ground.Isolated.Count)),
                settings.IsolatedMaps
                    ? GameText.Counted(FightTexts.Text("FigureIsolatedLastOne"), FightTexts.Text("FigureIsolatedLastMany"), ground.Isolated.Count)
                    : GameText.Counted(FightTexts.Text("FigureIsolatedOne"), FightTexts.Text("FigureIsolatedMany"), ground.Isolated.Count)));
        }

        var groups = ground.SubAreas.Select((subArea, index) => (subArea, index)).ToDictionary(pair => pair.subArea, pair => pair.index);

        return new SettingsPreview
        {
            Figures = [.. figures],
            Zone =
            [
                .. ground.Maps.Select(map => new ZoneMap(map.Key, ZoneMapState.ToVisit, groups.GetValueOrDefault(map.Value))),
                .. ground.Isolated.Where(map => !ground.Maps.ContainsKey(map) && atlas.TryGetPosition(map, out _)).Select(map => new ZoneMap(map, ZoneMapState.Isolated, atlas.TryGetPosition(map, out var position) ? groups.GetValueOrDefault(position.SubAreaId) : 0)),
            ],
            LandmarkMapId = bank,
            PlayerNotes = [.. notes],
        };
    }

    private HashSet<int> Chosen(FightSettings settings)
    {
        // Those named, and those of the families named a hunter meets (D247).
        var chosen = settings.Monsters.ToHashSet();

        if (settings.Families.Count > 0)
        {
            foreach (var id in _world.Atlas.Monsters.Span)
            {
                if (ChosenMonsters.Holds(settings, _tables.Monsters, id) && HuntingGround.Hunted(_tables.Monsters, id))
                    chosen.Add(id);
            }
        }

        return chosen;
    }

    private int? LevelOfChosen(FightSettings settings)
    {
        // The highest of their grades: each monster chosen is worth whole there, and no sub-area above it is chosen (D245).
        if (settings.Mode is not FightMode.Chosen)
            return null;

        int? highest = null;

        foreach (var id in Chosen(settings))
        {
            if (!_tables.Monsters.TryGetMonster(id, out var monster))
                continue;

            foreach (var grade in _tables.Monsters.GradesOf(monster))
                highest = Math.Max(highest ?? grade.Level, grade.Level);
        }

        return highest is { } level ? Math.Min(level, 200) : null;
    }

    private HashSet<int> HuntedIn(FightSettings settings, HuntingGround ground)
    {
        // The monsters the activity attacks a group for in the ground: those chosen, else every one living there, none left out.
        var excluded = settings.Excluded.ToHashSet();
        var hunted = new HashSet<int>();

        foreach (var id in ground.SubAreas)
        {
            if (!_world.Atlas.TryGetSubArea(id, out var subArea))
                continue;

            foreach (var monster in _world.Atlas.MonstersOf(subArea))
            {
                if (!excluded.Contains(monster) && (settings.Mode is not FightMode.Chosen || ChosenMonsters.Holds(settings, _tables.Monsters, monster)) && HuntingGround.Hunted(_tables.Monsters, monster))
                    hunted.Add(monster);
            }
        }

        return hunted;
    }

    private PreviewFigure LevelsOf(HashSet<int> hunted)
    {
        int? lowest = null;
        int? highest = null;

        foreach (var id in hunted)
        {
            if (!_tables.Monsters.TryGetMonster(id, out var monster))
                continue;

            foreach (var grade in _tables.Monsters.GradesOf(monster))
            {
                lowest = Math.Min(lowest ?? grade.Level, grade.Level);
                highest = Math.Max(highest ?? grade.Level, grade.Level);
            }
        }

        return lowest is { } low && highest is { } high
            ? new(GameText.Of(FightTexts.Text("FigureLevels"), GameTextValue.Number(low), GameTextValue.Number(high)), GameText.Of(FightTexts.Text("FigureLevelsLabel")))
            : new(GameText.Of(FightTexts.Text("FigureNoLevel")), GameText.Of(FightTexts.Text("FigureLevelsLabel")));
    }
}
