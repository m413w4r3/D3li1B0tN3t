using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Bot.Tests.Fights;

/// <summary>The AI playing a fight through the bot, from the placement to the end, the server and the client played as the captures measured them (D202).</summary>
public sealed class FightPlayTests : IAsyncDisposable
{
    private const long Self = TestPlay.CharacterId;
    private const long Monster = -1;
    private const int Far = 300;

    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static int Beside(int cell, int dx, int dy)
    {
        var point = MapGeometry.PointOf(cell);

        Assert.True(MapGeometry.TryGetCell(new MapPoint(point.X + dx, point.Y + dy), out var found));

        return found;
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

    private static Actor Character(int cell)
    {
        // No MP, so that only the placement brings the character within its blow's range.
        return new Actor
        {
            Id = Self,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation
            {
                Fighter = new FightFighter
                {
                    Named = new NamedFighter { Character = new FighterCharacter { BreedId = 15, Level = 18 } },
                    Stats = new FightCharacteristics { Characteristics = { Usable(1, 6), Usable(23, 0), Detailed(0, 140), Detailed(11, 58) } },
                    Spawn = new FighterSpawn { Alive = true, Team = FightTeam.Challenger },
                },
            },
        };
    }

    private static Actor MonsterActor(int cell)
    {
        // A monster the pack lacks: the AI knows none of its spells.
        return new Actor
        {
            Id = Monster,
            Disposition = new ActorDisposition { CellId = cell },
            Information = new ActorInformation
            {
                Fighter = new FightFighter
                {
                    Ai = new AiFighter { Monster = new MonsterFighter { MonsterId = 31, Grade = 1, Level = 3 } },
                    Stats = new FightCharacteristics { Characteristics = { Value(1, 2), Value(23, 3), Value(0, 50) } },
                    Spawn = new FighterSpawn { Alive = true, Team = FightTeam.Defender },
                },
            },
        };
    }

    private static FightTurnListEvent Order()
    {
        var order = new FightTurnListEvent();

        order.Entries.Add(new FightTurnEntry { Fighter = new FightTurnFighter { FighterId = Self } });
        order.Entries.Add(new FightTurnEntry { Fighter = new FightTurnFighter { FighterId = Monster } });

        return order;
    }

    private static void Placement(TestConnection connection, int near, int monster)
    {
        foreach (var message in new IMessage[]
        {
            new ContextDestroyEvent(),
            new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight },
            new MapCurrentEvent { MapId = TestPlay.MapId },
            new FightStartingEvent { AttackerId = Self, DefenderId = -20_004, Type = FightType.Monsters },
            new FightJoinEvent { PlacementTime = 440 },
            new FightFighterShowEvent { Fighter = Character(Far) },
            new FightFighterShowEvent { Fighter = MonsterActor(monster) },
            new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { DefenderCells = { monster }, ChallengerCells = { Far, near } } },
            new FightSpellsContextEvent { FighterId = Self, Spells = { new SpellItem { SpellId = TestPlay.BlowSpellId, Grade = 1 } } },
            Order(),
        })
        {
            connection.Server(message);
        }
    }

    private static void Cast(TestConnection connection, int cell, int apLeft, int actionId)
    {
        // As the server answers a cast, then the client once it showed it (client.md).
        connection.Server(new SequenceStartEvent { Type = SequenceType.Spell, AuthorId = Self });
        connection.Server(new GameActionFightEvent { ActionId = 300, SourceId = Self, Cast = new FightCast { Spell = new FightCastSpell { SpellId = TestPlay.BlowSpellId, LevelId = TestPlay.BlowLevelId }, CellId = cell, TargetId = Monster } });
        connection.Server(new GameActionFightEvent { ActionId = 97, SourceId = Self, LifeLost = new FightLifeLost { TargetId = Monster, Loss = 11, Element = 1 } });
        connection.Server(new FightRefreshCharacterStatsEvent { FighterId = Self, Stats = new FightCharacteristics { Characteristics = { Usable(1, apLeft, spent: 6 - apLeft), Usable(23, 0) } } });
        connection.Server(new SequenceEndEvent { ActionId = actionId, Type = SequenceType.Spell, AuthorId = Self });
        connection.Client(new GameActionAcknowledgementRequest { ActionId = actionId, Valid = true });
    }

    private static void Struck(TestConnection connection, int cell, int apLeft, int actionId)
    {
        // As the server answered the blows captured (D218): a sequence of its own, the blow naming no spell.
        connection.Server(new SequenceStartEvent { Type = SequenceType.Weapon, AuthorId = Self });
        connection.Server(new GameActionFightEvent { ActionId = 303, SourceId = Self, Cast = new FightCast { CellId = cell, TargetId = Monster } });
        connection.Server(new GameActionFightEvent { ActionId = 97, SourceId = Self, LifeLost = new FightLifeLost { TargetId = Monster, Loss = 22, Element = 1 } });
        connection.Server(new FightRefreshCharacterStatsEvent { FighterId = Self, Stats = new FightCharacteristics { Characteristics = { Usable(1, apLeft, spent: 6 - apLeft), Usable(23, 0) } } });
        connection.Server(new SequenceEndEvent { ActionId = actionId, Type = SequenceType.Weapon, AuthorId = Self });
        connection.Client(new GameActionAcknowledgementRequest { ActionId = actionId, Valid = true });
    }

    private async Task<IMessage> SentAsync(TestConnection connection, int count)
    {
        // The AI waits the client's display and the fight's pace before each request: the clock moves until it sent.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (connection.Sent.Count < count)
        {
            Assert.True(DateTime.UtcNow < deadline, $"D3B sent {connection.Sent.Count} requests, not {count}.");
            _play.Time.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        return connection.Sent[count - 1];
    }

    private async Task LoggedAsync(string message)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!_play.Log.GetSnapshot().Any(record => record.Message == message))
        {
            Assert.True(DateTime.UtcNow < deadline, $"The log never said: {message}");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TheAIClosesTheFightPlacesTheCharacterReadiesItCastsAndEndsItsTurn()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);
        var sent = connection.Sent.Count;

        bot.FightsAlone = true;
        Placement(connection, near, monster);

        Assert.Equal(FightOption.Closed, Assert.IsType<FightOptionToggleRequest>(await SentAsync(connection, sent + 1)).Option);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = true });
        Assert.Equal(FightOption.NoSpectators, Assert.IsType<FightOptionToggleRequest>(await SentAsync(connection, sent + 2)).Option);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });

        var placed = Assert.IsType<FightPlacementPositionRequest>(await SentAsync(connection, sent + 3));

        Assert.Equal((near, Self), (placed.CellId, placed.FighterId));
        connection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = Self, CellId = near } } });
        Assert.True(Assert.IsType<FightReadyRequest>(await SentAsync(connection, sent + 4)).Ready);
        connection.Server(new FightHumanReadyStateEvent { CharacterId = Self, Ready = true });

        connection.Server(new FightStartEvent());
        connection.Server(new FightNewRoundEvent { Round = 1 });
        connection.Server(new FightTurnEvent { FighterId = Self, Duration = 300, Round = 1 });
        connection.Server(new FightTurnStartPlayingEvent());

        // Two blows of 3 AP on the monster, from the cell the placement chose, then the turn ends: nothing is left to do.
        var first = Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));

        Assert.Equal((TestPlay.BlowSpellId, monster), (first.SpellId, first.CellId));
        Cast(connection, monster, apLeft: 3, actionId: 1);

        var second = Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 6));

        Assert.Equal((TestPlay.BlowSpellId, monster), (second.SpellId, second.CellId));
        Cast(connection, monster, apLeft: 0, actionId: 2);

        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 7));
        connection.Server(new FightTurnEndEvent { FighterId = Self });
        connection.Server(new FightTurnEvent { FighterId = Monster, Duration = 300, Round = 1 });
        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        await bot.ProcessedAsync();

        // The behavior writes the turn's end once the server's answer reached it, on its own task, after the bot's loop handled it.
        await LoggedAsync($"Bot {Self}: ends its turn after 2 actions");

        Assert.Equal(sent + 7, connection.Sent.Count);
        Assert.False(bot.IsPaused);
        Assert.DoesNotContain(_play.Log.GetSnapshot(), record => record.Message.Contains("did not show", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ATurnTheClientEndsByItselfLeavesTheAIPlayingTheNextOne()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);
        var sent = connection.Sent.Count;

        bot.FightsAlone = true;
        Placement(connection, near, monster);
        await SentAsync(connection, sent + 1);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = true });
        await SentAsync(connection, sent + 2);
        connection.Server(new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true });
        await SentAsync(connection, sent + 3);
        connection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = Self, CellId = near } } });
        await SentAsync(connection, sent + 4);
        connection.Server(new FightHumanReadyStateEvent { CharacterId = Self, Ready = true });

        connection.Server(new FightStartEvent());
        connection.Server(new FightNewRoundEvent { Round = 1 });
        connection.Server(new FightTurnEvent { FighterId = Self, Duration = 300, Round = 1 });
        connection.Server(new FightTurnStartPlayingEvent());
        Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 5));
        Cast(connection, monster, apLeft: 3, actionId: 1);

        // As in a friend's capture (D252): the client ends the AI's turn by itself, though no one touched it.
        connection.Client(new FightTurnFinishRequest { Automatic = true });
        connection.Server(new FightTurnEndEvent { FighterId = Self });
        connection.Server(new FightTurnEvent { FighterId = Monster, Duration = 300, Round = 1 });
        connection.Server(new FightTurnEndEvent { FighterId = Monster });
        connection.Server(new FightNewRoundEvent { Round = 2 });
        connection.Server(new FightTurnEvent { FighterId = Self, Duration = 300, Round = 2 });
        connection.Server(new FightRefreshCharacterStatsEvent { FighterId = Self, Stats = new FightCharacteristics { Characteristics = { Usable(1, 6), Usable(23, 0) } } });
        connection.Server(new FightTurnStartPlayingEvent());

        var next = Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 6));

        Assert.Equal((TestPlay.BlowSpellId, monster), (next.SpellId, next.CellId));
        Assert.False(bot.IsPaused);
        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        await bot.ProcessedAsync();
    }

    [Fact]
    public async Task AWornSwordIsStruckAsSpellZeroBesideTheSpell()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -1, 0);
        var sent = connection.Sent.Count;

        connection.Server(new ObjectAddedEvent { Object = new InventoryObject { Item = new ObjectItem { Uid = 77, ItemId = TestPlay.SwordItemId, Quantity = 1 }, Position = 1 } });
        bot.FightsAlone = true;

        foreach (var message in new IMessage[]
        {
            new ContextDestroyEvent(),
            new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight },
            new MapCurrentEvent { MapId = TestPlay.MapId },
            new FightStartingEvent { AttackerId = Self, DefenderId = -20_004, Type = FightType.Monsters },
            new FightJoinEvent { PlacementTime = 440 },
            new FightFighterShowEvent { Fighter = Character(Far) },
            new FightFighterShowEvent { Fighter = MonsterActor(monster) },
            new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { DefenderCells = { monster }, ChallengerCells = { Far, near } } },
            new FightSpellsContextEvent { FighterId = Self, Spells = { new SpellItem { SpellId = TestPlay.BlowSpellId, Grade = 1 }, new SpellItem { SpellId = 0, Grade = 1 } } },
            Order(),
            new FightOptionUpdateEvent { Option = FightOption.Closed, Enabled = true },
            new FightOptionUpdateEvent { Option = FightOption.NoSpectators, Enabled = true },
        })
        {
            connection.Server(message);
        }

        var placed = Assert.IsType<FightPlacementPositionRequest>(await SentAsync(connection, sent + 1));

        Assert.Equal(near, placed.CellId);
        connection.Server(new EntitiesDispositionEvent { Dispositions = { new ActorDisposition { EntityId = Self, CellId = near } } });
        Assert.IsType<FightReadyRequest>(await SentAsync(connection, sent + 2));
        connection.Server(new FightHumanReadyStateEvent { CharacterId = Self, Ready = true });
        connection.Server(new FightStartEvent());
        connection.Server(new FightNewRoundEvent { Round = 1 });
        connection.Server(new FightTurnEvent { FighterId = Self, Duration = 300, Round = 1 });
        connection.Server(new FightTurnStartPlayingEvent());

        // The sword's blow and the spell, each once, in the order the AI chose; the blow's sequence is a weapon's.
        var spells = new List<int>();

        for (var cast = 1; cast <= 2; cast++)
        {
            var request = Assert.IsType<GameActionFightCastRequest>(await SentAsync(connection, sent + 2 + cast));

            Assert.Equal(monster, request.CellId);
            spells.Add(request.SpellId);

            if (request.SpellId is 0)
                Struck(connection, monster, apLeft: 6 - (3 * cast), actionId: cast);
            else
                Cast(connection, monster, apLeft: 6 - (3 * cast), actionId: cast);
        }

        Assert.Equal([0, TestPlay.BlowSpellId], spells.Order());
        Assert.IsType<FightTurnFinishRequest>(await SentAsync(connection, sent + 5));
        connection.Server(new FightTurnEndEvent { FighterId = Self });
        connection.Server(new FightEndEvent { Results = { new FightResult { Fighter = new FightResultFighter { FighterId = Monster } } } });
        await bot.ProcessedAsync();
        await LoggedAsync($"Bot {Self}: ends its turn after 2 actions");

        Assert.DoesNotContain(_play.Log.GetSnapshot(), record => record.Message.Contains("did not show", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OutOfAnActivityTheBotLeavesTheFightToThePlayer()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var sent = connection.Sent.Count;

        Placement(connection, Beside(monster, -3, 0), monster);
        await bot.ProcessedAsync();
        _play.Time.Advance(BotDelays.Measured.Display + BotDelays.Measured.FightAction + BotDelays.Measured.Answer);
        await bot.ProcessedAsync();

        Assert.Equal(sent, connection.Sent.Count);
    }

    [Fact]
    public async Task ThePlayersCastPausesTheBotAndTheFightIsThePlayers()
    {
        var (connection, bot) = await _play.EnterAsync(Far);
        var monster = Beside(Far, 8, 0);
        var near = Beside(monster, -3, 0);
        var sent = connection.Sent.Count;

        bot.FightsAlone = true;
        Placement(connection, near, monster);
        await SentAsync(connection, sent + 1);

        // The player's hand during the placement: the bot pauses, and sends nothing more.
        connection.Client(new FightPlacementPositionRequest { CellId = near, FighterId = Self });
        await bot.ProcessedAsync();
        _play.Time.Advance(BotDelays.Measured.Answer + BotDelays.Measured.FightAction);
        await bot.ProcessedAsync();

        Assert.True(bot.IsPaused);
        Assert.Equal(sent + 1, connection.Sent.Count);
    }
}
