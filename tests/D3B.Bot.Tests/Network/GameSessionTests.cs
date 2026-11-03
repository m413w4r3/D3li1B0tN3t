using System.Buffers;
using D3B.Bot.Network;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Bot.Tests.Network;

public sealed class GameSessionTests : IDisposable
{
    private const string Secret = "secret-ticket-0123456789abcdef01";

    private static readonly RelaySettings _settings = new()
    {
        Address = RelaySettings.DefaultAddress,
        Ports = RelaySettings.DefaultPorts,
        GamePorts = RelaySettings.DefaultGamePorts,
        ClientDirectory = "no-client",
        RedirectDirectory = "no-redirect",
    };

    private readonly ProtocolTranslation _translation = TestGame.Translation();
    private readonly FakeLogCollector _log = new();
    private readonly ILoggerFactory _logs;
    private readonly FakeRelayConnection _connection = new("g1", RelayRoute.Game);

    public GameSessionTests()
    {
        _logs = LoggerFactory.Create(logging => logging.AddProvider(new FakeLoggerProvider(_log)).SetMinimumLevel(LogLevel.Debug));
    }

    public void Dispose()
    {
        _logs.Dispose();
        _connection.Dispose();
    }

    private IConnectionInterceptor Start(params IGameObserver[] observers)
    {
        return new RelayDecoder(_translation, new GameServerSelections(_settings, TimeProvider.System), new GameAccounts(_settings.SelectionLifetime, TimeProvider.System), observers, _logs).Intercept(_connection)!;
    }

    private static void Show(IConnectionInterceptor session, RelayDirection direction, byte[] frame)
    {
        Assert.Null(session.OnFrame(direction, new ReadOnlySequence<byte>(frame), session.Holds(direction)));
    }

    private static byte[] UnknownFrame(string carriedType, string payload)
    {
        return new GameMessage
        {
            Event = new GameEvent
            {
                Content = new Google.Protobuf.WellKnownTypes.Any
                {
                    TypeUrl = "type.ankama.com/" + carriedType,
                    Value = ByteString.CopyFromUtf8(payload),
                },
            },
        }.ToByteArray();
    }

    private static byte[] PartialMapInformationFrame()
    {
        var envelope = GameMessage.Parser.ParseFrom(TestGame.Event(new MapInformationEvent { MapId = 188744706 }));
        var original = envelope.Event.Content.Value.ToByteArray();

        // Field 20 is present in this build's partial message and is intentionally absent from the readable contract.
        envelope.Event.Content.Value = ByteString.CopyFrom([.. original, 0xA0, 0x01, 0x01]);

        return envelope.ToByteArray();
    }

    [Fact]
    public void EachFrameIsShownToTheObserversDecoded()
    {
        var observer = new RecordingObserver();
        var session = Start(observer);
        var current = new MapCurrentEvent { MapId = 188744706 };

        Show(session, RelayDirection.FromClient, TestGame.Request(new AuthenticationRequest { Ticket = Secret, Language = "fr" }));
        Show(session, RelayDirection.FromServer, TestGame.Event(current));

        Assert.Same(session, Assert.Single(observer.Sessions));
        Assert.Equal(
            [(RelayDirection.FromClient, "AuthenticationRequest"), (RelayDirection.FromServer, "MapCurrentEvent")],
            observer.Frames.Select(frame => (frame.Direction, frame.Frame.ReadableType!.Name)));
        Assert.Equal(current, observer.Frames[1].Frame.Content);
    }

