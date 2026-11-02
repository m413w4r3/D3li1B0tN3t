using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Tests.Runtime;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Fights;

/// <summary>A fight played as the captures measured it: the player attacking monsters (client.md, D178).</summary>
public sealed class FightEventsTests
{
    private const long Self = TestPlay.CharacterId;
    private const long Monster = -1;
    private const long Turret = -2;
    private const long FightMapId = 191_104_008;

    private static readonly CharacterState _chosen = CharacterState.Chosen(TestPlay.Key, TestPlay.Chosen().Success.Character);

    private static CharacterState Apply(CharacterState state, params IMessage[] messages)
    {
        return messages.Aggregate(state, (current, message) => CharacterEvents.Apply(current, RelayDirection.FromServer, message));
    }

    private static Characteristic Usable(int id, int left, int spent = 0)
    {
        return new Characteristic { Id = id, Usable = new UsableCharacteristic { Number4 = left + spent, Number6 = -spent, Spent = spent } };
    }

    private static Characteristic Detailed(int id, int value)
    {
        return new Characteristic { Id = id, Detailed = new DetailedCharacteristic { Number7 = value } };
    }

    private static Characteristic Value(int id, long total)
    {
        return new Characteristic { Id = id, Value = new CharacteristicValue { Total = total } };
    }

    private static Actor Character(int cell, FightTeam team = FightTeam.Challenger)
    {
        return new Actor
        {
            Id = Self,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation
            {
                Fighter = new FightFighter
                {
                    Named = new NamedFighter { Character = new FighterCharacter { BreedId = 15, Level = 18 } },
                    Stats = new FightCharacteristics { Characteristics = { Usable(1, 6), Usable(23, 3), Detailed(0, 140), Detailed(11, 58) } },
                    Spawn = new FighterSpawn { Alive = true, Team = team },
                },
            },
        };
    }

    private static SummonedFighter Summon(long id, int monsterId, int cell)
    {
        return new SummonedFighter
        {
            Entity = new SummonedEntity { Monster = new SummonedMonster { MonsterId = monsterId, Grade = 1 } },
            Stats = new FightCharacteristics { Summoned = true, SummonerId = Self, Characteristics = { Value(1, 6), Value(0, 15) } },
            Positions = { new FighterSpawn { Alive = true, Team = FightTeam.Challenger, Position = new Actor { Id = id, Disposition = new ActorDisposition { CellId = cell } } } },
        };
    }

    private static GameActionFightEvent Summoned(SummonedFighter summon)
    {
        return new GameActionFightEvent { ActionId = 181, SourceId = Self, Summons = new FightSummons { Fighters = new SummonedFighters { Summons = { summon } } } };
    }

    private static Actor MonsterActor(long id, int cell, int life)
    {
        return new Actor
        {
            Id = id,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation
            {
                Fighter = new FightFighter
                {
                    Ai = new AiFighter { Monster = new MonsterFighter { MonsterId = 3_271, Grade = 3, Level = 3 } },
                    Stats = new FightCharacteristics { Characteristics = { Value(1, 2), Value(23, 3), Value(0, life) } },
                    Spawn = new FighterSpawn { Alive = true, Team = FightTeam.Defender },
                },
            },
        };
    }

    private static FightTurnListEvent Order(params (long Id, bool Dead)[] fighters)
    {
        var order = new FightTurnListEvent();

        order.Entries.AddRange(fighters.Select(fighter => new FightTurnEntry { Fighter = new FightTurnFighter { FighterId = fighter.Id, Dead = fighter.Dead } }));

        return order;
    }

