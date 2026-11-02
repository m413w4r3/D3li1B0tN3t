using System.Collections.Frozen;
using D3B.Bot.Characters;
using D3B.Bot.Interactives;
using D3B.Bot.Network;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Network;
using D3B.Data;
using D3B.Data.Banks;
using D3B.Data.Breeds;
using D3B.Data.Effects;
using D3B.Data.Icons;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Maps;
using D3B.Data.Monsters;
using D3B.Data.Packs;
using D3B.Data.Spells;
using D3B.Data.Tests;
using D3B.Data.World;
using D3B.Engine.Maps;
using D3B.Engine.Criteria;
using D3B.Engine.World;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Runtime;

/// <summary>
/// A host's bots as a test plays them: a pack holding five maps, the world between them and a bank, every cell walkable
/// but those a test blocks on the first, the bots observing the game connections a test drives, and a clock the test moves.
/// </summary>
/// <remarks>
/// The world: <see cref="NorthMapId"/> is north of <see cref="MapId"/>, by a scroll each way, and by a map action from
/// <see cref="NorthActionCell"/>; a map action from <see cref="ActionCell"/> leads to <see cref="EastMapId"/>, whose west
/// edge scrolls back; the door <see cref="DoorElementId"/> leads to <see cref="InsideMapId"/> and to
/// <see cref="OtherInsideMapId"/>, both at <see cref="MapId"/>'s coordinates, as the bank of Astrub's leads to its inside
/// and to another; a map action from <see cref="OtherActionCell"/> leads out of the other. Every cell of a map's edge
/// leaves it straight across. On <see cref="MapId"/>, the server lists the door and a tree, <see cref="TreeElementId"/>,
/// as the bank of Astrub's map and a map near it list them in the captures. The pack's bank stands in front of
/// <see cref="MapId"/>; its banker, <see cref="BankerNpcId"/>, stands in <see cref="InsideMapId"/> beside an NPC who is
/// none, and the other inside holds no NPC. A character teleports from the three maps outside; <see cref="EastMapId"/>
/// holds a zaap, as Astrub's does, and <see cref="HavenBagMapId"/>, which the pack lacks, is a haven bag. Every map is of
/// <see cref="SubAreaId"/>, which lists <see cref="Ash"/>, which the tree's skill gathers, and <see cref="Chestnut"/>,
/// which a lumberjack gathers from level 20. The pack's fight data holds one spell, <see cref="BlowSpellId"/>, an earth blow
/// of 10 to 12 on an enemy for 3 AP at a range of 1 to 4, spell 0's punch, a sword, <see cref="SwordItemId"/>, striking
/// earth 20 to 25 for 3 AP once a turn, and the two effects of the tactics' tests; no monster, breed or state.
/// </remarks>
internal sealed class TestPlay : IAsyncDisposable
{
    public const long MapId = 191_104_002;
    public const long NorthMapId = 191_104_000;
    public const long EastMapId = 191_105_026;
    public const long InsideMapId = 192_415_750;
    public const long OtherInsideMapId = 192_677_890;
    public const long HavenBagMapId = 162_791_424;
    public const int OtherActionCell = 400;
    public const int BankerNpcId = 520;
    public const int BankerActorId = -20_001;
    public const int ChestReplyId = 64_361;
    public const int OtherReplyId = 64_362;
    public const int WayNpcId = 1_088;
    public const int WayActorId = -20_000;
    public const int WayReplyId = 36_979;
    public const int WayLastReplyId = 36_977;
    public const int StayReplyId = 36_978;
    public const int ActionCell = 409;
    public const int NorthActionCell = 200;
    public const int TreeElementId = 514_662;
    public const int TreeCell = 176;
    public const int TreeGfxId = 650;
    public const int TreeSkillId = 40;
    public const int TreeInstanceId = 19_146;
    public const int Lumberjack = 2;
    public const int Ash = 303;
    public const int Chestnut = 473;
    public const int Bread = 468;
    public const int WheatBag = 7_941;
    public const int Loaf = 1_737;
    public const int ClassPotion = 16_147;
    public const int AshIconId = 38_017;
    public const int ChestnutIconId = 38_086;
    public const int LumberjackIconId = 1;
    public const int ChestnutSkillId = 39;
    public const int DoorElementId = 515_308;
    public const int DoorCell = 288;
    public const int DoorGfxId = 3_518;
    public const int DoorInstanceId = 23_074;
    public const int NeighboursElementId = 514_594;
    public const int NeighboursElementCell = 435;
    public const int BushElementId = 514_701;
    public const int BushGfxId = 7_001;
    public const int ChestElementId = 514_702;
    public const int ChestGfxId = 7_002;
    public const int SubAreaId = 440;
    public const long AccountId = 7;
    public const uint TeleportFlag = 1 << 3;
    public const long CharacterId = 70_001;
    public const int BlowSpellId = 100;
    public const int BlowLevelId = 1_100;
    public const int SwordItemId = 6_751;

