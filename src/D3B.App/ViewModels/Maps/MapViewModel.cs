using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels.Logs;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Runtime;
using D3B.Data;
using D3B.Data.Maps;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Engine.Maps;
using D3B.Protocol.Game;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels.Maps;

/// <summary>The map page: a bot's map drawn from the pack as its character moves, what stands on it in layers, and what a cell holds under the pointer (D120 to D122).</summary>
/// <remarks>
/// <para>
/// It reads the bot's snapshot every 250 ms while it shows, on the UI's thread (D9, D94), and makes a new scene only when
/// the snapshot, the other bots on the map or the language changed: the bot's loop never waits for the page.
/// </para>
/// <para>
/// The bot it shows is the one the logs page and the inspector show too (<see cref="BotChoice"/>): a bot's card opens its
/// map here.
/// </para>
/// </remarks>
internal sealed partial class MapViewModel : ObservableObject
{
    private const string NoValue = "—";
    private const string Separator = " · ";

    private readonly Localizer _localizer;
    private readonly BotChoice _choice;
    private GameMaps? _maps;
    private GameWorld? _world;
    private MapNames? _names;
    private GameIcons? _icons;
    private Bots? _bots;
    private BotActor? _drawnBot;
    private CharacterState? _drawnState;
    private string _drawnOthers = "";
    private bool _drawnConnected;
    private bool _drawnPaused;
    private ActivityReport? _drawnReport;
    private CultureInfo? _drawnCulture;
    private GameTextFormatter? _says;
    private MapData? _map;
    private CultureInfo? _exitsCulture;
    private bool _mapHasStairs;
    private IReadOnlyList<MapExit> _exits = [];
    private List<(string Name, int Cell)> _others = [];

    /// <summary>Gets the bots, a chip each, in the order the bots page shows them.</summary>
    public ObservableCollection<LogSourceViewModel> Bots { get; } = [];

    /// <summary>Gets the bot whose map the page shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBot))]
    public partial LogSourceViewModel? Bot { get; private set; }

    /// <summary>Gets or sets the source chosen in the page's selector: setting it chooses it, as a click on it does.</summary>
    public LogSourceViewModel? Chosen
    {
        get => Bot;
        set
        {
            if (value is not null)
                Choose(value);
        }
    }

    /// <summary>Gets whether no bot has been made, so that the page has nothing to show.</summary>
    [ObservableProperty]
    public partial bool HasNoBot { get; private set; } = true;

    /// <summary>Gets what the map draws, once the bot's character is on a map the pack holds.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScene))]
    public partial MapScene? Scene { get; private set; }

    /// <summary>Gets why no map is drawn for the bot: its character not on a map yet, or on one the pack lacks.</summary>
    [ObservableProperty]
    public partial string? Problem { get; private set; }

    /// <summary>Gets the coordinates of the character's map on the world map.</summary>
    [ObservableProperty]
    public partial string Coordinates { get; private set; } = NoValue;

    /// <summary>Gets where the character is: the sub-area and the cell, or a haven bag, a fight's map, a map off the atlas.</summary>
    [ObservableProperty]
    public partial string Place { get; private set; } = "";

    /// <summary>Gets what the character is in the middle of, and the map's id.</summary>
    [ObservableProperty]
    public partial string Detail { get; private set; } = "";

    /// <summary>Gets or sets whether the grid and the obstacles show.</summary>
    [ObservableProperty]
    public partial bool ShowsCells { get; set; } = true;

    /// <summary>Gets or sets whether the character and its path show.</summary>
    [ObservableProperty]
    public partial bool ShowsCharacter { get; set; } = true;

    /// <summary>Gets or sets whether the interactive elements show.</summary>
    [ObservableProperty]
    public partial bool ShowsElements { get; set; } = true;

    /// <summary>Gets or sets whether the non-player characters show.</summary>
    [ObservableProperty]
    public partial bool ShowsNpcs { get; set; } = true;

    /// <summary>Gets or sets whether the exits, map actions and doors show.</summary>
    [ObservableProperty]
    public partial bool ShowsExits { get; set; } = true;

    /// <summary>Gets or sets whether the other bots on the map show.</summary>
    [ObservableProperty]
    public partial bool ShowsBots { get; set; } = true;

