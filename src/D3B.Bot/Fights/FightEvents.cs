using System.Collections.Immutable;
using D3B.Bot.Characters;
using D3B.Data.Maps;
using D3B.Engine.Fights;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Fights;

/// <summary>Turns what the server sends during a fight into the fight's next state: pure functions, as the character's (D9).</summary>
/// <remarks>
/// <para>
/// The characteristics, measured (client.md): a fighter's AP and MP left are characteristics 1 and 23, which the server
/// gives again after each cast and step, the character's and each fighter's as its turn begins. The life is 0 plus 11, the
/// vitality, plus 97, the life lacking, as out of fights; a monster's and a summon's carried 0 alone. The server announces
/// each loss and heal apart, and gives the life no more. The AP and MP an action spends are announced too, before or after the characteristics: the state takes
/// the characteristics alone, so that an action's spending is never counted twice.
/// </para>
/// <para>
/// Moves, measured: a fighter walks as out of fights, with no confirmation from its client, teleports onto a cell, or swaps
/// with a target; during the placement, the server gives every fighter's disposition after each move.
/// </para>
/// </remarks>
public static class FightEvents
{
    private const int MostLife = 0;
    private const int ActionPoints = 1;
    private const int Vitality = 11;
    private const int MovementPoints = 23;
    private const int LifeLacking = 97;

    /// <summary>Gives the fight's state once the server sent a message, when the message is the fight's.</summary>
    /// <param name="fight">The fight's state before the message.</param>
    /// <param name="self">The character's id.</param>
    /// <param name="message">The message, readable.</param>
    /// <param name="next">The fight's state after it, or the same state when the message is not the fight's.</param>
    /// <returns>Whether the message is the fight's: a message of the roleplay, such as the map's or the inventory's, is not.</returns>
    public static bool TryApply(FightState fight, long self, IMessage message, out FightState next)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(message);

        FightState? applied = message switch
        {
            FightPlacementPossiblePositionsEvent { Cells: { } cells } positions => fight with
            {
                Team = TeamOf(fight, self, positions),
                ChallengerCells = [.. cells.ChallengerCells.Where(IsCell)],
                DefenderCells = [.. cells.DefenderCells.Where(IsCell)],
            },
            FightPlacementPossiblePositionsEvent positions => fight with { Team = TeamOf(fight, self, positions) },
            EntitiesDispositionEvent dispositions => dispositions.Dispositions
                .Where(disposition => disposition is { HasEntityId: true, HasCellId: true } && IsCell(disposition.CellId))
                .Aggregate(fight, (state, disposition) => Moved(state, disposition.EntityId, disposition.CellId)),
            FightHumanReadyStateEvent ready when ready.CharacterId == self => fight with { IsReady = ready.Ready },
            FightHumanReadyStateEvent => fight,
            FightFighterShowEvent { Fighter: { } shown } => Shown(fight, self, shown),
            FightFighterShowEvent => fight,
            FightSynchronizeEvent synchronized => Synchronized(fight, self, synchronized),
            FightStartEvent => fight with { Phase = FightPhase.Fighting, IsReady = false },
            FightTurnListEvent order => Ordered(fight, order),
            FightNewRoundEvent round => fight with { Round = round.Round },
            FightTurnEvent turn => Turned(fight, turn),
            FightTurnStartPlayingEvent => fight with { Phase = FightPhase.Fighting, IsOwnTurn = Controls(fight, self) },
            FightTurnEndEvent ended when ended.FighterId == fight.PlayingId => fight with { PlayingId = null, IsOwnTurn = false },
            FightTurnEndEvent => fight,
            FightRefreshCharacterStatsEvent { Stats: { } stats } refreshed => Changed(fight, refreshed.FighterId, fighter => Spent(fighter, stats)),
            FightRefreshCharacterStatsEvent => fight,
            MapMovementEvent walked when walked.Cells.Count > 0 && IsCell(walked.Cells[^1]) => Moved(fight, walked.ActorId, walked.Cells[^1]),
            MapMovementEvent => fight,
            GameActionFightEvent action => Acted(fight, action),
            FightEffectsEvent { Given.Buff: { } buff } given => Changed(fight, buff.TargetId, fighter => Buffed(fighter, given.Given, buff)),
            FightEffectsEvent => fight,

            // Measured (D224): the state of a Zobal's mask goes so as another is put on, which the server then checks.
            FightBuffEndEvent ended => Changed(fight, ended.TargetId, fighter => fighter with { Buffs = [.. fighter.Buffs.Where(buff => buff.Uid != ended.Uid)] }),
            RefreshCooldownEvent cooldowns => Changed(fight, cooldowns.FighterId, fighter => fighter with
            {
                Cooldowns = cooldowns.Cooldowns.GroupBy(cooldown => cooldown.SpellId).ToImmutableSortedDictionary(group => group.Key, group => group.Last().Rounds),
            }),
            SequenceStartEvent started => fight with { Sequences = fight.Sequences.Add(new FightSequence(started.Type, started.AuthorId, HasCast: false)) },
            SequenceEndEvent => fight.Sequences.IsEmpty ? fight : fight with { Sequences = fight.Sequences.RemoveAt(fight.Sequences.Length - 1) },
            FightEndEvent end => fight with { Phase = FightPhase.Ended, PlayingId = null, IsOwnTurn = false, End = Ended(end, self) },
            // Measured (9f4): a summon the client controls comes with its own list, sent to the character, the summon named
            // apart; taken as the character's, it left a Roublard with its Roublabot's spells for the rest of its fight.
            FightSpellsContextEvent context when context.FighterId == self && context.Spells.Count > 0 => fight with
            {
                Spells = context.Spells.GroupBy(spell => spell.SpellId).ToImmutableSortedDictionary(group => group.Key, group => group.Last().Grade),
            },
            FightSpellsContextEvent => fight,
            FightOptionUpdateEvent update => fight with { Options = fight.Options.SetItem(update.Option, update.Enabled) },
            FightStartingEvent or FightJoinEvent => fight,
            _ => null,
        };