    // The one monster of the world, living in its sub-area, its grades from level 10 to 14.
    public const int LarvaId = 489;

    // Items', professions' and monsters' icons are 64 pixels square in a pack (D136).
    private const int IconSize = 64;

    private static readonly string[] _weaponZones = ["P"];

    private static readonly RelaySettings _settings = new()
    {
        Address = RelaySettings.DefaultAddress,
        Ports = RelaySettings.DefaultPorts,
        GamePorts = RelaySettings.DefaultGamePorts,
        ClientDirectory = "no-client",
        RedirectDirectory = "no-redirect",
    };

    private readonly TemporaryDirectory _directory = new();
    private readonly GamePack _pack;
    private readonly ILoggerFactory _logs;
    private readonly GameAccounts _accounts;
    private readonly RelayDecoder _decoder;
    private readonly List<TestConnection> _connections = [];

    public static GameAccount Account { get; } = new(AccountId, "nickname", "tag");

    public static CharacterKey Key { get; } = new(AccountId, CharacterId);

    public static GameAccount SubscribedAccount { get; } = Account with { SubscriptionEnd = DateTimeOffset.MaxValue };

    public FakeTimeProvider Time { get; } = new();

    public FakeLogCollector Log { get; } = new();

    public ProtocolTranslation Translation { get; } = TestGame.Translation();

    public GameMaps Maps { get; }

    public GameWorld World { get; }

    public GameTables Tables { get; }

    public GraphicPlaces Places { get; }

    public Bots Bots { get; }

    public TestPlay(params int[] blocked)
        : this([], blocked)
    {
    }

    /// <summary>A play whose game connections other observers see too, after the bots.</summary>
    public TestPlay(IReadOnlyList<IGameObserver> observers, params int[] blocked)
        : this(observers, [], blocked)
    {
    }

    private TestPlay(IReadOnlyList<IGameObserver> observers, IReadOnlyList<NpcWay> npcWays, int[] blocked)
    {
        var path = _directory.Combine("data-3.pack");

        File.WriteAllBytes(path, Pack(blocked));
        _pack = GamePack.Open(path);
        Maps = new GameMaps(_pack);
        World = new GameWorld(_pack);
        Tables = new GameTables(_pack);
        Places = new GraphicPlaces(_pack, World);
        _logs = LoggerFactory.Create(logging => logging.AddProvider(new FakeLoggerProvider(Log)).SetMinimumLevel(LogLevel.Debug));
        // The wait between two steps is a test's own to ask for: the others drive their steps by the server's answers alone.
        Bots = new Bots(Maps, World, Tables, Time, _logs, npcWays) { Delays = BotDelays.Measured with { BetweenSteps = TimeSpan.Zero } };
        _accounts = new GameAccounts(_settings.SelectionLifetime, Time);
        _decoder = new RelayDecoder(Translation, new GameServerSelections(_settings, Time), _accounts, [Bots, .. observers], _logs);
    }

    /// <summary>A play whose journeys go through an NPC's way: the NPC who is no banker, in <see cref="InsideMapId"/>, leads to <see cref="NorthMapId"/> by <see cref="WayReplyId"/> then <see cref="WayLastReplyId"/> (D253).</summary>
    public static TestPlay ThroughAnNpc()
    {
        return new TestPlay([], [new NpcWay(InsideMapId, WayNpcId, new[] { WayReplyId, WayLastReplyId }.ToFrozenSet(), NorthMapId)], []);
    }