    /// <summary>Gets or sets whether the zones and the stairs show, hidden at first.</summary>
    [ObservableProperty]
    public partial bool ShowsZones { get; set; }

    /// <summary>Gets or sets whether what the bot's activity looks at shows (D131).</summary>
    [ObservableProperty]
    public partial bool ShowsActivity { get; set; } = true;

    /// <summary>Gets or sets whether the map shows the character's fight: the placement cells, its allies and enemies.</summary>
    [ObservableProperty]
    public partial bool ShowsFight { get; set; } = true;

    /// <summary>Gets what the bot's activity says it does, and how far it is, if it runs one that says (D131).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActivity))]
    public partial string ActivityLine { get; private set; } = "";

    /// <summary>Gets the zone the bot's activity goes through, where it stands in each map, as a mini-map.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTour))]
    public partial ZoneScene? Tour { get; private set; }

    /// <summary>Gets whether the bot's activity says what it does.</summary>
    public bool HasActivity => ActivityLine.Length > 0;

    /// <summary>Gets whether the bot's activity goes through a zone.</summary>
    public bool HasTour => Tour is not null;

    /// <summary>Gets the layers the map shows.</summary>
    [ObservableProperty]
    public partial MapLayers Layers { get; private set; } = MapLayers.Default;

    /// <summary>Gets or sets the cell under the pointer, which the map sets.</summary>
    [ObservableProperty]
    public partial int? HoveredCell { get; set; }

    /// <summary>Gets whether the pointer is over a cell of the map drawn.</summary>
    [ObservableProperty]
    public partial bool HasHover { get; private set; }

    /// <summary>Gets the cell under the pointer, by its id and coordinates.</summary>
    [ObservableProperty]
    public partial string HoverTitle { get; private set; } = "";

    /// <summary>Gets what the cell under the pointer is: walked on, its floor and zone, an obstacle or a gap.</summary>
    [ObservableProperty]
    public partial string HoverDetail { get; private set; } = "";

    /// <summary>Gets what stands on the cell under the pointer, and where it leads, a line each.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> HoverItems { get; private set; } = [];

    /// <summary>Gets whether the page shows a bot.</summary>
    public bool HasBot => Bot is not null;

    /// <summary>Gets whether a map is drawn.</summary>
    public bool HasScene => Scene is not null;

    /// <summary>Initializes a new instance of the <see cref="MapViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="choice">The bot the logs page, the inspector and the map look at.</param>
    public MapViewModel(Localizer localizer, BotChoice choice)
    {
        _localizer = localizer;
        _choice = choice;
    }

    private static string Side(int direction)
    {
        return direction switch
        {
            0 => "SideRight",
            2 => "SideBottom",
            4 => "SideLeft",
            _ => "SideTop",
        };
    }

    private static bool IsCell(int cell)
    {
        return cell is >= 0 and < MapData.CellCount;
    }

    private static bool HasStairs(MapData map)
    {
        var moveZones = 0;

        foreach (var cell in map.Cells.Span)
        {
            if (cell.IsWalkable)
                moveZones |= 1 << Math.Min((int)cell.MoveZone, 1);
        }

        return moveZones is 3;
    }

    /// <summary>Reads the bots, and the chosen one's state, in the language chosen.</summary>
    /// <param name="services">The host's services while it runs, else <see langword="null"/>: what was drawn stays.</param>
    public void Refresh(IServiceProvider? services)
    {
        if (services is not null && _names is null)
        {
            _maps = services.GetService<GameMaps>();
            _world = services.GetService<GameWorld>();
            _names = new MapNames(_localizer, _world, services.GetService<GameTexts>(), services.GetService<GameTables>());
            _icons = GameIcons.Of(services.GetService<GameTables>());
            _says = new GameTextFormatter(_localizer, _world, services.GetService<GameTexts>(), services.GetService<GameTables>());
        }

        _bots = services?.GetService<Bots>() ?? _bots;
        RefreshBots(services?.GetService<Bots>());

        // The bot chosen on the logs page, the inspector or a card is the one shown.
        if (_choice.Key is { } key && Bot?.Bot?.Key != key && Find(key) is { } chosen)
            Choose(chosen);
        else if (Bot is null && Bots.Count > 0)
            Choose(Bots[0]);

        Read();
    }

