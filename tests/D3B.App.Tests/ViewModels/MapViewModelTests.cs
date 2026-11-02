using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Maps;
using D3B.Bot.Characters;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.ViewModels;

public sealed class MapViewModelTests : IAsyncDisposable
{
    private const long OtherCharacterId = 70_002;

    private readonly TestPlay _play = new();
    private readonly TestGameData _game = new();
    private readonly IHost _host;
    private readonly Localizer _french = new(Localizer.French);
    private readonly Localizer _english = new(Localizer.English);

    public MapViewModelTests()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.Maps);
        builder.Services.AddSingleton(_play.World);
        builder.Services.AddSingleton(_game.Texts);
        builder.Services.AddSingleton(_game.Tables);
        _host = builder.Build();
    }

    public async ValueTask DisposeAsync()
    {
        _host.Dispose();
        await _play.DisposeAsync();
        _game.Dispose();
    }

    private static string Point(int cell)
    {
        var point = MapGeometry.PointOf(cell);

        return $"({point.X}, {point.Y})";
    }

    private static List<string> Hover(MapViewModel page, int cell)
    {
        page.HoveredCell = cell;

        Assert.True(page.HasHover);

        return [page.HoverTitle, page.HoverDetail, .. page.HoverItems];
    }

    private static Actor Fighter(long id, int cell, FightTeam team, int? monsterId = null)
    {
        var fighter = new FightFighter
        {
            Spawn = new FighterSpawn { Alive = true, Team = team },
            Stats = new FightCharacteristics
            {
                Characteristics =
                {
                    new Characteristic { Id = 0, Value = new CharacteristicValue { Total = 21 } },
                    new Characteristic { Id = 1, Value = new CharacteristicValue { Total = 2 } },
                    new Characteristic { Id = 23, Value = new CharacteristicValue { Total = 3 } },
                },
            },
        };

        if (monsterId is { } monster)
            fighter.Ai = new AiFighter { Monster = new MonsterFighter { MonsterId = monster, Grade = 3, Level = 3 } };
        else
            fighter.Named = new NamedFighter { Character = new FighterCharacter { BreedId = 15, Level = 18 } };

        return new Actor { Id = id, Disposition = new ActorDisposition { CellId = cell }, Information = new ActorInformation { Fighter = fighter } };
    }

    private MapViewModel Page(Localizer localizer, BotChoice? choice = null)
    {
        var page = new MapViewModel(localizer, choice ?? new BotChoice());

        page.Refresh(_host.Services);

        return page;
    }

    [Fact]
    public async Task ABotsMapShowsWhereItsCharacterStandsWithItsElementsAndExits()
    {
        await _play.EnterAsync(300);

        var page = Page(_french);
        var scene = page.Scene!;

        Assert.Equal(("4,-18", "Sous-zone 440, cellule 300", "Immobile · carte 191104002"), (page.Coordinates, page.Place, page.Detail));
        Assert.Equal((TestPlay.MapId, 300), (scene.Map.Id, scene.Character));
        Assert.Empty(scene.Path);
        Assert.Null(page.Problem);
        Assert.Equal(
            [new MapElementMark(TestPlay.TreeElementId, TestPlay.TreeCell, MapMark.Resource), new MapElementMark(TestPlay.DoorElementId, TestPlay.DoorCell, MapMark.Door)],
            scene.Elements);

        // The north edge's two rows scroll north; map actions lead north and east; the door leads into both insides.
        Assert.Equal(
            [
                (MapExitKind.Scroll, 6, 28, "4,-19", new[] { TestPlay.NorthMapId }),
                (MapExitKind.MapAction, -1, 1, "", [TestPlay.NorthMapId]),
                (MapExitKind.MapAction, -1, 1, "", [TestPlay.EastMapId]),
                (MapExitKind.Door, -1, 1, "", [TestPlay.InsideMapId, TestPlay.OtherInsideMapId]),
            ],
            scene.Exits.Select(exit => (exit.Kind, exit.Direction, exit.Cells.Length, exit.Label, exit.Destinations.ToArray())));
        Assert.Equal([TestPlay.NorthActionCell, TestPlay.ActionCell, TestPlay.DoorCell], scene.Exits.Skip(1).Select(exit => exit.Cells[0]));
        Assert.Equal(TestPlay.DoorElementId, scene.Exits[^1].ElementId);
        Assert.All(scene.Exits[0].Cells, cell => Assert.True(cell < 2 * MapGeometry.Columns));
    }

    [Fact]
    public async Task ACellUnderThePointerSaysWhatItHoldsInTheAppsLanguage()
    {
        await _play.EnterAsync(300);

        var french = Page(_french);
        var english = Page(_english);

        Assert.Equal([$"Cellule 176 {Point(176)}", "Marchable, étage 0, zone 1", "Frêne : Couper"], Hover(french, TestPlay.TreeCell));
        Assert.Equal([$"Cell 176 {Point(176)}", "Walkable, floor 0, zone 1", "Ash tree: Cut"], Hover(english, TestPlay.TreeCell));
        Assert.Equal("Porte mène à un intérieur en 4,-18", Hover(french, TestPlay.DoorCell)[^1]);
        Assert.Equal("Door leads to an inside at 4,-18", Hover(english, TestPlay.DoorCell)[^1]);
        Assert.Equal("Character est ici", Hover(french, 300)[^1]);
        Assert.Equal("Sort par le haut vers 4,-19", Hover(french, 5)[^1]);
        Assert.Equal("Leaves by the top to 4,-19", Hover(english, 5)[^1]);
        Assert.Equal("Action de carte vers 5,-18", Hover(french, TestPlay.ActionCell)[^1]);

        french.HoveredCell = null;

        Assert.False(french.HasHover);
    }

    [Fact]
    public async Task AWalkShowsItsPathAndAHarvestedTreeItsState()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var page = Page(_french);
        var first = page.Scene;

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302 } });
        connection.Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: false) });
        await bot.ProcessedAsync();
        page.Refresh(_host.Services);

        var scene = page.Scene!;

        Assert.NotSame(first, scene);
        Assert.Same(first!.Exits, scene.Exits);
        Assert.Equal([300, 301, 302], scene.Path);
        Assert.Equal("Marche · carte 191104002", page.Detail);
        Assert.Equal("Character part d'ici, 2 cellules à parcourir", Hover(page, 300)[^1]);
        Assert.Equal("Fin de la marche de Character", Hover(page, 302)[^1]);
        Assert.Equal(MapMark.Harvested, scene.Elements[0].Mark);
        Assert.Equal("Frêne : rien à récolter avant la repousse", Hover(page, TestPlay.TreeCell)[^1]);
    }

    [Fact]
    public async Task NothingChangedLeavesTheSceneAsItWas()
    {
        await _play.EnterAsync(300);

        var page = Page(_french);
        var scene = page.Scene;

        page.Refresh(_host.Services);

        Assert.Same(scene, page.Scene);
    }

    [Fact]
    public async Task AnInsideShowsItsNpcsTheBankerByName()
    {
        await _play.EnterAsync(300, TestPlay.InsideMapId);

        var page = Page(_french);

        Assert.Equal([259, 302], page.Scene!.Npcs.Order());
        Assert.Equal("Banquier", Hover(page, 259)[^1]);
        Assert.Equal("PNJ 1088", Hover(page, 302)[^1]);
        Assert.Empty(page.Scene.Exits);
    }

    [Fact]
    public async Task ACharacterNotOnAMapOrOnOneThePackLacksSaysSo()
    {
        var connection = _play.Connect(TestPlay.Account);

        connection.Server(TestPlay.Chosen());
        Assert.True(_play.Bots.TryGet(TestPlay.Key, out var bot));
        await bot.ProcessedAsync();

        var page = Page(_french);

        Assert.Equal(("Pas encore sur une carte", "—", null), (page.Problem, page.Coordinates, page.Scene));
        Assert.True(page.HasBot);

        connection.Server(new MapCurrentEvent { MapId = TestPlay.HavenBagMapId });
        await bot.ProcessedAsync();
        page.Refresh(_host.Services);

        Assert.Equal("La carte 162791424 n'est pas dans le pack de ce build.", page.Problem);
        Assert.False(page.HasScene);
    }

    [Fact]
    public async Task AFightShowsItsOwnMapThenTheMapItBeganOn()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var page = Page(_french);

        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.EastMapId });
        await bot.ProcessedAsync();
        page.Refresh(_host.Services);

        Assert.Equal(("—", "En combat, sur sa carte", "En combat · carte 191105026"), (page.Coordinates, page.Place, page.Detail));
        Assert.Equal((TestPlay.EastMapId, null), (page.Scene!.Map.Id, page.Scene.Character));

        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(TestPlay.Information(300));
        await bot.ProcessedAsync();
        page.Refresh(_host.Services);

        Assert.Equal(("4,-18", TestPlay.MapId, 300), (page.Coordinates, page.Scene!.Map.Id, page.Scene.Character));
    }

    [Fact]
    public async Task AFightsPlacementShowsTheTeamsCellsThenItsFightersAlone()
    {
        var (connection, bot) = await _play.EnterAsync(300);
        var page = Page(_french);

        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.EastMapId });
        connection.Server(new FightFighterShowEvent { Fighter = Fighter(TestPlay.CharacterId, 314, FightTeam.Challenger) });
        connection.Server(new FightFighterShowEvent { Fighter = Fighter(-1, 385, FightTeam.Defender, TestGameData.Gobball) });
        connection.Server(new FightFighterShowEvent { Fighter = Fighter(-2, 345, FightTeam.Challenger, 5_836) });
        connection.Server(new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { ChallengerCells = { 314, 300 }, DefenderCells = { 385, 371 } } });
        await bot.ProcessedAsync();
        page.Refresh(_host.Services);

        var placing = page.Scene!;

        Assert.Equal([314, 300], placing.ChallengerCells);
        Assert.Equal([385, 371], placing.DefenderCells);
        Assert.Equal(314, placing.Character);
        Assert.Equal([385], placing.Enemies);
        Assert.Equal([345], placing.Allies);
        Assert.Contains("Ennemi : Bouftou, grade 3, 21/21 PV, 2 PA, 3 PM", Hover(page, 385));
        Assert.Contains("Allié : monstre 5836, grade 3, 21/21 PV, 2 PA, 3 PM", Hover(page, 345));
        Assert.Contains("Cellule de placement de l'autre équipe", Hover(page, 385));
        Assert.Contains("Cellule de placement de son équipe", Hover(page, 300));

        connection.Server(new FightStartEvent());
        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 314, 328 } });
        connection.Server(new GameActionFightEvent { ActionId = 103, SourceId = TestPlay.CharacterId, Death = new FightDeath { TargetId = -1 } });
        await bot.ProcessedAsync();
        page.Refresh(_host.Services);

        Assert.Empty(page.Scene!.ChallengerCells);
        Assert.Empty(page.Scene.DefenderCells);
        Assert.Empty(page.Scene.Enemies);
        Assert.Equal(328, page.Scene.Character);

        page.ShowsFight = false;

        Assert.Equal(MapLayers.Default & ~MapLayers.Fight, page.Layers);
    }

    [Fact]
    public async Task AnotherBotOnTheMapShowsAndTheChosenBotIsShared()
    {
        var (_, bot) = await _play.EnterAsync(300);
        var connection = _play.Connect(TestPlay.Account);

        connection.Server(TestPlay.Chosen(OtherCharacterId));
        Assert.True(_play.Bots.TryGet(new CharacterKey(TestPlay.AccountId, OtherCharacterId), out var other));
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(new MapInformationEvent
        {
            MapId = TestPlay.MapId,
            SubAreaId = TestPlay.SubAreaId,
            Actors = { new Actor { Id = OtherCharacterId, Disposition = new ActorDisposition { CellId = 350 } } },
        });
        await other.ProcessedAsync();

        var choice = new BotChoice();
        var page = Page(_french, choice);

        Assert.Equal(bot.Key, choice.Key);
        Assert.Equal([350], page.Scene!.Bots);
        Assert.Equal("Character, un autre bot", Hover(page, 350)[^1]);

        page.Show(other.Key);

        Assert.Equal((other.Key, 350, 300), (choice.Key, page.Scene!.Character, page.Scene.Bots.Single()));
    }

    [Fact]
    public void TheLayersShowAllButTheZonesAtFirstEachToggled()
    {
        var page = new MapViewModel(_french, new BotChoice());

        Assert.Equal(MapLayers.Default, page.Layers);

        page.ShowsZones = true;
        page.ShowsCells = false;
        page.ShowsNpcs = false;

        Assert.Equal((MapLayers.Default | MapLayers.Zones) & ~MapLayers.Cells & ~MapLayers.Npcs, page.Layers);
    }

    [Fact]
    public void WithoutABotThePageSaysSo()
    {
        var page = Page(_french);

        Assert.True(page.HasNoBot);
        Assert.False(page.HasBot);
        Assert.Null(page.Scene);
    }
}
