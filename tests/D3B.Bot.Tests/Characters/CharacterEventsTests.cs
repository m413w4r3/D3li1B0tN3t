using D3B.Bot.Characters;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using System.Text.Json;
using Google.Protobuf;

namespace D3B.Bot.Tests.Characters;

public sealed class CharacterEventsTests
{
    private static readonly CharacterState _chosen = CharacterState.Chosen(TestPlay.Key, TestPlay.Chosen().Success.Character);
    private static readonly JsonSerializerOptions _snapshot = new() { WriteIndented = true };

    private static CharacterState Apply(CharacterState state, params (RelayDirection Side, IMessage Message)[] messages)
    {
        return messages.Aggregate(state, (current, message) => CharacterEvents.Apply(current, message.Side, message.Message));
    }

    private static (RelayDirection, IMessage) Server(IMessage message)
    {
        return (RelayDirection.FromServer, message);
    }

    private static (RelayDirection, IMessage) Client(IMessage message)
    {
        return (RelayDirection.FromClient, message);
    }

    /// <summary>A group of monsters on the map, as the server describes it: its main monster, then the others.</summary>
    private static Actor GroupActor(long actorId, int cell, GroupMonster main, params GroupMonster[] others)
    {
        var monsters = new MonsterGroup { Main = main };

        monsters.Others.AddRange(others);

        return new Actor
        {
            Id = actorId,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation { Roleplay = new RoleplayActor { MonsterGroup = new MonsterGroupActor { Monsters = monsters } } },
        };
    }