    /// <summary>Shows a bot's map, as its card asks.</summary>
    /// <param name="key">The bot's character.</param>
    public void Show(CharacterKey key)
    {
        _choice.Key = key;

        if (Find(key) is { } source)
            Choose(source);
    }

    partial void OnShowsCellsChanged(bool value)
    {
        RefreshLayers();
    }

    partial void OnShowsCharacterChanged(bool value)
    {
        RefreshLayers();
    }

    partial void OnShowsElementsChanged(bool value)
    {
        RefreshLayers();
    }

    partial void OnShowsNpcsChanged(bool value)
    {
        RefreshLayers();
    }

    partial void OnShowsExitsChanged(bool value)
    {
        RefreshLayers();
    }

    partial void OnShowsBotsChanged(bool value)
    {
        RefreshLayers();
    }

    partial void OnShowsZonesChanged(bool value)
    {
        RefreshLayers();
    }

    partial void OnShowsActivityChanged(bool value)
    {
        RefreshLayers();
    }

    partial void OnShowsFightChanged(bool value)
    {
        RefreshLayers();
    }

    partial void OnHoveredCellChanged(int? value)
    {
        Describe();
    }

    private void RefreshLayers()
    {
        var layers = MapLayers.None;

        layers |= ShowsCells ? MapLayers.Cells : MapLayers.None;
        layers |= ShowsCharacter ? MapLayers.Character : MapLayers.None;
        layers |= ShowsElements ? MapLayers.Elements : MapLayers.None;
        layers |= ShowsNpcs ? MapLayers.Npcs : MapLayers.None;
        layers |= ShowsExits ? MapLayers.Exits : MapLayers.None;
        layers |= ShowsBots ? MapLayers.Bots : MapLayers.None;
        layers |= ShowsZones ? MapLayers.Zones : MapLayers.None;
        layers |= ShowsActivity ? MapLayers.Activity : MapLayers.None;
        layers |= ShowsFight ? MapLayers.Fight : MapLayers.None;
        Layers = layers;
    }

    private void RefreshBots(Bots? bots)
    {
        if (bots is null)
            return;

        foreach (var bot in bots.All.OrderBy(bot => bot.Key.AccountId).ThenBy(bot => bot.Key.CharacterId))
        {
            if (Find(bot.Key) is null)
                Bots.Add(new LogSourceViewModel(LogSourceKind.Bot, bot, Choose));
        }

        foreach (var source in Bots)
        {
            var bot = source.Bot!;

            source.Name = NameOf(bot);
            source.Tone = bot.IsPaused ? Tone.Warning : bot.IsConnected ? Tone.Success : Tone.Neutral;
        }

        HasNoBot = Bots.Count is 0;
    }

    private void Choose(LogSourceViewModel source)
    {
        foreach (var other in Bots)
            other.IsSelected = other == source;

        _choice.Key = source.Bot?.Key;

        if (Bot == source)
            return;

        Bot = source;
        Read();
    }

    private void Read()
    {
        if (Bot?.Bot is not { } bot || _names is null)
            return;

        var state = bot.State;
        var others = _bots is null ? [] : OthersOn(_bots, bot, state);
        var othersKey = string.Join(';', others.Select(other => $"{other.Name}:{other.Cell}"));
        var report = bot.Activity is { IsOver: false } run ? run.Activity.Report : null;

        if (bot == _drawnBot && ReferenceEquals(state, _drawnState) && othersKey == _drawnOthers && bot.IsConnected == _drawnConnected
            && bot.IsPaused == _drawnPaused && ReferenceEquals(report, _drawnReport) && _localizer.Culture.Equals(_drawnCulture))
        {
            return;
        }

        var tourChanged = report?.Focus?.Zone != _drawnReport?.Focus?.Zone || state.MapId != _drawnState?.MapId || bot != _drawnBot;

        _drawnBot = bot;
        _drawnState = state;
        _drawnOthers = othersKey;
        _drawnConnected = bot.IsConnected;
        _drawnPaused = bot.IsPaused;
        _drawnReport = report;
        _drawnCulture = _localizer.Culture;
        _others = others;
        RefreshHeader(bot, state);
        ActivityLine = ActivityLineOf(report);

        if (tourChanged)
            Tour = report?.Focus is { Zone.Length: > 0 } focus && _world is not null ? ZoneScenes.Of(focus.Zone, _world.Atlas, tour: true, character: state.MapId) : null;

        Scene = SceneOf(state, report?.Focus);
        Describe();
    }

