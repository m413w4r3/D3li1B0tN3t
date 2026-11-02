using System.Collections.Immutable;
using D3B.Bot.Fights;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Characters;

/// <summary>Turns what the game connection carries into the character's next state: the bot's state handlers (D9).</summary>
/// <remarks>
/// <para>
/// Each message a side sent gives a new snapshot, or the same one when it says nothing of the character. Pure functions,
/// so they are tested on their own, and a bot's loop is the only caller that keeps their result.
/// </para>
/// <para>
/// A walk, measured (client.md): the server shows the character's path, the client confirms once it arrived, and the
/// server answers; the character then stands on the path's last cell. The server refuses a path that does not start where
/// the character stands, naming its cell. The client stops a walk when the player gives a new path, naming the cell it
/// stopped on.
/// </para>
/// <para>
/// A use, measured (client.md): the server starts it for every actor on the map, disabling the element's skill and
/// stating the element harvested; a harvest ends its duration later, with the objects, the weight and the experience
/// within 2 ms, in no set order. A door's use has no duration: the server names the new map at once. The map's
/// information also lists the elements of the neighbouring maps, marked so, whose updates follow: the state keeps the
/// map's own.
/// </para>
/// <para>
/// The bank, measured (client.md): the server opens the NPC's dialog, then sends its question and the replies it offers.
/// The banker's reply ends the dialog and opens the exchange with the character's storage, with its kamas, in the same
/// millisecond; leaving ends the exchange. A dialog and an exchange end with the map too.
/// </para>
/// <para>
/// Zaaps, measured (client.md): the server lists the zaaps the character knows on entering the game, and again, whole,
/// when it enters the map of a zaap it did not know. A zaap used lists its destinations with their costs, a dialog that
/// leaving or travelling ends. The map's information says when the map is a haven bag, and whose.
/// </para>
/// <para>
/// Groups of monsters, measured on the 80 game connections captured: the map's information lists them, the server shows
/// a group coming onto the map, gives a group's walk as its first and last cells only, and removes a group attacked, by
/// the character or another player, after showing it again as it was.
/// </para>
/// <para>
/// A fight, measured (client.md): the server replaces the roleplay's context with a fight's, then names the fight's own
/// map; once the fight is over, it creates the roleplay's context again and names the map the fight began on (D88). In
/// between, <see cref="FightEvents"/> takes the fight's messages first (D179).
/// </para>
/// </remarks>
public static class CharacterEvents
{
    private const int MostLife = 0;
    private const int Vitality = 11;
    private const int LifeLacking = 97;

    /// <summary>Gives the character's state once a side sent a message.</summary>
    /// <param name="state">The state before the message.</param>
    /// <param name="side">The side that sent it: the client's requests, the server's events and responses.</param>
    /// <param name="message">The message, readable.</param>
    /// <returns>The state after it.</returns>
    public static CharacterState Apply(CharacterState state, RelayDirection side, IMessage message)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(message);