    /// <summary>The character as the server's confirmation of the choice describes it.</summary>
    public static CharacterSelectionEvent Chosen(long characterId = CharacterId)
    {
        return new CharacterSelectionEvent
        {
            Success = new CharacterSelectionEvent.Types.Success
            {
                Character = new Character { Id = characterId, Basics = new CharacterBasics { Name = "Character", Level = 200 } },
            },
        };
    }

    /// <summary>What is on the map, the character on a cell; on <see cref="MapId"/>, the tree and the door; in <see cref="InsideMapId"/>, the banker and another NPC.</summary>
    public static MapInformationEvent Information(int cell, long mapId = MapId)
    {
        var information = new MapInformationEvent
        {
            MapId = mapId,
            SubAreaId = SubAreaId,
            Actors =
            {
                new Actor { Id = -1, Disposition = new ActorDisposition { CellId = 101 } },
                new Actor { Id = CharacterId, Disposition = new ActorDisposition { CellId = cell } },
            },
        };

        if (mapId is InsideMapId)
        {
            information.Actors.Add(Npc(WayActorId, WayNpcId, 302));
            information.Actors.Add(Npc(BankerActorId, BankerNpcId, 259));
        }

        if (mapId is MapId)
        {
            information.InteractiveElements.Add(Tree(enabled: true));
            information.InteractiveElements.Add(new InteractiveElement
            {
                ElementId = DoorElementId,
                InteractiveId = 316,
                OnCurrentMap = true,
                EnabledSkills = { new InteractiveSkill { SkillId = WorldPathfinder.DoorSkillId, InstanceId = DoorInstanceId } },
            });
            information.StatedElements.Add(new StatedElement { ElementId = TreeElementId, CellId = TreeCell, State = 0, OnCurrentMap = true });

            // A tree of the map north, as the information lists the neighbouring maps' elements.
            information.InteractiveElements.Add(new InteractiveElement
            {
                ElementId = NeighboursElementId,
                InteractiveId = 34,
                EnabledSkills = { new InteractiveSkill { SkillId = TreeSkillId, InstanceId = 4_478 } },
            });
            information.StatedElements.Add(new StatedElement { ElementId = NeighboursElementId, CellId = NeighboursElementCell, State = 0 });
        }

        return information;
    }

    /// <summary>The tree, its skill offered or not, as the server describes it.</summary>
    public static InteractiveElement Tree(bool enabled)
    {
        var skill = new InteractiveSkill { SkillId = TreeSkillId, InstanceId = TreeInstanceId };
        var tree = new InteractiveElement { ElementId = TreeElementId, InteractiveId = 34, OnCurrentMap = true };

        (enabled ? tree.EnabledSkills : tree.DisabledSkills).Add(skill);

        return tree;
    }

    private static Actor Npc(long actorId, int npcId, int cell)
    {
        return new Actor
        {
            Id = actorId,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation { Roleplay = new RoleplayActor { Npc = new NpcActor { NpcId = npcId } } },
        };
    }