    private string ActivityLineOf(ActivityReport? report)
    {
        if (report is null || _says is null)
            return "";

        var doing = _says.Say(report.Doing);

        return report.Progress is { } progress ? doing + Separator + _says.Say(progress.Text) : doing;
    }

    private List<(string Name, int Cell)> OthersOn(Bots bots, BotActor bot, CharacterState state)
    {
        return
        [
            .. bots.All
                .Where(other => other != bot && other.IsConnected && other.State.MapId == state.MapId && other.State.Cell is { } cell && IsCell(cell))
                .OrderBy(other => other.Key.AccountId)
                .ThenBy(other => other.Key.CharacterId)
                .Select(other => (NameOf(other), other.State.Cell!.Value)),
        ];
    }

    private void RefreshHeader(BotActor bot, CharacterState state)
    {
        var doing = !bot.IsConnected ? _localizer["Disconnected"] : bot.IsPaused ? _localizer["Paused"] : _localizer[BotViewModel.DoingOf(state)];

        if (state.MapId is not { } mapId)
        {
            Coordinates = NoValue;
            Place = _localizer["NotPlaced"];
            Detail = doing;

            return;
        }

        string place;

        // A fight's map and a haven bag lie at 0,0, off the world graph (D83, D88).
        if (state.IsFighting)
        {
            Coordinates = NoValue;
            place = _localizer["MapInFight"];
        }
        else if (state.IsInHavenBag)
        {
            Coordinates = NoValue;
            place = _localizer["InHavenBag"];
        }
        else if (_world is not null && _world.Atlas.TryGetPosition(mapId, out var position))
        {
            Coordinates = _localizer.Format("Coordinates", position.X, position.Y);
            place = _names!.SubArea(position.SubAreaId);
        }
        else
        {
            Coordinates = NoValue;
            place = _localizer["OffAtlas"];
        }

        Place = state.Cell is { } cell ? _localizer.Format("AreaCell", place, cell) : place;
        Detail = doing + Separator + _localizer.Format("MapNumber", mapId);
    }

    private MapScene? SceneOf(CharacterState state, ActivityFocus? focus)
    {
        if (state.MapId is not { } mapId)
        {
            Problem = _localizer["NotPlaced"];

            return null;
        }

        if (_maps is null || !_maps.TryGet(mapId, out var map))
        {
            Problem = _localizer.Format("MapNotInPack", mapId);

            return null;
        }

        Problem = null;

        // The exits' labels name places in the app's language: a new language says them again.
        if (!ReferenceEquals(map, _map) || !_localizer.Culture.Equals(_exitsCulture))
        {
            var exits = _world is null ? [] : MapExits.Of(map, _world.Graph);

            _exits = [.. exits.Select(exit => exit.Kind is MapExitKind.Scroll ? exit with { Label = _names!.Place(exit.Destinations[0]) } : exit)];
            _map = map;
            _exitsCulture = _localizer.Culture;
            _mapHasStairs = HasStairs(map);
        }

        var elements = ElementsOf(map, state);

        if (state.Fight is { } fight)
            return FightSceneOf(map, state, fight, elements);

        return new MapScene(map, _exits)
        {
            Elements = elements,
            Npcs = [.. state.Npcs.Values.Select(npc => npc.Cell ?? -1).Where(IsCell)],
            Bots = [.. _others.Select(other => other.Cell)],
            Character = state.Cell is { } cell && IsCell(cell) ? cell : null,
            Path = [.. state.Path.Where(IsCell)],
            Activity = focus is not null && focus.MapId == mapId ? ActivityOf(focus, elements) : null,
        };
    }

