using System.Buffers;
using D3B.Bot.Network;
using D3B.Network.Relay;
using D3B.Protocol.Connection;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Bot.Tests.Network;

public sealed class RelayDecoderTests
{
    private const string Secret = "secret";

    private static readonly RelaySettings _settings = new()
    {
        Address = RelaySettings.DefaultAddress,
        Ports = RelaySettings.DefaultPorts,
        GamePorts = RelaySettings.DefaultGamePorts,
        ClientDirectory = "no-client",
        RedirectDirectory = "no-redirect",
    };

    private readonly FakeLogCollector _log = new();
    private readonly GameAccounts _accounts = new(_settings.SelectionLifetime, TimeProvider.System);
    private readonly ILoggerFactory _logs;
    private readonly ProtocolTranslation _translation;
    private readonly RelayDecoder _decoder;

    public RelayDecoderTests()
    {
        var (schema, mapping) = TestInstall.Protocol();
        _translation = ProtocolTranslation.Create(ProtocolSchema.Load(schema), ProtocolMapping.Parser.ParseFrom(mapping));
        _logs = LoggerFactory.Create(logging => logging.AddProvider(new FakeLoggerProvider(_log)));
        _decoder = new RelayDecoder(_translation, new GameServerSelections(_settings, TimeProvider.System), _accounts, [], _logs);
    }

    private IConnectionInterceptor Intercept(RelayRoute route)
    {
        return _decoder.Intercept(new FakeRelayConnection("c1", route))!;
    }

    /// <summary>A connection phase as session 3b captured one, every text holding what must never reach a log.</summary>
    private static IEnumerable<(RelayDirection Direction, ConnectionMessage Message)> ConnectionPhase(string subscriptionEnd = "2026-11-02T08:00:00+01:00")
    {
        yield return (RelayDirection.FromClient, new ConnectionMessage
        {
            Request = new ConnectionRequest
            {
                Id = "a",
                Identification = new IdentificationRequest { DeviceId = Secret, Token = new TokenCredentials { Token = Secret }, ClientVersion = Secret },
            },
        });
        yield return (RelayDirection.FromServer, new ConnectionMessage
        {
            Response = new ConnectionResponse
            {
                Id = "a",
                Identification = new IdentificationResponse
                {
                    Success = new IdentificationResponse.Types.Success { AccountId = 1, AccountNickname = Secret, SubscriptionEnd = subscriptionEnd },
                },
            },
        });
        yield return (RelayDirection.FromClient, new ConnectionMessage { Request = new ConnectionRequest { Id = "b", ServerSelection = new ServerSelectionRequest { ServerId = 401 } } });
        yield return (RelayDirection.FromServer, new ConnectionMessage
        {
            Response = new ConnectionResponse
            {
                Id = "b",
                ServerSelection = new ServerSelectionResponse
                {
                    Success = new ServerSelectionResponse.Types.Success { Ticket = Secret, Host = Secret + ".test", Ports = { 5555, 443 } },
                },
            },
        });
    }

    [Fact]
    public void EachFrameIsLoggedByWhatItIsNeverByWhatItHolds()
    {
        var phase = ConnectionPhase().ToList();
        var session = Intercept(RelayRoute.Connection);

        foreach (var (direction, message) in phase)
            session.OnFrame(direction, new ReadOnlySequence<byte>(message.ToByteArray()), session.Holds(direction));

        var lengths = phase.Select(frame => frame.Message.CalculateSize()).ToList();

        Assert.Equal(
            [
                $"Connection c1: the client sent request.identification.token, {lengths[0]} bytes",
                $"Connection c1: the server sent response.identification.success, {lengths[1]} bytes",
                $"Connection c1: the client sent request.server_selection, {lengths[2]} bytes",
                $"Connection c1: the server sent response.server_selection.success, {lengths[3]} bytes",
                "Connection c1: the server selection's answer now sends the client to D3B's game listener, 127.0.13.59 on ports 5556",
            ],
            _log.GetSnapshot().Select(record => record.Message));
        Assert.All(_log.GetSnapshot(), record => Assert.Equal(LogLevel.Information, record.Level));
        Assert.DoesNotContain(_log.GetSnapshot(), record => record.Message.Contains(Secret, StringComparison.Ordinal)
            || record.StructuredState!.Any(pair => pair.Value?.Contains(Secret, StringComparison.Ordinal) is true));
    }