    /// <summary>What the server sends on entering the game, in the order of the captures (client.md).</summary>
    private static (RelayDirection, IMessage)[] EnteringTheGame()
    {
        return
        [
            Server(new InventoryContentEvent
            {
                Kamas = 12_500,
                Objects =
                {
                    new InventoryObject { Item = new ObjectItem { Uid = 30, ItemId = 2_400, Quantity = 1 }, Position = 1 },
                    new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = 303, Quantity = 40 }, Position = 63 },
                },
            }),
            Server(new InventoryWeightEvent { Weight = 770, MaxWeight = 7_590 }),
            Server(new JobDescriptionEvent { Jobs = { new JobDescription { JobId = 2, Skills = { new JobSkill { SkillId = 6 }, new JobSkill { SkillId = 39 } } } } }),
            Server(new JobExperiencesEvent
            {
                Jobs = { new JobExperience { JobId = 2, Level = 111, Experience = 123_518, LevelFloor = 122_100, NextLevelFloor = 124_320 } },
            }),
            Server(new ZaapKnownListEvent { MapIds = { 191_105_026, 120_062_979 } }),
            Server(new SpellsEvent { Spells = { new SpellItem { SpellId = 13_286, Grade = 2 }, new SpellItem { SpellId = 0, Grade = 1 } } }),
            Client(new ContextCreationRequest()),
            Server(new ContextDestroyEvent()),
            Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay }),
            Server(new MapCurrentEvent { MapId = TestPlay.MapId }),
            Client(new MapInformationRequest { MapId = TestPlay.MapId }),
            Server(TestPlay.Information(300)),
        ];
    }

    [Fact]
    public void EnteringTheGameGivesTheCharactersState()
    {
        Assert.Equal(
            """
            {
              "Key": {
                "AccountId": 7,
                "CharacterId": 70001
              },
              "Name": "Character",
              "Level": 200,
              "MapId": 191104002,
              "SubAreaId": 440,
              "Cell": 300,
              "Path": [],
              "IsWalking": false,
              "IsWalkHeld": false,
              "LastWalkEnd": null,
              "Elements": {
                "514662": {
                  "ElementId": 514662,
                  "InteractiveId": 34,
                  "EnabledSkills": [
                    {
                      "SkillId": 40,
                      "InstanceId": 19146
                    }
                  ],
                  "DisabledSkills": [],
                  "Cell": 176,
                  "State": 0
                },
                "515308": {
                  "ElementId": 515308,
                  "InteractiveId": 316,
                  "EnabledSkills": [
                    {
                      "SkillId": 184,
                      "InstanceId": 23074
                    }
                  ],
                  "DisabledSkills": [],
                  "Cell": null,
                  "State": null
                }
              },
              "Use": null,
              "IsUsing": false,
              "Npcs": {},
              "MonsterGroups": {},
              "Dialog": null,
              "Storage": null,
              "ZaapDialog": null,
              "IsInDialog": false,
              "KnownZaaps": [
                120062979,
                191105026
              ],
              "HavenBagOwnerId": null,
              "IsInHavenBag": false,
              "Fight": null,
              "IsFighting": false,
              "IsBusy": false,
              "Inventory": {
                "12": {
                  "Uid": 12,
                  "ItemId": 303,
                  "Quantity": 40,
                  "Position": 63,
                  "IsWorn": false
                },
                "30": {
                  "Uid": 30,
                  "ItemId": 2400,
                  "Quantity": 1,
                  "Position": 1,
                  "IsWorn": true
                }
              },
              "Weapon": {
                "Uid": 30,
                "ItemId": 2400,
                "Quantity": 1,
                "Position": 1,
                "IsWorn": true
              },
              "Kamas": 12500,
              "Life": null,
              "MaxLife": null,
              "Characteristics": {},
              "LifeRegenInterval": null,
              "Weight": 770,
              "MaxWeight": 7590,
              "Load": 0.10144927536231885,
              "Jobs": {
                "2": {
                  "JobId": 2,
                  "Level": 111,
                  "Experience": 123518,
                  "LevelFloor": 122100,
                  "NextLevelFloor": 124320,
                  "Skills": [
                    6,
                    39
                  ]
                }
              },
              "Spells": {
                "0": 1,
                "13286": 2
              },
              "TransformedSpells": {},
              "SubscriptionEnd": null
            }
            """,
            JsonSerializer.Serialize(Apply(_chosen, EnteringTheGame()), _snapshot),
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void AChosenCharacterIsKnownByItsNameAndLevelOnly()
    {
        Assert.Equal("Character", _chosen.Name);
        Assert.Equal(200, _chosen.Level);
        Assert.Null(_chosen.MapId);
        Assert.Null(_chosen.Cell);
        Assert.Empty(_chosen.Inventory);
    }

    [Fact]
    public void TheCharacteristicsAreKeptWholeWhicheverWayTheServerGivesThem()
    {
        var stats = new CharacterStats
        {
            Characteristics =
            {
                new Characteristic { Id = 0, Detailed = new DetailedCharacteristic { Number5 = 1_000, Number7 = 55 } },
                new Characteristic { Id = 11, Detailed = new DetailedCharacteristic { Number1 = 200 } },
                new Characteristic { Id = 97, Detailed = new DetailedCharacteristic { Number5 = -300 } },
                new Characteristic { Id = 1, Usable = new UsableCharacteristic { Number4 = 6, Number2 = 1 } },
                new Characteristic { Id = 10, Detailed = new DetailedCharacteristic { Number1 = 300, Number3 = 120 } },
                new Characteristic { Id = 29, Value = new CharacteristicValue { Total = 4_000 } },
                new Characteristic { Id = 40 },
            },
        };

        var state = Apply(_chosen, Server(new CharacterStatsEvent { Stats = stats }));

        Assert.Equal(1_255, state.MaxLife);
        Assert.Equal(955, state.Life);
        Assert.Equal(new Dictionary<int, int> { [0] = 1_055, [1] = 7, [10] = 420, [11] = 200, [29] = 4_000, [97] = -300 }, state.Characteristics);
    }

    [Fact]
    public void AWalkEndsOnItsLastCellOnceTheServerAnsweredTheConfirmation()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var walking = Apply(standing, Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302 } }));

        Assert.True(walking.IsWalking);
        Assert.Equal(300, walking.Cell);

        var confirmed = Apply(walking, Client(new MapMovementConfirmRequest()));

        Assert.True(confirmed.IsWalking);

        var arrived = Apply(confirmed, Server(new MapMovementConfirmResponse()));

        Assert.False(arrived.IsWalking);
        Assert.Equal(302, arrived.Cell);
    }

    [Fact]
    public void AnotherActorsWalkLeavesTheCharacterWhereItIs()
    {
        var standing = Apply(_chosen, EnteringTheGame());

        Assert.Same(standing, Apply(standing, Server(new MapMovementEvent { ActorId = 42, Cells = { 300, 301 } })));
    }

    [Fact]
    public void ARefusedPathLeavesTheCharacterOnTheCellTheServerNames()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var point = MapGeometry.PointOf(388);
        var refused = Apply(standing, Server(new MapMovementRefusedEvent { X = point.X, Y = point.Y }));

        Assert.Equal(388, refused.Cell);
        Assert.False(refused.IsWalking);
    }

    [Fact]
    public void AWalkThePlayerStopsLeavesTheCharacterOnTheCellTheClientNames()
    {
        var walking = Apply(_chosen, [.. EnteringTheGame(), Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302, 303 } })]);
        var stopped = Apply(walking, Client(new MapMovementCancelRequest { CellId = 301 }));

        Assert.Equal(301, stopped.Cell);
        Assert.False(stopped.IsWalking);
    }

    [Fact]
    public void ANewMapForgetsTheCellUntilItsInformationPlacesTheCharacter()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var moved = Apply(standing, Server(new MapCurrentEvent { MapId = 88_212_481 }));

        Assert.Equal(88_212_481, moved.MapId);
        Assert.Null(moved.Cell);
        Assert.Null(moved.SubAreaId);

        // The information of another map than the current one places no one.
        Assert.Same(moved, Apply(moved, Server(TestPlay.Information(120))));
        Assert.Equal(120, Apply(moved, Server(TestPlay.Information(120, 88_212_481))).Cell);
    }

    [Fact]
    public void TheInventoryFollowsTheServer()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var changed = Apply(
            standing,
            Server(new ObjectAddedEvent { Object = new InventoryObject { Item = new ObjectItem { Uid = 50, ItemId = 1_844, Quantity = 14 }, Position = 63 } }),
            Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 12, Quantity = 54 } }),
            Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 999, Quantity = 1 } }),
            Server(new ObjectsDeletedEvent { Uids = { 30 } }),
            Server(new KamasUpdateEvent { Kamas = 11_000 }),
            Server(new InventoryWeightEvent { Weight = 812, MaxWeight = 7_590 }));

        Assert.Equal(
            [new InventoryItem(12, 303, 54, 63), new InventoryItem(50, 1_844, 14, 63)],
            changed.Inventory.Values);
        Assert.Equal(11_000, changed.Kamas);
        Assert.Equal(812, changed.Weight);
    }

    [Fact]
    public void AStackWornOrTakenOffMovesAndTheWeaponIsTheOneWornAtItsPosition()
    {
        var standing = Apply(_chosen, EnteringTheGame());

        Assert.Equal(30, standing.Weapon?.Uid);

        var takenOff = Apply(standing, Server(new ObjectMovementEvent { Uid = 30, Position = InventoryItem.NotWorn }), Server(new ObjectMovementEvent { Uid = 999, Position = 8 }));

        Assert.Null(takenOff.Weapon);
        Assert.False(takenOff.Inventory.ContainsKey(999));

        var worn = Apply(takenOff, Server(new ObjectMovementEvent { Uid = 12, Position = InventoryItem.Weapon }));

        Assert.Equal(new InventoryItem(12, 303, 40, InventoryItem.Weapon), worn.Inventory[12]);
        Assert.Equal(worn.Inventory[12], worn.Weapon);
    }

    [Fact]
    public void AProfessionsNewExperienceKeepsItsSkills()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var gained = Apply(standing, Server(new JobExperiencesEvent { Jobs = { new JobExperience { JobId = 2, Level = 112, Experience = 124_400 } } }));
        var job = gained.Jobs[2];

        Assert.Equal(112, job.Level);
        Assert.Equal([6, 39], job.Skills);
    }

    [Fact]
    public void TheMapsElementsFollowTheServerUntilANewMap()
    {
        var standing = Apply(_chosen, EnteringTheGame());

        Assert.True(standing.Elements[TestPlay.TreeElementId].TryGetEnabled(TestPlay.TreeSkillId, out var skill));
        Assert.Equal(new ElementSkill(TestPlay.TreeSkillId, TestPlay.TreeInstanceId), skill);

        // Another actor harvests the tree: its skill is disabled, its state 2, then 1.
        var harvested = Apply(
            standing,
            Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: false) }),
            Server(new StatedElementUpdatedEvent { Element = new StatedElement { ElementId = TestPlay.TreeElementId, CellId = TestPlay.TreeCell, State = 2 } }),
            Server(new StatedElementUpdatedEvent { Element = new StatedElement { ElementId = TestPlay.TreeElementId, CellId = TestPlay.TreeCell, State = 1 } }));
        var tree = harvested.Elements[TestPlay.TreeElementId];

        Assert.False(tree.TryGetEnabled(TestPlay.TreeSkillId, out _));
        Assert.Equal([new ElementSkill(TestPlay.TreeSkillId, TestPlay.TreeInstanceId)], tree.DisabledSkills);
        Assert.Equal(1, tree.State);
        Assert.Equal(TestPlay.TreeCell, tree.Cell);
        Assert.False(harvested.IsUsing);

        Assert.Empty(Apply(harvested, Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId })).Elements);
    }

    [Fact]
    public void TheNeighbouringMapsElementsAreLeftOut()
    {
        var standing = Apply(_chosen, EnteringTheGame());

        Assert.DoesNotContain(TestPlay.NeighboursElementId, standing.Elements.Keys);

        var updated = Apply(
            standing,
            Server(new InteractiveElementUpdatedEvent { Element = new InteractiveElement { ElementId = TestPlay.NeighboursElementId, InteractiveId = 34 } }),
            Server(new StatedElementUpdatedEvent { Element = new StatedElement { ElementId = TestPlay.NeighboursElementId, CellId = 435, State = 2 } }));

        Assert.Same(standing, updated);
    }

    [Fact]
    public void TheCharacterUsesAnElementFromTheServersStartToItsEnd()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var used = new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 };
        var harvesting = Apply(standing, Server(used));

        Assert.Equal(new CurrentUse(TestPlay.TreeElementId, TestPlay.TreeSkillId), harvesting.Use);
        Assert.True(harvesting.IsUsing);

        // Another element's end, then the use's own.
        Assert.True(Apply(harvesting, Server(new InteractiveUseEndedEvent { ElementId = 1, SkillId = TestPlay.TreeSkillId })).IsUsing);
        Assert.False(Apply(harvesting, Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId })).IsUsing);
    }

    [Fact]
    public void AnotherActorsUseOrADoorsLeavesTheCharacterFree()
    {
        var standing = Apply(_chosen, EnteringTheGame());

        Assert.False(Apply(standing, Server(new InteractiveUsedEvent { ActorId = 42, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 })).IsUsing);
        Assert.False(Apply(standing, Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.DoorElementId, SkillId = 184 })).IsUsing);
    }

    [Fact]
    public void TheMapsNpcsAreItsNonPlayerActors()
    {
        var inside = Apply(_chosen, Server(new MapCurrentEvent { MapId = TestPlay.InsideMapId }), Server(TestPlay.Information(396, TestPlay.InsideMapId)));

        Assert.Equal([new MapNpc(TestPlay.BankerActorId, TestPlay.BankerNpcId, 259), new MapNpc(-20_000, 1_088, 302)], inside.Npcs.Values);
        Assert.Empty(Apply(inside, Server(new MapCurrentEvent { MapId = TestPlay.MapId })).Npcs);
    }

    [Fact]
    public void TheMapsMonsterGroupsAreItsGroupActorsWithTheirMonstersTheMainOneFirst()
    {
        var information = TestPlay.Information(300);

        information.Actors.Add(GroupActor(-3, 412, new GroupMonster { MonsterId = 489, Grade = 2, Level = 24 }, new GroupMonster { MonsterId = 101, Grade = 1, Level = 18 }));
        information.Actors.Add(GroupActor(-5, 260, new GroupMonster { MonsterId = 98, Grade = 5, Level = 12 }));

        var standing = Apply(_chosen, Server(new MapCurrentEvent { MapId = TestPlay.MapId }), Server(information));

        Assert.Equal([-5L, -3L], standing.MonsterGroups.Keys);
        Assert.Equal((-3L, 412), (standing.MonsterGroups[-3].ActorId, standing.MonsterGroups[-3].Cell));
        Assert.Equal([new MonsterInGroup(489, 2, 24), new MonsterInGroup(101, 1, 18)], standing.MonsterGroups[-3].Monsters);
        Assert.Equal([new MonsterInGroup(98, 5, 12)], standing.MonsterGroups[-5].Monsters);
        Assert.Empty(standing.Npcs);

        Assert.Empty(Apply(standing, Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId })).MonsterGroups);
    }

    [Fact]
    public void AGroupShownComesOntoTheMapOrIsShownAgainAsItIs()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var came = Apply(standing, Server(new MapActorShownEvent { Actors = { GroupActor(-2, 330, new GroupMonster { MonsterId = 489, Grade = 3, Level = 26 }) } }));

        Assert.Equal(330, came.MonsterGroups[-2].Cell);
        Assert.Equal([new MonsterInGroup(489, 3, 26)], came.MonsterGroups[-2].Monsters);

        // The server shows a group again as it is attacked, just before removing it.
        var again = Apply(came, Server(new MapActorShownEvent { Actors = { GroupActor(-2, 330, new GroupMonster { MonsterId = 489, Grade = 3, Level = 26 }) } }));

        Assert.Equal([-2L], again.MonsterGroups.Keys);
        Assert.Equal([new MonsterInGroup(489, 3, 26)], again.MonsterGroups[-2].Monsters);

        // Another player, or the character itself, shown on the map.
        Assert.Same(came, Apply(came, Server(new MapActorShownEvent { Actors = { new Actor { Id = 70_002, Disposition = new ActorDisposition { CellId = 120 } } } })));
    }

    [Fact]
    public void AGroupRemovedLeavesTheMap()
    {
        var information = TestPlay.Information(300);

        information.Actors.Add(GroupActor(-3, 412, new GroupMonster { MonsterId = 489, Grade = 2, Level = 24 }));
        information.Actors.Add(GroupActor(-5, 260, new GroupMonster { MonsterId = 98, Grade = 5, Level = 12 }));

        var standing = Apply(_chosen, Server(new MapCurrentEvent { MapId = TestPlay.MapId }), Server(information));

        Assert.Equal([-5L], Apply(standing, Server(new MapActorRemovedEvent { ActorId = -3 })).MonsterGroups.Keys);

        // Another player leaves.
        Assert.Same(standing, Apply(standing, Server(new MapActorRemovedEvent { ActorId = 70_002 })));
    }

    [Fact]
    public void AGroupsWalkPutsItOnItsLastCell()
    {
        var information = TestPlay.Information(300);

        information.Actors.Add(GroupActor(-3, 412, new GroupMonster { MonsterId = 489, Grade = 2, Level = 24 }));

        var standing = Apply(_chosen, Server(new MapCurrentEvent { MapId = TestPlay.MapId }), Server(information));

        // The server names a group's first and last cells only.
        var walked = Apply(standing, Server(new MapMovementEvent { ActorId = -3, Cells = { 412, 384 } }));

        Assert.Equal(384, walked.MonsterGroups[-3].Cell);
        Assert.Equal(300, walked.Cell);
        Assert.False(walked.IsWalking);

        Assert.Same(standing, Apply(standing, Server(new MapMovementEvent { ActorId = -3, Cells = { 412, 560 } })));
        Assert.Same(standing, Apply(standing, Server(new MapMovementEvent { ActorId = -4, Cells = { 412, 384 } })));
    }

    [Fact]
    public void TheBankersDialogThenTheBanksExchangeFollowTheServerUntilLeft()
    {
        var inside = Apply(_chosen, Server(new MapCurrentEvent { MapId = TestPlay.InsideMapId }), Server(TestPlay.Information(396, TestPlay.InsideMapId)));
        var asked = Apply(
            inside,
            Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId }),
            Server(new NpcDialogQuestionEvent { MessageId = 48_366, Replies = { new NpcReply { ReplyId = TestPlay.ChestReplyId }, new NpcReply { ReplyId = TestPlay.OtherReplyId } } }));

        Assert.Equal(TestPlay.BankerActorId, asked.Dialog?.NpcActorId);
        Assert.Equal(48_366, asked.Dialog?.MessageId);
        Assert.Equal([TestPlay.ChestReplyId, TestPlay.OtherReplyId], asked.Dialog?.Replies);
        Assert.True(asked.IsInDialog);

        // The reply ends the dialog and opens the bank in the same millisecond.
        var open = Apply(
            asked,
            Server(new DialogLeaveEvent { Type = DialogType.Npc }),
            Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage, MaxSlots = int.MaxValue }),
            Server(new StorageContentEvent { Kamas = 5 }));

        Assert.Null(open.Dialog);
        Assert.Equal(new OpenStorage(5), open.Storage);
        Assert.Equal(new OpenStorage(6), Apply(open, Server(new StorageKamasUpdateEvent { Kamas = 6 })).Storage);

        // The client's request to leave changes nothing: the server's end does.
        Assert.Same(open, Apply(open, Client(new DialogLeaveRequest())));
        Assert.False(Apply(open, Server(new ExchangeLeaveEvent { Type = DialogType.Storage })).IsInDialog);
        Assert.False(Apply(asked, Server(new MapCurrentEvent { MapId = TestPlay.MapId })).IsInDialog);
    }

    [Fact]
    public void AnotherExchangeOrAnotherMapsDialogIsNotTheBanks()
    {
        var inside = Apply(_chosen, Server(new MapCurrentEvent { MapId = TestPlay.InsideMapId }), Server(TestPlay.Information(396, TestPlay.InsideMapId)));

        Assert.False(Apply(inside, Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Value1 })).IsInDialog);
        Assert.False(Apply(inside, Server(new NpcDialogCreationEvent { MapId = TestPlay.MapId, NpcActorId = -1 })).IsInDialog);
        Assert.Null(Apply(inside, Server(new NpcDialogQuestionEvent { MessageId = 1 })).Dialog);
    }

    [Fact]
    public void AMessageFromTheOtherSideChangesNothing()
    {
        var standing = Apply(_chosen, EnteringTheGame());

        Assert.Same(standing, Apply(standing, Server(new MapMovementCancelRequest { CellId = 10 })));
        Assert.Same(standing, Apply(standing, Client(new MapCurrentEvent { MapId = 1 })));
    }

    [Fact]
    public void ACellOffTheMapIsNotTaken()
    {
        var standing = Apply(_chosen, EnteringTheGame());

        Assert.Same(standing, Apply(standing, Client(new MapMovementCancelRequest { CellId = 560 })));
        Assert.Same(standing, Apply(standing, Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 900 } })));
    }

    [Fact]
    public void TheZaapsTheCharacterKnowsAreTheServersLastList()
    {
        var state = Apply(_chosen, [Server(new ZaapKnownListEvent { MapIds = { 1, 2 } }), Server(new ZaapKnownListEvent { MapIds = { 3, 2 } })]);

        Assert.Equal([2, 3], state.KnownZaaps);
    }

    [Fact]
    public void AZaapsDialogListsWhatEachZaapCostsUntilItEnds()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var listed = Apply(standing, Server(new TeleportDestinationsEvent
        {
            Destinations =
            {
                new TeleportDestination { MapId = 120_062_979, Cost = 140, SubAreaId = 30 },
                new TeleportDestination { MapId = 191_105_026, SubAreaId = 95 },
                new TeleportDestination { Type = TeleporterType.Value4, MapId = 120_062_979, Cost = 180 },
            },
        }));

        Assert.True(listed.IsInDialog);
        Assert.Equal(TeleporterType.Zaap, listed.ZaapDialog!.Type);
        Assert.Equal(new KeyValuePair<long, int>[] { new(120_062_979, 140), new(191_105_026, 0) }, listed.ZaapDialog.Costs);
        Assert.False(Apply(listed, Server(new DialogLeaveEvent { Type = DialogType.Teleport })).IsInDialog);
        Assert.False(Apply(listed, Server(new MapCurrentEvent { MapId = 120_062_979 })).IsInDialog);
    }

    [Fact]
    public void TheMapsInformationSaysWhoseHavenBagTheCharacterIsIn()
    {
        var standing = Apply(_chosen, EnteringTheGame());
        var information = TestPlay.Information(324, TestPlay.HavenBagMapId);

        information.HavenBag = new HavenBagInformation { Owner = new Character { Id = TestPlay.CharacterId } };

        var inside = Apply(standing, [Server(new MapCurrentEvent { MapId = TestPlay.HavenBagMapId }), Server(information)]);

        Assert.False(standing.IsInHavenBag);
        Assert.Equal(TestPlay.CharacterId, inside.HavenBagOwnerId);
        Assert.False(Apply(inside, Server(new MapCurrentEvent { MapId = TestPlay.MapId })).IsInHavenBag);
    }

    [Fact]
    public void AWalkTheClientDidNotConfirmLeavesTheCharacterOnItsLastCell()
    {
        var walking = Apply(_chosen, [.. EnteringTheGame(), Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302 } })]);
        var settled = CharacterEvents.NotConfirmed(walking);

        Assert.False(settled.IsWalking);
        Assert.Equal(302, settled.Cell);
        Assert.Same(settled, CharacterEvents.NotConfirmed(settled));
    }

    [Fact]
    public void TheServerHoldsAWalkTheClientDidNotConfirmUntilTheNextWalkRefusalOrMap()
    {
        var walking = Apply(_chosen, [.. EnteringTheGame(), Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 300, 301, 302 } })]);
        var held = CharacterEvents.NotConfirmed(walking);

        Assert.True(held.IsWalkHeld);
        Assert.False(held.IsBusy);
        Assert.False(Apply(held, Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { 302, 303 } })).IsWalkHeld);
        Assert.False(Apply(held, Server(new MapMovementRefusedEvent { X = 18, Y = -3 })).IsWalkHeld);
        Assert.False(Apply(held, Server(new MapCurrentEvent { MapId = TestPlay.NorthMapId })).IsWalkHeld);
        Assert.False(Apply(held, Server(new MapMovementConfirmResponse())).IsWalkHeld);
        Assert.True(Apply(held, Server(new MapMovementEvent { ActorId = 9, Cells = { 100, 101 } })).IsWalkHeld);
    }

    [Fact]
    public void AFightLastsFromItsContextToTheRoleplaysAndPlacesTheCharacterNowhere()
    {
        var entered = Apply(_chosen, EnteringTheGame());
        var fighting = Apply(
            entered,
            Server(new ContextDestroyEvent()),
            Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight }),
            Server(new MapCurrentEvent { MapId = 191_104_008 }),
            Client(new MapInformationRequest { MapId = 191_104_008 }));

        Assert.True(fighting.IsFighting);
        Assert.True(fighting.IsBusy);
        Assert.Equal(191_104_008, fighting.MapId);
        Assert.Null(fighting.Cell);

        var over = Apply(
            fighting,
            Server(new ContextDestroyEvent()),
            Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay }),
            Server(new MapCurrentEvent { MapId = TestPlay.MapId }),
            Server(TestPlay.Information(358)));

        Assert.False(over.IsFighting);
        Assert.False(over.IsBusy);
        Assert.Equal(358, over.Cell);
    }

    [Fact]
    public void TheLoadIsTheWeightOverTheMostTheCharacterCarries()
    {
        Assert.Equal(0, _chosen.Load);
        Assert.Equal(0.5, Apply(_chosen, Server(new InventoryWeightEvent { Weight = 3_795, MaxWeight = 7_590 })).Load);
    }
}