    /// <summary>The fight's context, its map and its placement, as the server sends them once the player attacked.</summary>
    private static CharacterState Placed()
    {
        return Apply(
            _chosen,
            new ContextDestroyEvent(),
            new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight },
            new MapCurrentEvent { MapId = FightMapId },
            new FightStartingEvent { AttackerId = Self, DefenderId = -20_004, Type = FightType.Monsters },
            new FightJoinEvent { PlacementTime = 440 },
            new FightFighterShowEvent { Fighter = Character(314) },
            new FightFighterShowEvent { Fighter = MonsterActor(Monster, 385, 21) },
            new FightPlacementPossiblePositionsEvent
            {
                Cells = new PlacementCells { DefenderCells = { 385, 345 }, ChallengerCells = { 314, 300 } },
            },
            Order((Self, false), (Monster, false)));
    }

    /// <summary>The placement over, the fight started and the character's first turn begun.</summary>
    private static CharacterState Playing()
    {
        return Apply(
            Placed(),
            new FightHumanReadyStateEvent { CharacterId = Self, Ready = true },
            new FightStartEvent(),
            new FightNewRoundEvent { Round = 1 },
            new FightTurnEvent { FighterId = Self, Duration = 300, Round = 1 },
            new FightTurnStartPlayingEvent());
    }

    [Fact]
    public void AFightsContextGivesTheCharacterAFightInPlacement()
    {
        var state = Apply(_chosen, new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });

        Assert.True(state.IsFighting);
        Assert.Equal(FightPhase.Placement, state.Fight?.Phase);
        Assert.Empty(state.Fight!.Fighters);
    }

    [Fact]
    public void ThePlacementGivesEachTeamsCellsAndWhereEveryFighterStands()
    {
        var fight = Placed().Fight!;

        Assert.Equal(FightTeam.Challenger, fight.Team);
        Assert.Equal([314, 300], fight.OwnCells);
        Assert.Equal([385, 345], fight.DefenderCells);
        Assert.Equal([Self, Monster], fight.TurnOrder);

        var self = fight.Fighters[Self];

        Assert.Equal((314, 198, 198, 6, 3, 15, 18), (self.Cell, self.Life, self.MaxLife, self.ActionPoints, self.MovementPoints, self.BreedId, self.Level));

        var monster = fight.Fighters[Monster];

        Assert.Equal((FightTeam.Defender, 385, 21, 2, 3, 3_271, 3), (monster.Team, monster.Cell, monster.Life, monster.ActionPoints, monster.MovementPoints, monster.MonsterId, monster.Grade));
    }

    [Fact]
    public void AMoveDuringThePlacementPlacesTheFighterWhereTheServerSays()
    {
        var moved = Apply(
            Placed(),
            new EntitiesDispositionEvent
            {
                Dispositions =
                {
                    new ActorDisposition { EntityId = Monster, CellId = 385 },
                    new ActorDisposition { EntityId = Self, CellId = 300 },
                    new ActorDisposition { EntityId = -9, CellId = 345 },
                },
            });

        Assert.Equal(300, moved.Fight!.Fighters[Self].Cell);
        Assert.Equal(385, moved.Fight.Fighters[Monster].Cell);
        Assert.False(moved.Fight.Fighters.ContainsKey(-9));
    }

    [Fact]
    public void TheTurnsSayWhoPlaysAndWhenTheCharacterMayAct()
    {
        var playing = Playing();

        Assert.Equal(FightPhase.Fighting, playing.Fight!.Phase);
        Assert.False(playing.Fight.IsReady);
        Assert.Equal((Self, 1, true), (playing.Fight.PlayingId, playing.Fight.Round, playing.Fight.IsOwnTurn));

        var ended = Apply(playing, new FightTurnEndEvent { FighterId = Self });

        Assert.Equal((null, false), (ended.Fight!.PlayingId, ended.Fight.IsOwnTurn));

        var theirs = Apply(ended, new FightTurnEvent { FighterId = Monster, Duration = 290, Round = 1 }, new FightTurnStartPlayingEvent());

        Assert.Equal((Monster, false), (theirs.Fight!.PlayingId, theirs.Fight.IsOwnTurn));
    }

    [Fact]
    public void TheCharactersWalkInAFightMovesItsFighterWithNoPathToConfirm()
    {
        var walked = Apply(Playing(), new MapMovementEvent { ActorId = Self, Cells = { 314, 329 } });

        Assert.Equal(329, walked.Fight!.Fighters[Self].Cell);
        Assert.False(walked.IsWalking);
        Assert.Null(walked.Cell);
    }

    [Fact]
    public void TheCharacteristicsGiveTheApAndMpLeftAndTheAnnouncedSpendingIsNotCountedAgain()
    {
        var spent = Apply(
            Playing(),
            new FightRefreshCharacterStatsEvent { FighterId = Self, Stats = new FightCharacteristics { Characteristics = { Usable(1, 3, spent: 3) } } },
            new GameActionFightEvent { ActionId = 102, SourceId = Self, Points = new FightPointsVariation { TargetId = Self, Delta = -3 } });

        Assert.Equal((3, 3), (spent.Fight!.Fighters[Self].ActionPoints, spent.Fight.Fighters[Self].MovementPoints));
    }

    [Fact]
    public void ABlowTakesLifeAndADeathLeavesTheFighterOnNoCell()
    {
        var hit = Apply(
            Playing(),
            new GameActionFightEvent { ActionId = 300, SourceId = Self, Cast = new FightCast { Spell = new FightCastSpell { SpellId = 13_858 }, CellId = 385 } },
            new GameActionFightEvent { ActionId = 99, SourceId = Self, LifeLost = new FightLifeLost { TargetId = Monster, Loss = 15, PermanentLoss = 3, Element = 2 } });

        Assert.Equal((6, 18), (hit.Fight!.Fighters[Monster].Life, hit.Fight.Fighters[Monster].MaxLife));

        var dead = Apply(
            hit,
            new GameActionFightEvent { ActionId = 103, SourceId = Self, Death = new FightDeath { TargetId = Monster } },
            Order((Self, false), (Monster, true)));

        Assert.False(dead.Fight!.Fighters[Monster].IsAlive);
        Assert.Null(dead.Fight.Fighters[Monster].Cell);
        Assert.Equal([Self, Monster], dead.Fight.TurnOrder);
    }

    [Fact]
    public void AHealNeverGivesMoreThanTheMostLife()
    {
        var healed = Apply(
            Playing(),
            new GameActionFightEvent { ActionId = 99, SourceId = Monster, LifeLost = new FightLifeLost { TargetId = Self, Loss = 50 } },
            new GameActionFightEvent { ActionId = 108, SourceId = Self, LifeGained = new FightLifeGained { TargetId = Self, Amount = 36 } },
            new GameActionFightEvent { ActionId = 108, SourceId = Self, LifeGained = new FightLifeGained { TargetId = Self, Amount = 36 } });

        Assert.Equal(198, healed.Fight!.Fighters[Self].Life);
    }

    [Fact]
    public void ATeleportAndASwapMoveTheFighters()
    {
        var teleported = Apply(Playing(), new GameActionFightEvent { ActionId = 4, SourceId = Self, Teleport = new FightTeleport { TargetId = Self, CellId = 384 } });

        Assert.Equal(384, teleported.Fight!.Fighters[Self].Cell);

        var swapped = Apply(teleported, new GameActionFightEvent { ActionId = 8, SourceId = Self, Swap = new FightSwap { TargetId = Monster } });

        Assert.Equal((385, 384), (swapped.Fight!.Fighters[Self].Cell, swapped.Fight.Fighters[Monster].Cell));
    }

    [Fact]
    public void ASummonComesIntoTheFightWithItsSummonerAndCell()
    {
        var summoned = Apply(Playing(), Summoned(Summon(Turret, 5_836, 286)));

        var turret = summoned.Fight!.Fighters[Turret];

        Assert.Equal((FightTeam.Challenger, 286, 15, 6, 5_836, 1, Self), (turret.Team, turret.Cell, turret.Life, turret.ActionPoints, turret.MonsterId, turret.Grade, turret.SummonerId));
    }

    [Fact]
    public void TheClientPlaysTheTurnOfASummonOfTheCharactersItControls()
    {
        // Measured (9f2): the server lets the client play the summons it controls, 20 turns of 6 monsters in the captures.
        var summoned = Apply(Playing(), Summoned(Summon(Turret, 5_836, 286)), new FightTurnEndEvent { FighterId = Self });

        var controlled = Apply(summoned, new FightTurnEvent { FighterId = Turret, Duration = 150, Round = 1 }, new FightTurnStartPlayingEvent());

        Assert.Equal((Turret, true), (controlled.Fight!.PlayingId, controlled.Fight.IsOwnTurn));

        var free = Apply(summoned, new FightTurnEvent { FighterId = Turret, Duration = 150, Round = 1 });

        Assert.Equal((Turret, false), (free.Fight!.PlayingId, free.Fight.IsOwnTurn));
    }

    [Fact]
    public void ASummonSummonedAgainUnderAnotherMonsterKeepsItsIdAndTakesTheNewOne()
    {
        // Measured: a turret summoned again under another monster's id, its own id the same, on another cell.
        var again = Apply(Playing(), Summoned(Summon(Turret, 5_836, 286)), Summoned(Summon(Turret, 5_835, 328)));

        Assert.Equal((5_835, 328), (again.Fight!.Fighters[Turret].MonsterId, again.Fight.Fighters[Turret].Cell));
        Assert.Equal(3, again.Fight.Fighters.Count);
    }

    [Fact]
    public void TheCharactersOwnFighterSaysItsTeamAndPlacementCells()
    {
        var defending = Apply(
            _chosen,
            new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight },
            new FightFighterShowEvent { Fighter = Character(385, FightTeam.Defender) },
            new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { DefenderCells = { 385, 345 }, ChallengerCells = { 314, 300 } } });

        Assert.Equal(FightTeam.Defender, defending.Fight!.Team);
        Assert.Equal([385, 345], defending.Fight.OwnCells);
    }

    [Fact]
    public void TheFightersSynchronizedKeepWhatTheActionsToldAndTheOthersGo()
    {
        var hit = Apply(
            Playing(),
            new FightFighterShowEvent { Fighter = MonsterActor(-3, 345, 18) },
            new GameActionFightEvent { ActionId = 99, SourceId = Self, LifeLost = new FightLifeLost { TargetId = Monster, Loss = 15 } });
        var synchronized = Apply(
            hit,
            new FightSynchronizeEvent { Fighters = { Character(300), new Actor { Id = Monster, Disposition = new ActorDisposition { CellId = 371 } } } });

        var monster = synchronized.Fight!.Fighters[Monster];

        Assert.Equal([Monster, Self], synchronized.Fight.Fighters.Keys);
        Assert.Equal((371, 6, 21, FightTeam.Defender, 3_271), (monster.Cell, monster.Life, monster.MaxLife, monster.Team, monster.MonsterId));
        Assert.Equal(300, synchronized.Fight.Fighters[Self].Cell);
    }

    [Fact]
    public void AnEndWithoutTheCharactersResultEndsTheFightWithNoGains()
    {
        var ended = Apply(Playing(), new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });

        Assert.Equal(FightPhase.Ended, ended.Fight!.Phase);
        Assert.Null(ended.Fight.End);
    }

    [Fact]
    public void TheEndGivesTheCharactersOutcomeAndGainsUntilTheRoleplay()
    {
        var result = new FightResult
        {
            Outcome = FightOutcome.Won,
            Fighter = new FightResultFighter
            {
                FighterId = Self,
                Alive = true,
                Character = new FightResultCharacter { Level = 18, Data = { new FightResultData { Experience = new FightExperience { Experience = 124_909, Gained = 77 } } } },
            },
            Loot = new FightLoot
            {
                Kamas = 7,
                Objects =
                {
                    new FightLootObjects { MonsterId = 3_271, Objects = { new FightLootObject { ItemId = 16_518, Quantity = 1 } } },
                    new FightLootObjects { MonsterId = 3_272, Objects = { new FightLootObject { ItemId = 16_518, Quantity = 1 }, new FightLootObject { ItemId = 1_984, Quantity = 2 } } },
                },
            },
        };
        var ended = Apply(Playing(), new FightEndEvent { Results = { result, new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });

        Assert.Equal(FightPhase.Ended, ended.Fight!.Phase);
        Assert.Equal((FightOutcome.Won, 77, 7), (ended.Fight.End!.Outcome, ended.Fight.End.Experience, ended.Fight.End.Kamas));
        Assert.Equal([KeyValuePair.Create(1_984, 2), KeyValuePair.Create(16_518, 2)], ended.Fight.End.Items.ToArray());

        var over = Apply(ended, new ContextDestroyEvent(), new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Roleplay });

        Assert.Null(over.Fight);
        Assert.False(over.IsFighting);
    }

    [Fact]
    public void WhatTheServerSaysOfAFighterItNeverShowedChangesNothing()
    {
        var playing = Playing();
        var unknown = Apply(
            playing,
            new MapMovementEvent { ActorId = -7, Cells = { 100, 114 } },
            new GameActionFightEvent { ActionId = 103, SourceId = Self, Death = new FightDeath { TargetId = -7 } },
            new FightRefreshCharacterStatsEvent { FighterId = -7, Stats = new FightCharacteristics() });

        Assert.Equal(playing.Fight!.Fighters, unknown.Fight!.Fighters);
        Assert.Same(playing, Apply(playing, new FightJoinEvent { PlacementTime = 440 }, new FightHumanReadyStateEvent { CharacterId = -9, Ready = true }));
    }

    private static FightEffectsEvent Buff(long target, int uid, int action, FightBuffKind kind, int spell, int value = 0, int parameter = 0, int endTurn = 0, int endRound = -1, long source = Self)
    {
        return new FightEffectsEvent
        {
            Given = new FightBuffGiven
            {
                ActionId = action,
                SourceId = source,
                Buff = new global::D3B.Protocol.Game.FightBuff
                {
                    Uid = uid,
                    TargetId = target,
                    Kind = kind,
                    SpellId = spell,
                    Grade = 1,
                    EffectUid = 400_000 + uid,
                    Value = value,
                    Parameter = parameter,
                    Triggers = "I",
                    End = new FightMoment { Turn = endTurn, Round = endRound },
                },
            },
        };
    }

    private static GameActionFightEvent Cast(long caster, int spell, int cell, long target = 0)
    {
        return new GameActionFightEvent
        {
            ActionId = 300,
            SourceId = caster,
            Cast = new FightCast { Spell = new FightCastSpell { SpellId = spell, LevelId = 40_000 + spell }, CellId = cell, TargetId = target },
        };
    }

    [Fact]
    public void ABuffIsKeptOnItsFighterWhichItsStatesAndSpellsModifiersCome()
    {
        var buffed = Apply(
            Playing(),
            Buff(Self, 1, 950, FightBuffKind.State, 12_787, parameter: 3),
            Buff(Self, 2, 281, FightBuffKind.SpellModifier, 12_787, value: 12_832, parameter: 3),
            Buff(Monster, 1, 116, FightBuffKind.Boost, 4_195, value: 2, endTurn: 0, endRound: 2, source: Monster));

        var self = buffed.Fight!.Fighters[Self];

        Assert.Equal([1, 2], self.Buffs.Select(buff => buff.Uid));
        Assert.Equal([3], self.States);
        Assert.Equal([new Engine.Fights.SpellModifier(Engine.Fights.SpellModifierKind.Range, 12_832, 3)], self.Modifiers);

        var deboost = Assert.Single(buffed.Fight.Fighters[Monster].Buffs);

        Assert.Equal((Monster, 116, 2, 2, 0), (deboost.SourceId, deboost.ActionId, deboost.Value, deboost.EndRound, deboost.EndTurn));

        var again = Apply(buffed, Buff(Self, 1, 950, FightBuffKind.State, 12_787, parameter: 8));

        Assert.Equal([8], again.Fight!.Fighters[Self].States);
    }

    [Fact]
    public void ABuffEndsAsTheTurnItEndsAtBeginsAndAWholeFightsNever()
    {
        var buffed = Apply(
            Playing(),
            Buff(Monster, 1, 116, FightBuffKind.Boost, 13_858, value: 2, endTurn: 0, endRound: 2),
            Buff(Self, 2, 950, FightBuffKind.State, 13_858, parameter: 5));

        var theirs = Apply(buffed, new FightTurnEndEvent { FighterId = Self }, new FightTurnEvent { FighterId = Monster, Round = 1 });

        Assert.Single(theirs.Fight!.Fighters[Monster].Buffs);

        var next = Apply(theirs, new FightTurnEndEvent { FighterId = Monster }, new FightNewRoundEvent { Round = 2 }, new FightTurnEvent { FighterId = Self, Round = 2 });

        Assert.Empty(next.Fight!.Fighters[Monster].Buffs);
        Assert.Equal([5], next.Fight.Fighters[Self].States);
    }

    [Fact]
    public void BuffsGoBySpellAllTogetherOrOneByItsUidAndWithTheirFightersDeath()
    {
        var buffed = Apply(
            Playing(),
            Buff(Self, 1, 950, FightBuffKind.State, 10, parameter: 3),
            Buff(Self, 2, 112, FightBuffKind.Boost, 20, value: 10),
            Buff(Self, 3, 1040, FightBuffKind.Boost, 20, value: 79),
            Buff(Monster, 1, 950, FightBuffKind.State, 10, parameter: 8));

        var ofSpell = Apply(buffed, new GameActionFightEvent { ActionId = 406, SourceId = Self, BuffsRemoval = new FightBuffsRemoval { SpellId = 10, TargetId = Self } });

        Assert.Equal([2, 3], ofSpell.Fight!.Fighters[Self].Buffs.Select(buff => buff.Uid));

        var one = Apply(ofSpell, new GameActionFightEvent { ActionId = 514, SourceId = Self, BuffRemoval = new FightBuffRemoval { Uid = 3, TargetId = Self } });

        Assert.Equal([2], one.Fight!.Fighters[Self].Buffs.Select(buff => buff.Uid));

        var all = Apply(one, new GameActionFightEvent { ActionId = 132, SourceId = -1, BuffsRemoval = new FightBuffsRemoval { All = true, TargetId = Self } });

        Assert.Empty(all.Fight!.Fighters[Self].Buffs);

        var dead = Apply(all, new GameActionFightEvent { ActionId = 103, SourceId = Self, Death = new FightDeath { TargetId = Monster } });

        Assert.Empty(dead.Fight!.Fighters[Monster].Buffs);
    }

    [Fact]
    public void AMasksStateGoesWhenTheServerEndsItsBuffAndAnotherTakesItsPlace()
    {
        var masked = Apply(
            Playing(),
            Buff(Self, 1, 950, FightBuffKind.State, 18_633, parameter: 98),
            Buff(Monster, 1, 950, FightBuffKind.State, 10, parameter: 8));

        var changed = Apply(
            masked,
            new FightBuffEndEvent { Uid = 1, TargetId = Self },
            Buff(Self, 2, 950, FightBuffKind.State, 13_388, parameter: 99),
            new FightBuffEndEvent { Uid = 7, TargetId = Self });

        Assert.Equal([99], changed.Fight!.Fighters[Self].States);
        Assert.Equal([8], changed.Fight.Fighters[Monster].States);
    }

    [Fact]
    public void AWeaponsBlowIsACastOfSpellZeroInTheHistory()
    {
        // Measured (D218): the blow is the first action of a sequence of its own, a cast naming no spell.
        var blow = new GameActionFightEvent { ActionId = 303, SourceId = Self, Cast = new FightCast { CellId = 288, TargetId = Monster } };
        var struck = Apply(
            Playing(),
            new SequenceStartEvent { Type = SequenceType.Weapon, AuthorId = Self },
            blow,
            new SequenceEndEvent { Type = SequenceType.Weapon, AuthorId = Self });
        var history = struck.Fight!.Fighters[Self].Casts;

        Assert.Equal((1, 1), (history.CastsIn(0, 1), history.CastsOn(0, Monster, 1)));
        Assert.Empty(struck.Fight.Sequences);

        var unnamed = Apply(Playing(), new SequenceStartEvent { Type = SequenceType.Spell, AuthorId = Self }, blow);

        Assert.Empty(unnamed.Fight!.Fighters[Self].Casts.LastRounds);
    }

    [Fact]
    public void TheCastsAFighterChoseAreItsHistoryNotThoseItsEffectsCast()
    {
        var cast = Apply(
            Playing(),
            new SequenceStartEvent { Type = SequenceType.Spell, AuthorId = Self },
            Cast(Self, 13_858, 385, Monster),
            Cast(Self, 29_132, 385, Monster),
            new SequenceStartEvent { Type = SequenceType.Triggered, AuthorId = Self },
            Cast(Self, 29_162, 385),
            new SequenceEndEvent { Type = SequenceType.Triggered, AuthorId = Self },
            new SequenceEndEvent { Type = SequenceType.Spell, AuthorId = Self });

        var history = cast.Fight!.Fighters[Self].Casts;

        Assert.Equal([13_858], history.LastRounds.Keys);
        Assert.Equal((1, 1), (history.CastsIn(13_858, 1), history.CastsOn(13_858, Monster, 1)));
        Assert.Empty(cast.Fight.Sequences);

        var theirs = Apply(cast, new SequenceStartEvent { Type = SequenceType.Spell, AuthorId = Monster }, Cast(Self, 13_858, 385));

        Assert.Equal(1, theirs.Fight!.Fighters[Self].Casts.CastsIn(13_858, 1));
        Assert.Single(theirs.Fight.Sequences);
    }

    [Fact]
    public void TheFightsContextGivesTheSpellsTheCharacterCastsAndNoneOfASummons()
    {
        var listed = Apply(
            Playing(),
            new FightSpellsContextEvent { CharacterId = Self, FighterId = Self, Spells = { new SpellItem { SpellId = 8_703, Grade = 1 }, new SpellItem { SpellId = 202, Grade = 2 } } },
            new FightSpellsContextEvent { CharacterId = Self, FighterId = Monster, Spells = { new SpellItem { SpellId = 5, Grade = 1 } } });

        Assert.Equal([KeyValuePair.Create(202, 2), KeyValuePair.Create(8_703, 1)], listed.Fight!.Spells.ToArray());
    }

    [Fact]
    public void AMarkIsKeptAsTheServerSetsItAndGoesAsItTakesItBack()
    {
        var mark = new FightMark
        {
            MarkId = 3,
            AuthorId = Monster,
            Team = FightTeam.Defender,
            ImpactCell = 414,
            SpellId = 10_241,
            CastSpellId = 10_241,
            Grade = 5,
            End = new FightMoment { Turn = 5, Round = 4 },
            Cells = { new FightMarkedCell { CellId = 413 }, new FightMarkedCell { CellId = 415 } },
        };

        var marked = Apply(Playing(), new GameActionFightEvent { ActionId = 401, SourceId = Monster, MarkSet = new FightMarkSet { Mark = mark } });

        Assert.Equal(
            (FightMarkType.Glyph, Monster, 10_241, 5, 414, 4),
            (marked.Fight!.Marks[3].Type, marked.Fight.Marks[3].AuthorId, marked.Fight.Marks[3].SpellId, marked.Fight.Marks[3].Grade, marked.Fight.Marks[3].Centre, marked.Fight.Marks[3].EndRound));
        Assert.Equal([413, 415], marked.Fight.Marks[3].Cells);

        var gone = Apply(marked, new GameActionFightEvent { ActionId = 310, SourceId = Monster, MarkRemoved = new FightMarkRemoved { MarkId = 3 } });

        Assert.Empty(gone.Fight!.Marks);
    }

    [Fact]
    public void TheTeamsOptionsAreKeptAsTheServerSaysThem()
    {
        var closed = Apply(
            Placed(),
            new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = false },
            new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = false },
            new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = true });

        Assert.Equal([KeyValuePair.Create(FightOption.NoSpectators, false), KeyValuePair.Create(FightOption.Closed, true)], closed.Fight!.Options.ToArray());
    }

    [Fact]
    public void TheCooldownsTheServerGivesAreKeptBySpell()
    {
        var cooled = Apply(
            Playing(),
            new RefreshCooldownEvent { FighterId = Self, Cooldowns = { new FightSpellCooldown { SpellId = 13_249, Rounds = 1 }, new FightSpellCooldown { SpellId = 373 } } });

        Assert.Equal([KeyValuePair.Create(373, 0), KeyValuePair.Create(13_249, 1)], cooled.Fight!.Fighters[Self].Cooldowns.ToArray());
    }

    [Fact]
    public void ASlideMovesItsFighterACarryTakesItOffItsCellAndAThrowLandsItWithoutTheCarrysStates()
    {
        var slid = Apply(Playing(), new GameActionFightEvent { ActionId = 5, SourceId = Self, Slide = new FightSlide { TargetId = Monster, FromCell = 385, ToCell = 399 } });

        Assert.Equal(399, slid.Fight!.Fighters[Monster].Cell);

        var carried = Apply(
            slid,
            new GameActionFightEvent { ActionId = 50, SourceId = Self, Carry = new FightCarry { TargetId = Monster, CellId = 399 } },
            Buff(Self, 1, 950, FightBuffKind.State, 12_787, parameter: 3),
            Buff(Monster, 1, 950, FightBuffKind.State, 12_787, parameter: 8),
            Buff(Monster, 2, 950, FightBuffKind.State, 7_000, parameter: 6, source: Monster));

        Assert.Equal((null, Self), (carried.Fight!.Fighters[Monster].Cell, carried.Fight.Fighters[Monster].CarrierId));

        var thrown = Apply(carried, new GameActionFightEvent { ActionId = 51, SourceId = Self, Throw = new FightThrow { TargetId = Monster, CellId = 341 } });
        var monster = thrown.Fight!.Fighters[Monster];

        Assert.Equal((341, null), (monster.Cell, monster.CarrierId));
        Assert.Equal([6], monster.States);
        Assert.Empty(thrown.Fight.Fighters[Self].States);
    }

    [Fact]
    public void TheCharacteristicsTheServerGivesAreKeptWhole()
    {
        var refreshed = Apply(
            Playing(),
            new FightRefreshCharacterStatsEvent { FighterId = Self, Stats = new FightCharacteristics { Characteristics = { Usable(1, 4, 2), Detailed(15, 140), Value(107, 100) } } });

        var self = refreshed.Fight!.Fighters[Self];

        Assert.Equal((4, 140, 100), (self.ActionPoints, self.Characteristics[15], self.Characteristics[107]));
        Assert.Equal(3, self.Characteristics[23]);
    }
}