    [Fact]
    public void AnEventIsNamedByItsField()
    {
        var message = new ConnectionMessage { Event = new ConnectionEvent { ServerUpdate = new ServerUpdate { Server = new ServerInformation() } } };

        Assert.Equal("event.server_update", RelayDecoder.Describe(message));
    }

    [Fact]
    public void AFrameNotUnderstoodIsAWarning()
    {
        // Field 15 of the root, a varint: no readable message has it.
        byte[] bytes = [.. new ConnectionMessage { Request = new ConnectionRequest { Id = "a", Ping = new Ping() } }.ToByteArray(), 15 << 3, 1];

        Intercept(RelayRoute.Connection).OnFrame(RelayDirection.FromClient, new ReadOnlySequence<byte>(bytes), held: false);

        var record = Assert.Single(_log.GetSnapshot());

        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.StartsWith($"Connection c1: the client sent request.ping, {bytes.Length} bytes, not understood and relayed as it is: ", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BytesThatAreNoMessageAreAWarning()
    {
        Intercept(RelayRoute.Connection).OnFrame(RelayDirection.FromServer, new ReadOnlySequence<byte>([0x12, 0x7F, 0x01]), held: false);

        var record = Assert.Single(_log.GetSnapshot());

        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.StartsWith("Connection c1: the server sent no message, 3 bytes, not understood", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSelectionsAnswerIsRewrittenToTheGameListenerItsTicketKept()
    {
        var session = Intercept(RelayRoute.Connection);
        var rewritten = new List<byte[]?>();

        foreach (var (direction, message) in ConnectionPhase())
            rewritten.Add(session.OnFrame(direction, new ReadOnlySequence<byte>(message.ToByteArray()), session.Holds(direction)));

        Assert.All(rewritten[..3], Assert.Null);

        var answer = ConnectionMessage.Parser.ParseFrom(rewritten[3]).Response;

        Assert.Equal("b", answer.Id);
        Assert.Equal(Secret, answer.ServerSelection.Success.Ticket);
        Assert.Equal("127.0.13.59", answer.ServerSelection.Success.Host);
        Assert.Equal([5556], answer.ServerSelection.Success.Ports);
    }

    [Fact]
    public void TheServersFramesAreHeldFromTheClientsServerSelectionToItsAnswer()
    {
        var session = Intercept(RelayRoute.Connection);
        var holds = new List<bool>();

        foreach (var (direction, message) in ConnectionPhase())
        {
            holds.Add(session.Holds(RelayDirection.FromServer));
            session.OnFrame(direction, new ReadOnlySequence<byte>(message.ToByteArray()), session.Holds(direction));
            Assert.False(session.Holds(RelayDirection.FromClient));
        }

        holds.Add(session.Holds(RelayDirection.FromServer));

        Assert.Equal([false, false, false, true, false], holds);
    }

    [Fact]
    public void AnAnswerTheRelayCouldNotHoldGoesOnAsItCame()
    {
        var session = Intercept(RelayRoute.Connection);
        var phase = ConnectionPhase().ToList();

        session.OnFrame(phase[2].Direction, new ReadOnlySequence<byte>(phase[2].Message.ToByteArray()), held: false);

        Assert.Null(session.OnFrame(phase[3].Direction, new ReadOnlySequence<byte>(phase[3].Message.ToByteArray()), held: false));
        Assert.Contains(_log.GetSnapshot(), record => record.Level is LogLevel.Warning
            && record.Message == "Connection c1: the server selection's answer goes on as it came, and the game connection straight to the game server: the relay could not hold it whole");
    }

    [Fact]
    public void AnErrorAnswerGoesOnAsItCameAndEndsTheHold()
    {
        var session = Intercept(RelayRoute.Connection);
        var error = new ConnectionMessage
        {
            Response = new ConnectionResponse { Id = "b", ServerSelection = new ServerSelectionResponse { Error = ServerSelectionResponse.Types.Error.Value1 } },
        };

        session.OnFrame(RelayDirection.FromClient, new ReadOnlySequence<byte>(ConnectionPhase().ElementAt(2).Message.ToByteArray()), held: false);

        Assert.Null(session.OnFrame(RelayDirection.FromServer, new ReadOnlySequence<byte>(error.ToByteArray()), held: true));
        Assert.False(session.Holds(RelayDirection.FromServer));
    }

    [Fact]
    public void AnAnswerNotUnderstoodWholeIsNotRewritten()
    {
        var session = Intercept(RelayRoute.Connection);
        var phase = ConnectionPhase().ToList();

        // Field 15 of the root, a varint: no readable message has it, so rewriting would lose it.
        byte[] answer = [.. phase[3].Message.ToByteArray(), 15 << 3, 1];

        session.OnFrame(phase[2].Direction, new ReadOnlySequence<byte>(phase[2].Message.ToByteArray()), held: false);

        Assert.Null(session.OnFrame(RelayDirection.FromServer, new ReadOnlySequence<byte>(answer), held: true));
        Assert.DoesNotContain(_log.GetSnapshot(), record => record.Message.Contains("now sends the client", StringComparison.Ordinal));
    }

    [Fact]
    public void AHoldTheRelayAbandonedIsAWarning()
    {
        var session = Intercept(RelayRoute.Connection);

        session.OnFrame(RelayDirection.FromClient, new ReadOnlySequence<byte>(ConnectionPhase().ElementAt(2).Message.ToByteArray()), held: false);
        session.OnHoldAbandoned(RelayDirection.FromServer, "it was not whole after 5 s");

        Assert.False(session.Holds(RelayDirection.FromServer));
        Assert.Contains(_log.GetSnapshot(), record => record.Level is LogLevel.Warning
            && record.Message.EndsWith("straight to the game server: the relay held the server's frame, but it was not whole after 5 s", StringComparison.Ordinal));
    }

    [Fact]
    public void TheGameConnectionIsReadByAGameSession()
    {
        Assert.IsType<GameSession>(Intercept(RelayRoute.Game));
    }

    [Fact]
    public void AGameSessionKnowsTheAccountTheConnectionPhaseIdentifiedByTheTicketItPresents()
    {
        var phase = Intercept(RelayRoute.Connection);

        foreach (var (direction, message) in ConnectionPhase())
            phase.OnFrame(direction, new ReadOnlySequence<byte>(message.ToByteArray()), phase.Holds(direction));

        var game = (GameSession)Intercept(RelayRoute.Game);
        var other = (GameSession)Intercept(RelayRoute.Game);

        Assert.Null(game.Account);

        Present(other, "another-ticket");
        Present(game, Secret);

        Assert.Equal(new GameAccount(1, Secret, "", new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(1))), game.Account);
        Assert.Null(other.Account);
    }

    [Theory]
    [InlineData("")]
    [InlineData("never")]
    public void AnAccountWhoseSubscriptionEndIsNoDateHasNone(string subscriptionEnd)
    {
        var phase = Intercept(RelayRoute.Connection);

        foreach (var (direction, message) in ConnectionPhase(subscriptionEnd))
            phase.OnFrame(direction, new ReadOnlySequence<byte>(message.ToByteArray()), phase.Holds(direction));

        var game = (GameSession)Intercept(RelayRoute.Game);

        Present(game, Secret);

        Assert.Null(game.Account?.SubscriptionEnd);
        Assert.Equal(1, game.Account?.Id);
    }

    [Fact]
    public void AnAccountIsTakenByOneGameSessionOnly()
    {
        var phase = Intercept(RelayRoute.Connection);

        foreach (var (direction, message) in ConnectionPhase())
            phase.OnFrame(direction, new ReadOnlySequence<byte>(message.ToByteArray()), phase.Holds(direction));

        var first = (GameSession)Intercept(RelayRoute.Game);
        var second = (GameSession)Intercept(RelayRoute.Game);

        Present(first, Secret);
        Present(second, Secret);

        Assert.NotNull(first.Account);
        Assert.Null(second.Account);
    }

    private static void Present(IConnectionInterceptor session, string ticket)
    {
        var request = TestGame.Request(new D3B.Protocol.Game.AuthenticationRequest { Ticket = ticket, Language = "fr" });

        session.OnFrame(RelayDirection.FromClient, new ReadOnlySequence<byte>(request), held: false);
    }
}