        return side is RelayDirection.FromClient ? FromClient(state, message) : FromServer(state, message);
    }

    /// <summary>Gives the character's state once the client did not confirm a walk the server showed it.</summary>
    /// <param name="state">The state, the character walking.</param>
    /// <returns>The state with the character on the walk's last cell, where the server has it and may still hold it walking, or the same state when it no longer walks.</returns>
    /// <remarks>
    /// Measured (client.md): the server walked the path, and its next refusal names the path's last cell; a walk sent from
    /// that cell is followed by the client, which first brings its character there (D85).
    /// </remarks>
    public static CharacterState NotConfirmed(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.IsWalking ? state with { Cell = state.Path[^1], Path = [], IsWalkHeld = true } : state;
    }

    private static CharacterState FromClient(CharacterState state, IMessage message)
    {
        return message switch
        {
            MapMovementCancelRequest cancel when IsCell(cancel.CellId) => state with { Cell = cancel.CellId, Path = [] },
            _ => state,
        };
    }

    private static CharacterState FromServer(CharacterState state, IMessage message)
    {
        var self = state.Key.CharacterId;

        // In a fight the fight's state takes the fighters' moves, the character's among them, which no confirmation ends.
        if (state.Fight is { } fight && FightEvents.TryApply(fight, self, message, out var fought))
            return ReferenceEquals(fought, fight) ? state : state with { Fight = fought };

        return message switch
        {
            MapCurrentEvent current => state with
            {
                MapId = current.MapId,
                SubAreaId = null,
                Cell = null,
                Path = [],
                Elements = ImmutableSortedDictionary<int, MapElement>.Empty,
                Use = null,
                Npcs = ImmutableSortedDictionary<int, MapNpc>.Empty,
                MonsterGroups = ImmutableSortedDictionary<long, MapMonsterGroup>.Empty,
                Dialog = null,
                Storage = null,
                ZaapDialog = null,
                HavenBagOwnerId = null,
                IsWalkHeld = false,
            },
            ContextCreationEvent created => state with { Fight = created.Context is ContextCreationEvent.Types.GameContext.Fight ? new FightState() : null },
            MapInformationEvent information when information.MapId == state.MapId => Placed(state, information),
            InteractiveElementUpdatedEvent { Element: { } element } when state.Elements.ContainsKey(element.ElementId) => state with
            {
                Elements = state.Elements.SetItem(element.ElementId, Updated(state.Elements, element)),
            },
            StatedElementUpdatedEvent { Element: { } stated } when state.Elements.ContainsKey(stated.ElementId) => state with
            {
                Elements = state.Elements.SetItem(stated.ElementId, Stated(state.Elements, stated)),
            },
            InteractiveUsedEvent used when used.ActorId == self && used.Duration > 0 => state with { Use = new CurrentUse(used.ElementId, used.SkillId) },
            InteractiveUseEndedEvent ended when state.Use == new CurrentUse(ended.ElementId, ended.SkillId) => state with { Use = null },
            MapMovementEvent movement when movement.ActorId == self && movement.Cells.Count > 0 && movement.Cells.All(IsCell) => state with { Path = [.. movement.Cells], IsWalkHeld = false, LastWalkEnd = movement.Cells[^1] },
            // A group's walk names only its first and last cells, and the client attacks it where it stops.
            MapMovementEvent movement when movement.Cells.Count > 0 && IsCell(movement.Cells[^1]) && state.MonsterGroups.TryGetValue(movement.ActorId, out var walker) => state with
            {
                MonsterGroups = state.MonsterGroups.SetItem(walker.ActorId, walker with { Cell = movement.Cells[^1] }),
            },
            MapActorShownEvent shown when shown.Actors.Any(IsMonsterGroup) => state with
            {
                MonsterGroups = state.MonsterGroups.SetItems(shown.Actors.Where(IsMonsterGroup).Select(actor => KeyValuePair.Create(actor.Id, Group(actor)))),
            },
            MapActorRemovedEvent removed when state.MonsterGroups.ContainsKey(removed.ActorId) => state with { MonsterGroups = state.MonsterGroups.Remove(removed.ActorId) },
            MapMovementConfirmResponse when state.IsWalking => state with { Cell = state.Path[^1], Path = [] },
            MapMovementConfirmResponse => state with { IsWalkHeld = false },
            MapMovementRefusedEvent refused => state with
            {
                Cell = MapGeometry.TryGetCell(new MapPoint(refused.X, refused.Y), out var cell) ? cell : state.Cell,
                Path = [],
                IsWalkHeld = false,
            },
            NpcDialogCreationEvent creation when creation.MapId == state.MapId => state with { Dialog = new NpcDialog(creation.NpcActorId) },
            NpcDialogQuestionEvent question when state.Dialog is { } dialog => state with
            {
                Dialog = dialog with { MessageId = question.MessageId, Replies = [.. question.Replies.Select(reply => reply.ReplyId)] },
            },
            DialogLeaveEvent => state with { Dialog = null, ZaapDialog = null },
            ZaapKnownListEvent known => state with { KnownZaaps = [.. known.MapIds] },
            TeleportDestinationsEvent destinations => state with { ZaapDialog = new ZaapDialog(destinations.Type, Costs(destinations)) },
            ExchangeStartedWithStorageEvent { Type: ExchangeType.Storage } => state with { Dialog = null, Storage = new OpenStorage(0) },
            ExchangeStartedWithTabbedStorageEvent { Type: ExchangeType.GuildChest } => state with { Dialog = null, Storage = new OpenStorage(0, IsGuildChest: true) },
            StorageContentEvent content when state.Storage is { } storage => state with { Storage = storage with { Kamas = content.Kamas } },
            StorageKamasUpdateEvent kamas when state.Storage is { } storage => state with { Storage = storage with { Kamas = kamas.Kamas } },
            ExchangeLeaveEvent => state with { Storage = null },
            InventoryContentEvent content => state with { Inventory = Stacks(content.Objects), Kamas = content.Kamas },
            ObjectAddedEvent { Object: { Item: not null } added } => state with { Inventory = state.Inventory.SetItem(added.Item.Uid, Stack(added)) },
            ObjectQuantityEvent { Object: { } changed } when state.Inventory.TryGetValue(changed.Uid, out var stack) => state with
            {
                Inventory = state.Inventory.SetItem(changed.Uid, stack with { Quantity = changed.Quantity }),
            },
            ObjectsDeletedEvent deleted => state with { Inventory = state.Inventory.RemoveRange(deleted.Uids) },
            ObjectDeletedEvent deleted => state with { Inventory = state.Inventory.Remove(deleted.Uid) },
            ObjectMovementEvent moved when state.Inventory.TryGetValue(moved.Uid, out var stack) => state with
            {
                Inventory = state.Inventory.SetItem(moved.Uid, stack with { Position = moved.Position }),
            },
            CharacterStatsEvent { Stats: { } stats } => Lived(state, stats),
            LifeRegenEndEvent ended => state with { Life = ended.Life, MaxLife = ended.MaxLife, LifeRegenInterval = null },
            LifeRegenStartEvent started => state with { LifeRegenInterval = started.Interval > 0 ? started.Interval : null },
            KamasUpdateEvent kamas => state with { Kamas = kamas.Kamas },
            InventoryWeightEvent weight => state with { Weight = weight.Weight, MaxWeight = weight.MaxWeight },
            JobDescriptionEvent description => state with { Jobs = Described(state.Jobs, description) },
            JobExperiencesEvent experiences => state with { Jobs = Experienced(state.Jobs, experiences) },
            SpellsEvent spells => state with
            {
                Spells = spells.Spells.GroupBy(spell => spell.SpellId).ToImmutableSortedDictionary(group => group.Key, group => group.Last().Grade),
                TransformedSpells = spells.TransformedSpells.GroupBy(spell => spell.SpellId).ToImmutableSortedDictionary(group => group.Key, group => group.Last().Grade),
            },
            _ => state,
        };
    }

    private static CharacterState Placed(CharacterState state, MapInformationEvent information)
    {
        var self = information.Actors.FirstOrDefault(actor => actor.Id == state.Key.CharacterId);
        var cell = self?.Disposition is { HasCellId: true } disposition && IsCell(disposition.CellId) ? disposition.CellId : state.Cell;

        // The information also lists the neighbouring maps' elements, which cannot be used from here; their updates follow,
        // which only a known element takes.
        var elements = information.InteractiveElements.Where(element => element.OnCurrentMap).Aggregate(
            ImmutableSortedDictionary<int, MapElement>.Empty,
            (elements, element) => elements.SetItem(element.ElementId, Updated(elements, element)));

        elements = information.StatedElements.Where(stated => stated.OnCurrentMap)
            .Aggregate(elements, (elements, stated) => elements.SetItem(stated.ElementId, Stated(elements, stated)));

        var npcs = information.Actors.Where(actor => actor.Id is < 0 and >= int.MinValue && actor.Information?.Roleplay?.Npc is not null)
            .Select(actor => new MapNpc((int)actor.Id, actor.Information.Roleplay.Npc.NpcId, actor.Disposition is { HasCellId: true } at && IsCell(at.CellId) ? at.CellId : null))
            .ToImmutableSortedDictionary(npc => npc.ActorId, npc => npc);

        var groups = information.Actors.Where(IsMonsterGroup).Aggregate(
            ImmutableSortedDictionary<long, MapMonsterGroup>.Empty,
            (groups, actor) => groups.SetItem(actor.Id, Group(actor)));

        var owner = information.PlaceCase is MapInformationEvent.PlaceOneofCase.HavenBag ? information.HavenBag.Owner?.Id : null;

        return state with { SubAreaId = information.SubAreaId, Cell = cell, Elements = elements, Npcs = npcs, MonsterGroups = groups, HavenBagOwnerId = owner };
    }

    private static bool IsMonsterGroup(Actor actor)
    {
        return actor.Id < 0 && actor.Information?.Roleplay?.MonsterGroup is not null;
    }

    private static MapMonsterGroup Group(Actor actor)
    {
        var monsters = actor.Information.Roleplay.MonsterGroup.Monsters;
        var cell = actor.Disposition is { HasCellId: true } at && IsCell(at.CellId) ? at.CellId : (int?)null;
        var members = ImmutableArray.CreateBuilder<MonsterInGroup>();

        // The fight against a group had its main monster and its others, none more, in each of the 97 fights measured.
        if (monsters?.Main is { } main)
            members.Add(Member(main));

        if (monsters is not null)
            members.AddRange(monsters.Others.Select(Member));

        return new MapMonsterGroup(actor.Id, cell, members.ToImmutable());
    }

    private static MonsterInGroup Member(GroupMonster monster)
    {
        return new MonsterInGroup(monster.MonsterId, monster.Grade, monster.Level);
    }

    private static CharacterState Lived(CharacterState state, CharacterStats stats)
    {
        var values = CharacteristicTotals.Of(stats.Characteristics);

        // Measured (D153): the most life is characteristic 0 plus 11, the vitality; the life, the most plus 97, the life lacking.
        if (!values.TryGetValue(MostLife, out var most))
            return state;

        var max = most + values.GetValueOrDefault(Vitality);

        return state with { Characteristics = values, MaxLife = max, Life = max + values.GetValueOrDefault(LifeLacking) };
    }

    private static ImmutableSortedDictionary<long, int> Costs(TeleportDestinationsEvent destinations)
    {
        // The list also holds destinations of other types, on maps a zaap stands on too.
        return destinations.Destinations.Where(destination => destination.Type is TeleporterType.Zaap)
            .Aggregate(ImmutableSortedDictionary<long, int>.Empty, (costs, destination) => costs.SetItem(destination.MapId, destination.Cost));
    }

    private static MapElement Updated(ImmutableSortedDictionary<int, MapElement> elements, InteractiveElement element)
    {
        var enabled = element.EnabledSkills.Select(Skill).ToImmutableArray();
        var disabled = element.DisabledSkills.Select(Skill).ToImmutableArray();

        return elements.TryGetValue(element.ElementId, out var known)
            ? known with { InteractiveId = element.InteractiveId, EnabledSkills = enabled, DisabledSkills = disabled }
            : new MapElement(element.ElementId, element.InteractiveId, enabled, disabled);
    }

    private static MapElement Stated(ImmutableSortedDictionary<int, MapElement> elements, StatedElement stated)
    {
        var known = elements.TryGetValue(stated.ElementId, out var element) ? element : new MapElement(stated.ElementId, -1, [], []);

        return known with { Cell = IsCell(stated.CellId) ? stated.CellId : null, State = stated.State };
    }

    private static ElementSkill Skill(InteractiveSkill skill)
    {
        return new ElementSkill(skill.SkillId, skill.InstanceId);
    }

    private static ImmutableSortedDictionary<int, InventoryItem> Stacks(IEnumerable<InventoryObject> objects)
    {
        return objects.Where(item => item.Item is not null).Select(Stack).ToImmutableSortedDictionary(stack => stack.Uid, stack => stack);
    }

    private static InventoryItem Stack(InventoryObject item)
    {
        return new InventoryItem(item.Item.Uid, item.Item.ItemId, item.Item.Quantity, item.Position);
    }

    private static ImmutableSortedDictionary<int, CharacterJob> Described(ImmutableSortedDictionary<int, CharacterJob> jobs, JobDescriptionEvent description)
    {
        foreach (var job in description.Jobs)
        {
            var skills = job.Skills.Select(skill => skill.SkillId).ToImmutableArray();

            jobs = jobs.SetItem(job.JobId, jobs.TryGetValue(job.JobId, out var known) ? known with { Skills = skills } : new CharacterJob(job.JobId, 0, 0, 0, 0, skills));
        }

        return jobs;
    }

    private static ImmutableSortedDictionary<int, CharacterJob> Experienced(ImmutableSortedDictionary<int, CharacterJob> jobs, JobExperiencesEvent experiences)
    {
        // The server sends every profession on entering the game, then only the one whose experience changed.
        foreach (var job in experiences.Jobs)
        {
            var skills = jobs.TryGetValue(job.JobId, out var known) ? known.Skills : [];

            jobs = jobs.SetItem(job.JobId, new CharacterJob(job.JobId, job.Level, job.Experience, job.LevelFloor, job.NextLevelFloor, skills));
        }

        return jobs;
    }

    private static bool IsCell(int cell)
    {
        return cell is >= 0 and < MapData.CellCount;
    }
}
