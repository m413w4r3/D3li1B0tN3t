using D3B.Bot.Characters;
using D3B.Bot.Network;
using D3B.Bot.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Tests.Runtime;

public sealed class BotsTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public async Task TheCharacterAClientChoosesGetsABotOfItsAccount()
    {
        var added = new List<BotActor>();

        _play.Bots.Added += (_, bot) => added.Add(bot);

        var connection = _play.Connect(TestPlay.Account);

        connection.Client(new CharacterSelectionRequest { CharacterId = TestPlay.CharacterId });
        Assert.Empty(_play.Bots.All);

        connection.Server(TestPlay.Chosen());

        var bot = Assert.Single(_play.Bots.All);

        await bot.ProcessedAsync();

        Assert.Equal(new CharacterKey(TestPlay.AccountId, TestPlay.CharacterId), bot.Key);
        Assert.Equal([bot], added);
        Assert.True(bot.IsConnected);
        Assert.Equal("Character", bot.State.Name);
    }

    [Fact]
    public async Task ABotSeesWhatItsCharactersConnectionCarries()
    {
        var (connection, bot) = await _play.EnterAsync(300);

        Assert.Equal(TestPlay.MapId, bot.State.MapId);
        Assert.Equal(300, bot.State.Cell);

        connection.Server(new KamasUpdateEvent { Kamas = 42 });
        await bot.ProcessedAsync();

        Assert.Equal(42, bot.State.Kamas);
    }

    [Fact]
    public void AConnectionWhoseAccountIsNotKnownGetsNoBot()
    {
        var connection = _play.Connect(null);

        connection.Server(TestPlay.Chosen());

        Assert.Empty(_play.Bots.All);
        Assert.Contains(_play.Log.GetSnapshot(), record => record.Level is LogLevel.Warning
            && record.Message == "Game connection g1: its account is not known, so no bot plays its character");
    }

    [Fact]
    public async Task ABotWhoseConnectionClosedKeepsItsState()
    {
        var (connection, bot) = await _play.EnterAsync(300);

        connection.Close();
        await bot.ProcessedAsync();

        Assert.False(bot.IsConnected);
        Assert.Equal(300, bot.State.Cell);
        Assert.Same(bot, Assert.Single(_play.Bots.All));
    }

    [Fact]
    public async Task TheCharacterChosenAgainTakesItsBotBackWithAStateFromTheNewConnection()
    {
        var (first, bot) = await _play.EnterAsync(300);

        first.Close();

        var second = _play.Connect(TestPlay.Account);

        second.Server(TestPlay.Chosen());
        await bot.ProcessedAsync();

        Assert.Same(bot, Assert.Single(_play.Bots.All));
        Assert.True(bot.IsConnected);
        Assert.Null(bot.State.MapId);

        // The closed connection's messages no longer reach the bot.
        first.Server(new MapCurrentEvent { MapId = 1 });
        second.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        await bot.ProcessedAsync();

        Assert.Equal(TestPlay.MapId, bot.State.MapId);
    }

    [Fact]
    public async Task AnotherCharacterOfTheAccountGetsABotOfItsOwn()
    {
        var (first, bot) = await _play.EnterAsync(300);

        first.Close();

        var second = _play.Connect(TestPlay.Account);

        second.Server(TestPlay.Chosen(80_002));

        Assert.Equal(2, _play.Bots.All.Count);
        Assert.True(_play.Bots.TryGet(new CharacterKey(TestPlay.AccountId, 80_002), out var other));
        Assert.NotSame(bot, other);
    }

    [Fact]
    public void AFailedChoiceGetsNoBot()
    {
        var connection = _play.Connect(TestPlay.Account);

        connection.Server(new CharacterSelectionEvent { Error = new CharacterSelectionEvent.Types.Error() });

        Assert.Empty(_play.Bots.All);
    }

    [Fact]
    public void TheAccountIsTheOneWhoseTicketTheConnectionPresented()
    {
        var connection = _play.Connect(new GameAccount(99, "other", "tag"));

        Assert.Equal(99, connection.Session.Account?.Id);
    }
}