    [Fact]
    public void FramesAreLoggedAtTheDebugLevelByWhatTheyAreNeverByWhatTheyHold()
    {
        var session = Start();
        var ticket = TestGame.Request(new AuthenticationRequest { Ticket = Secret, Language = "fr" });
        var unknown = UnknownFrame("zzz", Secret);

        Show(session, RelayDirection.FromClient, ticket);
        Show(session, RelayDirection.FromServer, unknown);

        var records = _log.GetSnapshot();

        Assert.Equal(
            [
                $"Connection g1: the client sent AuthenticationRequest, {ticket.Length} bytes, understood",
                $"Connection g1: the server sent .zzz, {unknown.Length} bytes, not read",
            ],
            records.Where(record => record.Level is LogLevel.Debug).Select(record => record.Message));

        var warning = Assert.Single(records, record => record.Level is LogLevel.Warning);

        Assert.Contains("not understood and relayed as it is", warning.Message, StringComparison.Ordinal);
        Assert.Contains("readable protocol lacks the carried type", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(records, record => record.Message.Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public void ARepeatedPartialFrameLogsInformationOnceAndThenACountedDebug()
    {
        var session = Start();
        var partial = PartialMapInformationFrame();

        Show(session, RelayDirection.FromServer, partial);
        Show(session, RelayDirection.FromServer, partial);

        var records = _log.GetSnapshot();
        var diagnostics = records.Where(record => record.Message.Contains("partial readable", StringComparison.Ordinal)).ToArray();

        Assert.Equal([LogLevel.Information, LogLevel.Debug], diagnostics.Select(record => record.Level));
        Assert.Contains("seen 2 times", diagnostics[1].Message, StringComparison.Ordinal);
        Assert.DoesNotContain(records, record => record.Level is LogLevel.Warning);
    }

    [Fact]
    public void ARepeatedUnknownFrameWarnsOnceAndThenLogsACountedDebugWithoutItsPayload()
    {
        var session = Start();
        var unknown = UnknownFrame("zzz", Secret);

        Show(session, RelayDirection.FromServer, unknown);
        Show(session, RelayDirection.FromServer, unknown);

        var records = _log.GetSnapshot();
        var diagnostics = records.Where(record => record.Message.Contains("not understood and relayed as it is", StringComparison.Ordinal)
            || record.Message.Contains("repeated unread translation problem", StringComparison.Ordinal)).ToArray();

        Assert.Equal([LogLevel.Warning, LogLevel.Debug], diagnostics.Select(record => record.Level));
        Assert.Contains("seen 2 times", diagnostics[1].Message, StringComparison.Ordinal);
        Assert.DoesNotContain(records, record => record.Message.Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public void DistinctUnknownTranslationProblemsEachWarnOnce()
    {
        var session = Start();

        Show(session, RelayDirection.FromServer, UnknownFrame("zzz", Secret));
        Show(session, RelayDirection.FromServer, UnknownFrame("yyy", Secret));

        var records = _log.GetSnapshot();
        var warnings = records.Where(record => record.Level is LogLevel.Warning).ToArray();

        Assert.Equal(2, warnings.Length);
        Assert.Contains(warnings, record => record.Message.Contains(".zzz", StringComparison.Ordinal));
        Assert.Contains(warnings, record => record.Message.Contains(".yyy", StringComparison.Ordinal));
        Assert.DoesNotContain(records, record => record.Message.Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public void NoGameFrameIsHeldOrRewritten()
    {
        var session = Start();

        Assert.False(session.Holds(RelayDirection.FromClient));
        Assert.False(session.Holds(RelayDirection.FromServer));
        Show(session, RelayDirection.FromServer, TestGame.Event(new MapCurrentEvent { MapId = 1 }));
    }

    [Fact]
    public void AFailingObserverIsLoggedAndTheOthersStillSeeTheFrames()
    {
        var observer = new RecordingObserver();
        var session = Start(new RecordingObserver { Fails = true }, observer);

        Show(session, RelayDirection.FromServer, TestGame.Event(new MapCurrentEvent { MapId = 1 }));

        Assert.Single(observer.Frames);
        Assert.Equal(2, _log.GetSnapshot().Count(record => record.Level is LogLevel.Error && record.Message.Contains("the game observer", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ARequestGoesToTheServerAsTheClientSendsOne()
    {
        var observer = new RecordingObserver();

        Start(observer);

        var request = new DialogLeaveRequest();

        await observer.Sessions[0].SendAsync(request, TestContext.Current.CancellationToken);

        var envelope = GameMessage.Parser.ParseFrom(Assert.Single(_connection.Sent));

        Assert.Equal(ProtocolTranslation.UncountedRequestId, envelope.Request.Id);
        Assert.Equal(TestGame.TypeUrl(request), envelope.Request.Content.TypeUrl);
        Assert.Equal(request, DialogLeaveRequest.Parser.ParseFrom(envelope.Request.Content.Value));
        Assert.Contains(_log.GetSnapshot(), record => record.Level is LogLevel.Information && record.Message == $"Connection g1: D3B sent DialogLeaveRequest, {_connection.Sent[0].Length} bytes");
    }

    [Fact]
    public async Task AnUnboundSessionBlocksEveryMovementCapableRequest()
    {
        var observer = new RecordingObserver();

        Start(observer);

        IMessage[] requests =
        [
            new MapMovementRequest { MapId = 1 },
            new MapChangeRequest { MapId = 1 },
            new TeleportRequest { MapId = 1 },
            new HavenBagEnterRequest { OwnerId = 1 },
            new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 1 },
            new NpcActionRequest { Action = 3, MapId = 1, NpcActorId = -1 },
            new NpcDialogReplyRequest { ReplyId = 1 },
        ];

        foreach (var request in requests)
            await Assert.ThrowsAsync<InvalidOperationException>(() => observer.Sessions[0].SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Empty(_connection.Sent);
    }

    [Fact]
    public async Task APartialMessageIsNeverSent()
    {
        var observer = new RecordingObserver();

        Start(observer);

        await Assert.ThrowsAsync<ArgumentException>(() => observer.Sessions[0].SendAsync(new MapInformationEvent(), TestContext.Current.CancellationToken));
        Assert.Empty(_connection.Sent);
    }

    [Fact]
    public async Task ARequestSentOnceTheConnectionClosedFails()
    {
        var observer = new RecordingObserver();

        Start(observer);
        _connection.Close();

        Assert.True(observer.Sessions[0].Closed.IsCancellationRequested);
        await Assert.ThrowsAsync<RelayInjectionException>(() => observer.Sessions[0].SendAsync(new DialogLeaveRequest(), TestContext.Current.CancellationToken));
    }
}