    private MapScene FightSceneOf(MapData map, CharacterState state, FightState fight, ImmutableArray<MapElementMark> elements)
    {
        var self = state.Key.CharacterId;
        var placing = fight.Phase is FightPhase.Placement;
        var others = fight.Fighters.Values.Where(fighter => fighter.Id != self && fighter.IsAlive && fighter.Cell is { } cell && IsCell(cell)).ToList();

        // The character stands on its fighter's cell: the client walks it on the fight's map with no path to confirm.
        return new MapScene(map, _exits)
        {
            Elements = elements,
            Bots = [.. _others.Select(other => other.Cell)],
            Character = fight.Fighters.GetValueOrDefault(self)?.Cell is { } own && IsCell(own) ? own : null,
            ChallengerCells = placing ? [.. fight.ChallengerCells.Where(IsCell)] : [],
            DefenderCells = placing ? [.. fight.DefenderCells.Where(IsCell)] : [],
            Allies = [.. others.Where(fighter => fighter.Team == fight.Team).Select(fighter => fighter.Cell!.Value)],
            Enemies = [.. others.Where(fighter => fighter.Team != fight.Team).Select(fighter => fighter.Cell!.Value)],
        };
    }

    private MapActivity ActivityOf(ActivityFocus focus, ImmutableArray<MapElementMark> elements)
    {
        var cells = elements.ToDictionary(element => element.ElementId, element => element.Cell);
        int? target = focus.TargetElementId is { } id && cells.TryGetValue(id, out var cell) ? cell : null;

        return new MapActivity
        {
            Wanted = [.. focus.WantedElementIds.Where(cells.ContainsKey).Select(element => cells[element])],
            Target = target,
            NextExit = focus.NextMapId is { } next
                ? [.. _exits.Where(exit => exit.Kind is not MapExitKind.Door && exit.Destinations.Contains(next)).SelectMany(exit => exit.Cells).Distinct()]
                : [],
        };
    }

    private ImmutableArray<MapElementMark> ElementsOf(MapData map, CharacterState state)
    {
        var doors = _exits.Where(exit => exit.Kind is MapExitKind.Door).Select(exit => exit.ElementId).ToHashSet();
        var marks = ImmutableArray.CreateBuilder<MapElementMark>();

        foreach (var element in map.Interactives.Span)
        {
            if (element.CellId == MapInteractive.NoCell)
                continue;

            var described = state.Elements.GetValueOrDefault(element.ElementId);
            var mark = doors.Contains(element.ElementId) ? MapMark.Door : MarkOf(described);

            marks.Add(new MapElementMark(element.ElementId, element.CellId, mark, mark is MapMark.Resource or MapMark.Harvested ? IconOf(described!) : null));
        }

        return marks.ToImmutable();
    }

    private IImage? IconOf(MapElement element)
    {
        var gathered = element.EnabledSkills.Concat(element.DisabledSkills).Select(skill => _names!.Gathered(skill.SkillId)).FirstOrDefault(item => item is not null);

        return gathered is { } item ? _icons?.Item(item) : null;
    }

    private MapMark MarkOf(MapElement? element)
    {
        if (element is null)
            return MapMark.Unknown;

        if (element.EnabledSkills.Any(skill => _names!.Gathers(skill.SkillId)))
            return MapMark.Resource;

        return element.DisabledSkills.Any(skill => _names!.Gathers(skill.SkillId)) ? MapMark.Harvested : MapMark.Element;
    }

    private void Describe()
    {
        if (HoveredCell is not { } cell || !IsCell(cell) || Scene is not { } scene || _names is null || _drawnState is not { } state)
        {
            HasHover = false;

            return;
        }

        var mapCell = scene.Map.Cell(cell);
        var point = MapGeometry.PointOf(cell);
        var stair = _mapHasStairs && mapCell.MoveZone is 1;

        HoverTitle = _localizer.Format("MapHoverCell", cell, point.X, point.Y);
        HoverDetail = mapCell.IsWalkable
            ? _localizer.Format(stair ? "MapStairs" : "MapWalkable", mapCell.Floor, mapCell.Zone)
            : _localizer[(mapCell.Properties & MapCellProperties.LineOfSight) is MapCellProperties.None ? "MapObstacle" : "MapVoid"];
        HoverItems = [.. ItemsOn(cell, scene, state)];
        HasHover = true;
    }