        next = applied ?? fight;

        return applied is not null;
    }

    private static FightTeam TeamOf(FightState fight, long self, FightPlacementPossiblePositionsEvent positions)
    {
        // The event's team was unset in every fight captured, its meaning not measured: the character's own fighter says it.
        return fight.Fighters.TryGetValue(self, out var own) ? own.Team : positions.Team;
    }

    private static FightState Synchronized(FightState fight, long self, FightSynchronizeEvent synchronized)
    {
        // Every fighter there is: those it leaves out are gone, those it lists keep what the actions told of them.
        var listed = synchronized.Fighters.Select(actor => actor.Id).ToHashSet();
        var kept = fight with { Fighters = fight.Fighters.RemoveRange(fight.Fighters.Keys.Where(id => !listed.Contains(id))) };

        return synchronized.Fighters.Aggregate(kept, (state, actor) => Shown(state, self, actor));
    }

    private static FightState Shown(FightState fight, long self, Actor actor)
    {
        var known = fight.Fighters.GetValueOrDefault(actor.Id) ?? new Fighter { Id = actor.Id };
        var cell = actor.Disposition is { HasCellId: true } at && IsCell(at.CellId) ? at.CellId : known.Cell;
        var shown = actor.Information?.Fighter;

        if (shown is null)
            return fight with { Fighters = fight.Fighters.SetItem(actor.Id, known with { Cell = cell }) };

        var fighter = Lived(known with
        {
            Cell = cell,
            Team = shown.Spawn?.Team ?? known.Team,
            IsAlive = shown.Spawn?.Alive ?? known.IsAlive,
            Level = shown.Named?.Character?.Level ?? shown.Ai?.Monster?.Level ?? known.Level,
            BreedId = shown.Named?.Character?.BreedId ?? known.BreedId,
            MonsterId = shown.Ai?.Monster?.MonsterId ?? known.MonsterId,
            Grade = shown.Ai?.Monster?.Grade ?? known.Grade,
            SummonerId = shown.Stats is { Summoned: true } summoned ? summoned.SummonerId : known.SummonerId,
        }, shown.Stats);

        return fight with
        {
            Fighters = fight.Fighters.SetItem(actor.Id, fighter),
            Team = actor.Id == self && shown.Spawn is { } spawn ? spawn.Team : fight.Team,
        };
    }

    private static bool Controls(FightState fight, long self)
    {
        // Measured (9f2): the server lets the client play a summon of the character's it controls, as it lets the character
        // play, and takes the same requests for it.
        return fight.PlayingId is { } playing && (playing == self || fight.Fighters.GetValueOrDefault(playing)?.SummonerId == self);
    }

    private static FightState Ordered(FightState fight, FightTurnListEvent order)
    {
        var entries = order.Entries.Where(entry => entry.Fighter is not null).Select(entry => entry.Fighter).ToList();
        var dead = entries.Where(entry => entry.Dead).Aggregate(fight, (state, entry) => Changed(state, entry.FighterId, Died));

        return dead with { TurnOrder = [.. entries.Select(entry => entry.FighterId)] };
    }

    private static FightState Turned(FightState fight, FightTurnEvent turn)
    {
        // Measured live (D203): a client joining a fight under way, its connection come back, gets no fight's start: a turn says it.
        var round = turn.Round > 0 ? turn.Round : fight.Round;
        var place = fight.TurnOrder.IndexOf(turn.FighterId);
        var turned = fight with { Phase = FightPhase.Fighting, PlayingId = turn.FighterId, Round = round, IsOwnTurn = false };

        if (place < 0)
            return turned;

        // Measured (D193): the server names no buff's end of time; a buff ends as the turn it ends at begins, as the
        // references count a buff down at the start of the turn of the fighter playing as it was given.
        var expired = turned.Fighters.Values.Where(fighter => fighter.Buffs.Any(buff => buff.IsOverAt(round, place))).ToList();

        return expired.Aggregate(turned, (state, fighter) => Changed(state, fighter.Id, held => held with { Buffs = [.. held.Buffs.Where(buff => !buff.IsOverAt(round, place))] }));
    }

    private static Fighter Buffed(Fighter fighter, FightBuffGiven given, global::D3B.Protocol.Game.FightBuff buff)
    {
        var kept = new FightBuff
        {
            Uid = buff.Uid,
            SourceId = given.SourceId,
            ActionId = given.ActionId,
            Kind = buff.Kind,
            SpellId = buff.SpellId,
            Grade = buff.Grade,
            EffectUid = buff.EffectUid,
            Value = buff.Value,
            Parameter = buff.Parameter,
            Triggers = buff.Triggers,
            EndRound = buff.End is { Round: >= 0 } end ? end.Round : FightBuff.Forever,
            EndTurn = buff.End?.Turn ?? 0,
        };

        // A buff the server gives again under its uid replaces the one it held.
        return fighter with { Buffs = [.. fighter.Buffs.Where(held => held.Uid != kept.Uid), kept] };
    }

    private static FightState Acted(FightState fight, GameActionFightEvent action)
    {
        return action.ActionCase switch
        {
            GameActionFightEvent.ActionOneofCase.Cast => Cast(fight, action.SourceId, action.Cast),
            GameActionFightEvent.ActionOneofCase.Death => Changed(fight, action.Death.TargetId, Died),
            GameActionFightEvent.ActionOneofCase.Teleport when IsCell(action.Teleport.CellId) => Moved(fight, action.Teleport.TargetId, action.Teleport.CellId),
            GameActionFightEvent.ActionOneofCase.Slide when IsCell(action.Slide.ToCell) => Moved(fight, action.Slide.TargetId, action.Slide.ToCell),
            GameActionFightEvent.ActionOneofCase.Carry => Changed(fight, action.Carry.TargetId, carried => carried with { Cell = null, CarrierId = action.SourceId }),
            GameActionFightEvent.ActionOneofCase.Throw when IsCell(action.Throw.CellId) => Thrown(fight, action.SourceId, action.Throw),
            GameActionFightEvent.ActionOneofCase.BuffsRemoval when action.BuffsRemoval.All => Changed(fight, action.BuffsRemoval.TargetId, fighter => fighter with { Buffs = [] }),
            GameActionFightEvent.ActionOneofCase.BuffsRemoval => Changed(fight, action.BuffsRemoval.TargetId, fighter => fighter with
            {
                Buffs = [.. fighter.Buffs.Where(buff => buff.SpellId != action.BuffsRemoval.SpellId)],
            }),
            GameActionFightEvent.ActionOneofCase.BuffRemoval when action.BuffRemoval.KindCase is FightBuffRemoval.KindOneofCase.Uid => Changed(fight, action.BuffRemoval.TargetId, fighter => fighter with
            {
                Buffs = [.. fighter.Buffs.Where(buff => buff.Uid != action.BuffRemoval.Uid)],
            }),
            GameActionFightEvent.ActionOneofCase.Swap => Swapped(fight, action.SourceId, action.Swap.TargetId),
            GameActionFightEvent.ActionOneofCase.LifeLost => Changed(fight, action.LifeLost.TargetId, fighter => fighter with
            {
                Life = Math.Max(0, fighter.Life - action.LifeLost.Loss),
                MaxLife = Math.Max(0, fighter.MaxLife - action.LifeLost.PermanentLoss),
            }),
            GameActionFightEvent.ActionOneofCase.LifeGained => Changed(fight, action.LifeGained.TargetId, fighter => fighter with
            {
                Life = Math.Min(fighter.MaxLife, fighter.Life + action.LifeGained.Amount),
            }),
            GameActionFightEvent.ActionOneofCase.Summons when action.Summons.Fighters is { } summoned => summoned.Summons.Aggregate(fight, Summoned),
            GameActionFightEvent.ActionOneofCase.MarkSet when action.MarkSet.Mark is { } mark => fight with { Marks = fight.Marks.SetItem(mark.MarkId, Marked(mark)) },
            GameActionFightEvent.ActionOneofCase.MarkRemoved => fight with { Marks = fight.Marks.Remove(action.MarkRemoved.MarkId) },
            _ => fight,
        };
    }

    private static GroundMark Marked(FightMark mark)
    {
        return new GroundMark
        {
            Id = mark.MarkId,
            Type = mark.Type,
            AuthorId = mark.AuthorId,
            Team = mark.Team,
            SetterId = mark.SpellId,
            SpellId = mark.CastSpellId,
            Grade = mark.Grade,
            Centre = mark.ImpactCell,
            Cells = [.. mark.Cells.Select(cell => cell.CellId).Where(IsCell)],
            EndRound = mark.End?.Round ?? 0,
        };
    }

    private static FightState Cast(FightState fight, long casterId, FightCast cast)
    {
        if (fight.Sequences.IsEmpty)
            return fight;

        var innermost = fight.Sequences[^1];
        var marked = fight with { Sequences = fight.Sequences.SetItem(fight.Sequences.Length - 1, innermost with { HasCast = true }) };

        // Measured (client.md): the casts a fighter chose are the first of its sequences of a spell; those its effects
        // cast follow in the same sequence or in one they trigger, and no spell's interval or casts a turn count them. A
        // weapon's blow is the first of a sequence of its own, naming no spell (D218): spell 0's, whose casts a turn count it.
        int? spellId = innermost switch
        {
            { Type: SequenceType.Spell } when cast.Spell is { } spell => spell.SpellId,
            { Type: SequenceType.Weapon } => WeaponBlows.SpellId,
            _ => null,
        };

        if (innermost.HasCast || innermost.AuthorId != casterId || spellId is null)
            return marked;

        long? target = cast.TargetId is not 0 ? cast.TargetId : null;

        return Changed(marked, casterId, caster => caster with { Casts = caster.Casts.Cast(spellId.Value, target, fight.Round) });
    }

    private static FightState Thrown(FightState fight, long carrierId, FightThrow thrown)
    {
        if (!fight.Fighters.TryGetValue(thrown.TargetId, out var carried))
            return fight;

        // Measured (D191): the throw ends the carry, the server removing none of its buffs; the carried's states from its
        // carrier name the spells whose buffs go, on both.
        var spells = carried.Buffs.Where(buff => buff.Kind is FightBuffKind.State && buff.SourceId == carrierId).Select(buff => buff.SpellId).ToHashSet();
        var landed = Changed(fight, thrown.TargetId, fighter => fighter with { Cell = thrown.CellId, CarrierId = null, Buffs = [.. fighter.Buffs.Where(buff => !spells.Contains(buff.SpellId))] });

        return Changed(landed, carrierId, fighter => fighter with { Buffs = [.. fighter.Buffs.Where(buff => !spells.Contains(buff.SpellId))] });
    }

    private static FightState Summoned(FightState fight, SummonedFighter summon)
    {
        // Measured: one position per summon, which names its id and cell; it has no actor of its own.
        if (summon.Positions.FirstOrDefault() is not { Position: { } position } spawn)
            return fight;

        var cell = position.Disposition is { HasCellId: true } at && IsCell(at.CellId) ? at.CellId : (int?)null;
        var monster = summon.Entity?.Monster;
        var fighter = Lived(new Fighter
        {
            Id = position.Id,
            Team = spawn.Team,
            Cell = cell,
            IsAlive = spawn.Alive,
            MonsterId = monster?.MonsterId,
            Grade = monster is { GradeOrLevelCase: SummonedMonster.GradeOrLevelOneofCase.Grade } ? monster.Grade : null,
            SummonerId = summon.Stats is { Summoned: true } stats ? stats.SummonerId : null,
        }, summon.Stats);

        return fight with { Fighters = fight.Fighters.SetItem(fighter.Id, fighter) };
    }

    private static FightState Swapped(FightState fight, long sourceId, long targetId)
    {
        if (!fight.Fighters.TryGetValue(sourceId, out var source) || !fight.Fighters.TryGetValue(targetId, out var target))
            return fight;

        return fight with { Fighters = fight.Fighters.SetItem(sourceId, source with { Cell = target.Cell }).SetItem(targetId, target with { Cell = source.Cell }) };
    }

    private static FightState Moved(FightState fight, long fighterId, int cell)
    {
        return Changed(fight, fighterId, fighter => fighter with { Cell = cell });
    }

    private static FightState Changed(FightState fight, long fighterId, Func<Fighter, Fighter> change)
    {
        return fight.Fighters.TryGetValue(fighterId, out var fighter) ? fight with { Fighters = fight.Fighters.SetItem(fighterId, change(fighter)) } : fight;
    }

    private static Fighter Died(Fighter fighter)
    {
        return fighter with { IsAlive = false, Life = 0, Cell = null, CarrierId = null, Buffs = [] };
    }

    private static Fighter Lived(Fighter fighter, FightCharacteristics? stats)
    {
        if (stats is null)
            return fighter;

        var spent = Spent(fighter, stats);

        if (ValueOf(stats, MostLife) is not { } most)
            return spent;

        var max = (int)(most + (ValueOf(stats, Vitality) ?? 0));

        return spent with { MaxLife = max, Life = (int)Math.Max(0, max + (ValueOf(stats, LifeLacking) ?? 0)) };
    }

    private static Fighter Spent(Fighter fighter, FightCharacteristics stats)
    {
        var characteristics = fighter.Characteristics.ToBuilder();

        foreach (var characteristic in stats.Characteristics)
        {
            if (ValueOf(characteristic) is { } value)
                characteristics[characteristic.Id] = (int)Math.Clamp(value, int.MinValue, int.MaxValue);
        }

        return fighter with
        {
            ActionPoints = (int)(ValueOf(stats, ActionPoints) ?? fighter.ActionPoints),
            MovementPoints = (int)(ValueOf(stats, MovementPoints) ?? fighter.MovementPoints),
            Characteristics = characteristics.ToImmutable(),
        };
    }

    private static long? ValueOf(FightCharacteristics stats, int id)
    {
        var found = stats.Characteristics.FirstOrDefault(characteristic => characteristic.Id == id);

        return found is null ? null : ValueOf(found);
    }

    private static long? ValueOf(Characteristic found)
    {
        return CharacteristicTotals.Of(found);
    }

    private static bool IsCell(int cell)
    {
        return cell is >= 0 and < MapData.CellCount;
    }

    private static FightEnd? Ended(FightEndEvent end, long self)
    {
        var result = end.Results.FirstOrDefault(result => result.Fighter?.FighterId == self);

        if (result is null)
            return null;

        var experience = result.Fighter.Character?.Data.Select(data => data.Experience).FirstOrDefault(found => found is not null)?.Gained ?? 0;
        var items = (result.Loot?.Objects.AsEnumerable() ?? []).SelectMany(group => group.Objects)
            .GroupBy(item => item.ItemId)
            .ToImmutableSortedDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        return new FightEnd(result.Outcome, experience, result.Loot?.Kamas ?? 0, items);
    }
}