    /// <summary>An icon of the test's own, nothing of the client's: a disc of one colour on a transparent square.</summary>
    private static byte[] Disc(byte red, byte green, byte blue)
    {
        var pixels = new byte[IconSize * IconSize * 4];
        var center = (IconSize - 1) / 2.0;

        for (var y = 0; y < IconSize; y++)
        {
            for (var x = 0; x < IconSize; x++)
            {
                if (Math.Pow(x - center, 2) + Math.Pow(y - center, 2) > Math.Pow(IconSize * 0.42, 2))
                    continue;

                var at = ((y * IconSize) + x) * 4;

                (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = (red, green, blue, 255);
            }
        }

        return pixels;
    }

    private static byte[] Pack(int[] blocked)
    {
        var cells = Enumerable.Range(0, MapData.CellCount).Select(cell => blocked.Contains(cell) ? Wall() : Walkable(cell)).ToArray();
        var open = Enumerable.Range(0, MapData.CellCount).Select(Walkable).ToArray();
        var info = new PackInfo { ClientVersion = TestInstall.Version, Fingerprint = "0123456789abcdef", ExtractorVersion = "0.0.0-test" };

        using var stream = new MemoryStream();

        using (var writer = new PackWriter(stream, info, 3))
        {
            writer.Add(MapData.EntryName(MapId), new MapData(MapId, NorthMapId, 0, 0, 0, cells, new[] { new MapInteractive(TreeElementId, TreeCell, TreeGfxId), new MapInteractive(DoorElementId, DoorCell, DoorGfxId) }));
            writer.Add(MapData.EntryName(NorthMapId), new MapData(NorthMapId, 0, MapId, 0, 0, open, new[] { new MapInteractive(NeighboursElementId, NeighboursElementCell, TreeGfxId) }));
            writer.Add(MapData.EntryName(EastMapId), new MapData(EastMapId, 0, 0, MapId, 0, open, new[] { new MapInteractive(BushElementId, 300, BushGfxId) }));
            writer.Add(MapData.EntryName(InsideMapId), new MapData(InsideMapId, 0, 0, 0, 0, open, new[] { new MapInteractive(ChestElementId, 300, ChestGfxId) }));
            writer.Add(MapData.EntryName(OtherInsideMapId), new MapData(OtherInsideMapId, 0, 0, 0, 0, open, Array.Empty<MapInteractive>()));
            writer.Add(WorldGraph.EntryName, Graph());
            writer.Add(WorldAtlas.EntryName, Atlas());
            writer.Add(BankCatalog.EntryName, new BankCatalog(new[] { new Bank(2_756, MapId) }, new[] { new Banker(BankerNpcId, 0, 0, 2) }, new[] { 64_360, ChestReplyId }));
            writer.Add(ItemCatalog.EntryName, new ItemCatalog(new[] { new Item(Ash, 403, 38, 1, 5, AshIconId), new Item(Bread, 468, 33, 1, 1, 0, 12, 12), new Item(Chestnut, 404, 38, 20, 5, ChestnutIconId), new Item(Loaf, 1_737, 33, 110, 1, 0, 30, 40), new Item(WheatBag, 7_941, 100, 1, 1, 0), new Item(ClassPotion, 16_147, 12, 1, 1, 0) }, Array.Empty<ItemType>()));
            writer.Add(IconCatalog.EntryName, new IconCatalog(
                new IconTable(IconSize, new[] { AshIconId, ChestnutIconId }, Disc(0x8B, 0xC3, 0x4A).Concat(Disc(0x9C, 0x5B, 0x2E)).ToArray()),
                new IconTable(IconSize, new[] { LumberjackIconId }, Disc(0x3B, 0x82, 0xF6)),
                new IconTable(48, Array.Empty<int>(), Array.Empty<byte>()),
                new IconTable(IconSize, Array.Empty<int>(), Array.Empty<byte>())));
            writer.Add(SpellCatalog.EntryName, Spells());
            writer.Add(WeaponCatalog.EntryName, new WeaponCatalog(
                new[] { new Weapon { ItemId = SwordItemId, TypeId = 6, ApCost = 3, MinRange = 1, Range = 1, CriticalHitProbability = 0, MaxCastPerTurn = 1, Properties = SpellLevelProperties.CastTestLos, FirstEffect = 0, EffectCount = 1 } },
                new[] { new WeaponEffect { EffectId = 97, DiceNum = 20, DiceSide = 25 } },
                new[] { new WeaponType { Id = 6, Zone = 0 } },
                _weaponZones));
            writer.Add(SpellStateCatalog.EntryName, new SpellStateCatalog(Array.Empty<SpellState>(), ReadOnlyMemory<int>.Empty));
            writer.Add(EffectCatalog.EntryName, new EffectCatalog(new Effect[]
            {
                new(97, 0, 0, '\0', 2, 1, 0, 0, 0, 0, EffectProperties.UseInFight),
                new(108, 0, 0, '\0', 2, 2, 0, 0, 0, 0, EffectProperties.UseInFight),
            }));
            writer.Add(MonsterCatalog.EntryName, new MonsterCatalog(
                new[] { new Monster { Id = LarvaId, FirstGrade = 0, GradeCount = 2 } },
                new[] { new MonsterGrade { Grade = 1, Level = 10 }, new MonsterGrade { Grade = 2, Level = 14 } },
                Array.Empty<MonsterSpell>(),
                Array.Empty<byte>(),
                Array.Empty<MonsterRatio>(),
                Array.Empty<MonsterRace>(),
                Array.Empty<MonsterRace>()));
            writer.Add(BreedCatalog.EntryName, new BreedCatalog(Array.Empty<Breed>(), Array.Empty<int>(), Array.Empty<BreedRole>(), Array.Empty<BreedRoleValue>(), Array.Empty<BreedStatCost>()));
            writer.Add(JobCatalog.EntryName, new JobCatalog(
                new[] { new Job(Job.Base, 0, Job.NoIcon), new Job(Lumberjack, 202, LumberjackIconId) },
                new[] { new Skill(ChestnutSkillId, 0, Lumberjack, Chestnut, 20, 1, false), new Skill(TreeSkillId, 0, Lumberjack, Ash, 1, 1, false), new Skill(WorldPathfinder.DoorSkillId, 0, Job.Base, Skill.NoItem, 0, 1, false) }));
            writer.Complete();
        }

        return stream.ToArray();
    }

    private static SpellCatalog Spells()
    {
        var point = new SpellZone { Shape = SpellZoneShape.Point, Size = 1, DamageDecreaseStepPercent = 10, MaxDamageDecreaseApplyCount = 4, ForcedDirection = -1 };
        SpellLevel[] levels =
        [
            new SpellLevel { Id = BlowLevelId, SpellId = BlowSpellId, Grade = 1, ApCost = 3, MinRange = 1, Range = 4, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 0, EffectCount = 1 },
            new SpellLevel { Id = 10_461, SpellId = 0, Grade = 1, ApCost = 3, MinRange = 1, Range = 1, StatesCriterion = SpellLevel.NoCriterion, FirstEffect = 1, EffectCount = 1 },
        ];
        SpellEffect[] effects =
        [
            new SpellEffect { Uid = 1, EffectId = 97, TargetMask = 0, Triggers = 1, DiceNum = 10, DiceSide = 12, Zone = point },
            new SpellEffect { Uid = 2, EffectId = 97, TargetMask = 3, Triggers = 1, DiceNum = 6, DiceSide = 7, Zone = point },
        ];
        Spell[] spells = [new Spell { Id = 0, NameId = 1, FirstLevel = 0, LevelCount = 1 }, new Spell { Id = BlowSpellId, NameId = BlowSpellId, FirstLevel = 1, LevelCount = 1 }];
        int[] levelIds = [10_461, BlowLevelId];
        string[] strings = ["A", "I", "a", "a,A"];

        return new SpellCatalog(spells, levels, levelIds, effects, Array.Empty<SpellPreviewZone>(), Array.Empty<int>(), strings, Array.Empty<SpellVariant>(), Array.Empty<SpellBomb>(), Array.Empty<SpellBombWall>());
    }

    private static MapCell Walkable(int cell)
    {
        var leaves = Enumerable.Range(0, MapCell.DirectionCount / 2).Select(side => 2 * side).Where(direction => MapGeometry.Neighbour(cell, direction) < 0);

        return new MapCell(MapCellProperties.Walkable | MapCellProperties.LineOfSight, 0, 0, leaves.Sum(direction => 1 << direction), 0, 1 << 4, 0);
    }

    private static MapCell Wall()
    {
        return new MapCell(MapCellProperties.None, 0, 0, 0, 0, 0, 0);
    }

    private static WorldGraph Graph()
    {
        // Vertices in ascending order of map: north, the first, east, inside, the other inside.
        WorldVertex[] vertices = [new(NorthMapId, 1), new(MapId, 1), new(EastMapId, 1), new(InsideMapId, 1), new(OtherInsideMapId, 1)];
        WorldTransition[] transitions =
        [
            Transition(WorldTransitionType.Scroll, 2, 539, MapId),
            Transition(WorldTransitionType.Scroll, 6, 6, NorthMapId),
            Transition(WorldTransitionType.MapAction, WorldTransition.NoDirection, NorthActionCell, NorthMapId),
            Transition(WorldTransitionType.MapAction, WorldTransition.NoDirection, ActionCell, EastMapId),
            new(WorldTransitionType.Interactive, WorldTransition.NoDirection, DoorCell, WorldPathfinder.DoorSkillId, InsideMapId, DoorElementId, WorldTransition.None),
            new(WorldTransitionType.Interactive, WorldTransition.NoDirection, DoorCell, WorldPathfinder.DoorSkillId, OtherInsideMapId, DoorElementId, WorldTransition.None),
            Transition(WorldTransitionType.Scroll, 4, 252, MapId),
            Transition(WorldTransitionType.MapAction, WorldTransition.NoDirection, OtherActionCell, MapId),
        ];
        WorldEdge[] edges = [new(0, 1, 0, 1), new(1, 0, 1, 2), new(1, 2, 3, 1), new(1, 3, 4, 1), new(1, 4, 5, 1), new(2, 1, 6, 1), new(4, 1, 7, 1)];

        return new WorldGraph(vertices, edges, transitions, Array.Empty<string>());
    }

    private static WorldTransition Transition(WorldTransitionType type, int direction, int cell, long mapId)
    {
        return new WorldTransition(type, direction, cell, WorldTransition.None, mapId, WorldTransition.None, WorldTransition.None);
    }

    private static WorldAtlas Atlas()
    {
        MapPosition[] positions =
        [
            new(NorthMapId, 4, -19, SubAreaId, 1, 0, TeleportFlag),
            new(MapId, 4, -18, SubAreaId, 1, 0, TeleportFlag),
            new(EastMapId, 5, -18, SubAreaId, 1, 0, TeleportFlag),
            new(InsideMapId, 4, -18, SubAreaId, -1, 0, 0),
            new(OtherInsideMapId, 4, -18, SubAreaId, -1, 0, 0),
        ];
        SubArea[] subAreas = [new(SubAreaId, 0, 1, 10, SubArea.NoZaap, true, 0, 2, 0, 1)];

        return new WorldAtlas(positions, subAreas, new[] { new Area(1, 0, 0) }, Array.Empty<Waypoint>(), new[] { Ash, Chestnut }, new[] { LarvaId });
    }

    /// <summary>A game connection, its first request presenting a ticket the connection phase gave an account, or none.</summary>
    public TestConnection Connect(GameAccount? account)
    {
        var id = $"g{_connections.Count + 1}";
        var ticket = $"ticket-of-{id}";
        var fake = new FakeRelayConnection(id, RelayRoute.Game);
        var connection = new TestConnection(fake, _decoder.Intercept(fake)!, Translation);

        if (account is not null)
            _accounts.Remember(ticket, account);

        _connections.Add(connection);
        connection.Client(new AuthenticationRequest { Ticket = ticket, Language = "fr" });

        return connection;
    }

    /// <summary>A connection of the account that chose the character, on a map, standing on a cell, the map drawn: the bot playing it.</summary>
    public async Task<(TestConnection Connection, BotActor Bot)> EnterAsync(int cell, long mapId = MapId, GameAccount? account = null)
    {
        var connection = Connect(account ?? Account);

        connection.Server(Chosen());
        Assert.True(Bots.TryGet(Key, out var bot));
        connection.Server(new MapCurrentEvent { MapId = mapId });
        connection.Server(Information(cell, mapId));
        await bot.ProcessedAsync();

        // The client has drawn the map: the bot's requests go at once.
        Time.Advance(BotDelays.Measured.Display);

        return (connection, bot);
    }

    /// <summary>The first step of the path from where the bot's character stands to a map.</summary>
    public WorldStep StepTo(BotActor bot, long mapId)
    {
        var pathfinder = new WorldPathfinder(World.Graph, World.Atlas);
        var state = bot.State;

        Assert.True(pathfinder.TryGetVertex(Maps.Get(state.MapId!.Value), state.Cell!.Value, out var vertex));
        Assert.True(pathfinder.TryFind(vertex, new HashSet<long> { mapId }, new WorldPathRules { Facts = new CriterionFacts { Subscribed = true } }, out var path));

        return path.Steps[0];
    }

    public async ValueTask DisposeAsync()
    {
        await Bots.DisposeAsync();

        foreach (var connection in _connections)
            connection.Dispose();

        _logs.Dispose();
        _pack.Dispose();
        _directory.Dispose();
    }
}