    private IEnumerable<string> ItemsOn(int cell, MapScene scene, CharacterState state)
    {
        var name = Bot?.Name ?? "";

        if (scene.Character == cell)
        {
            yield return scene.Path.Length > 1
                ? _localizer.Format("MapCharacterWalks", name, _localizer.Plural("PathCells", scene.Path.Length - 1))
                : _localizer.Format("MapCharacterHere", name);
        }

        if (scene.Path.Length > 1 && scene.Path[^1] == cell)
            yield return _localizer.Format("MapPathEnd", name);

        foreach (var other in _others.Where(other => other.Cell == cell))
            yield return _localizer.Format("MapOtherBot", other.Name);

        if (state.Fight is { } fight)
        {
            foreach (var fighter in fight.Fighters.Values.Where(fighter => fighter.IsAlive && fighter.Cell == cell && fighter.Id != state.Key.CharacterId))
                yield return FighterSaid(fighter, fight);

            if (scene.ChallengerCells.Contains(cell) || scene.DefenderCells.Contains(cell))
                yield return _localizer[scene.ChallengerCells.Contains(cell) == (fight.Team is FightTeam.Challenger) ? "MapOwnPlacement" : "MapEnemyPlacement"];
        }

        foreach (var element in scene.Elements.Where(element => element.Cell == cell && element.Mark is not MapMark.Door))
            yield return ElementSaid(element, state.Elements.GetValueOrDefault(element.ElementId));

        foreach (var npc in state.Npcs.Values.Where(npc => npc.Cell == cell))
            yield return _names!.Npc(npc.NpcId);

        foreach (var exit in scene.Exits.Where(exit => exit.Cells.Contains(cell)))
            yield return ExitSaid(exit, state);
    }

    private string FighterSaid(Fighter fighter, FightState fight)
    {
        var side = _localizer[fighter.Team == fight.Team ? "MapAlly" : "MapEnemy"];
        var who = fighter switch
        {
            { MonsterId: { } monster, SummonerId: not null } => _localizer.Format("MapSummon", _names!.Monster(monster)),
            { MonsterId: { } monster } => _localizer.Format("MapMonster", _names!.Monster(monster), fighter.Grade ?? 0),
            _ => _localizer.Format("MapCharacterFighter", fighter.Level ?? 0),
        };

        return _localizer.Format("MapFighter", side, who, fighter.Life, fighter.MaxLife, fighter.ActionPoints, fighter.MovementPoints);
    }

    private string ElementSaid(MapElementMark element, MapElement? described)
    {
        if (described is null)
            return _localizer.Format("MapNotDescribed", element.ElementId);

        var name = _names!.Element(element.ElementId, described.InteractiveId);

        if (element.Mark is MapMark.Harvested)
            return _localizer.Format("MapHarvested", name);

        var skills = string.Join(", ", described.EnabledSkills.Select(skill => _names.Skill(skill.SkillId)).Distinct());

        return skills.Length > 0 ? _localizer.Format("MapOffers", name, skills) : name;
    }

    private string ExitSaid(MapExit exit, CharacterState state)
    {
        var places = string.Join(Separator, exit.Destinations.Select(_names!.Place).Distinct());

        return exit.Kind switch
        {
            MapExitKind.Scroll => _localizer.Format("MapExitSide", _localizer[Side(exit.Direction)], places),
            MapExitKind.MapAction => _localizer.Format("MapActionTo", places),
            _ => _localizer.Format("MapLeadsTo", _names.Element(exit.ElementId, state.Elements.GetValueOrDefault(exit.ElementId)?.InteractiveId ?? -1), places),
        };
    }

    private LogSourceViewModel? Find(CharacterKey key)
    {
        return Bots.FirstOrDefault(source => source.Bot?.Key == key);
    }

    private string NameOf(BotActor bot)
    {
        return bot.State.Name.Length > 0 ? bot.State.Name : _localizer.Format("CharacterNamed", bot.Key.CharacterId);
    }

    partial void OnBotChanged(LogSourceViewModel? value)
    {
        OnPropertyChanged(nameof(Chosen));
    }
}
