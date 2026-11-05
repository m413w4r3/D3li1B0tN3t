using System.Buffers;
using System.Diagnostics;
using D3B.Bot.Network;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Tests.Network;

public sealed class DiagnosticObserverTests : IDisposable
{
    private static readonly RelaySettings _settings = new()
    {
        Address = RelaySettings.DefaultAddress,
        Ports = RelaySettings.DefaultPorts,
        GamePorts = RelaySettings.DefaultGamePorts,
        ClientDirectory = "no-client",
        RedirectDirectory = "no-redirect",
    };

    private readonly FakeRelayConnection _connection = new("diagnostic", RelayRoute.Game);
    private readonly ILoggerFactory _logs = LoggerFactory.Create(_ => { });

    public void Dispose()
    {
        _connection.Dispose();
        _logs.Dispose();
    }

    [Fact]
    public void UnknownFramesCarryTheirTypePayloadAndDirection()
    {
        var factory = new FakeDiagnosticSessionFactory();
        var observer = new DiagnosticObserver(factory, false, _logs.CreateLogger<DiagnosticObserver>());
        var session = Start(observer);
        var payload = ByteString.CopyFrom(9, 8, 7);
        Show(session, RelayDirection.FromServer, UnknownFrame("opaque.Token", payload));

        var sink = Assert.IsType<FakeDiagnosticSink>(factory.LastSink);
        Assert.True(SpinWait.SpinUntil(() => sink.Frames.Count is 1, TimeSpan.FromSeconds(2)));
        var frame = Assert.Single(sink.Frames);
        Assert.Equal("server", frame.Direction);
        Assert.Equal("type.ankama.com/opaque.Token", frame.TypeUrl);
        Assert.Equal("opaque.Token", frame.CarriedType);
        Assert.Equal(payload.ToByteArray(), frame.Payload);
    }

    [Fact]
    public void ReadableFramesAreIgnoredUnlessConfigured()
    {
        var ignoredFactory = new FakeDiagnosticSessionFactory();
        var ignored = Start(new DiagnosticObserver(ignoredFactory, false, _logs.CreateLogger<DiagnosticObserver>()));
        Show(ignored, RelayDirection.FromClient, TestGame.Request(new AuthenticationRequest { Ticket = "ticket" }));
        Assert.Empty(Assert.IsType<FakeDiagnosticSink>(ignoredFactory.LastSink).Frames);

        var includedFactory = new FakeDiagnosticSessionFactory();
        var included = Start(new DiagnosticObserver(includedFactory, true, _logs.CreateLogger<DiagnosticObserver>()));
        Show(included, RelayDirection.FromClient, TestGame.Request(new AuthenticationRequest { Ticket = "ticket" }));
        var includedSink = Assert.IsType<FakeDiagnosticSink>(includedFactory.LastSink);
        Assert.True(SpinWait.SpinUntil(() => includedSink.Frames.Count is 1, TimeSpan.FromSeconds(2)));
        Assert.Equal("client", Assert.Single(includedSink.Frames).Direction);
    }

    [Fact]
    public void AThrowingSinkDoesNotEscapeTheObserver()
    {
        var factory = new FakeDiagnosticSessionFactory { Create = () => new ThrowingDiagnosticSink() };
        var session = Start(new DiagnosticObserver(factory, false, _logs.CreateLogger<DiagnosticObserver>()));

        var exception = Record.Exception(() => Show(session, RelayDirection.FromServer, UnknownFrame("opaque.Token", ByteString.CopyFrom(1))));

        Assert.Null(exception);
    }

    [Fact]
    public void ABlockingSinkDoesNotDelayRelayForwardingAndQueueDropsAreReported()
    {
        var sink = new BlockingDiagnosticSink();
        var factory = new FakeDiagnosticSessionFactory { Create = () => sink };
        var observer = new DiagnosticObserver(factory, false, _logs.CreateLogger<DiagnosticObserver>(), 1);
        var session = Start(observer);
        var bytes = UnknownFrame("opaque.Token", ByteString.CopyFrom(1));
        var timer = Stopwatch.StartNew();

        Show(session, RelayDirection.FromServer, bytes);
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Show(session, RelayDirection.FromServer, bytes);
        Show(session, RelayDirection.FromServer, bytes);

        Assert.True(session.IsMovementTranslationBlocked);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(1));
        sink.Release.Set();
        Assert.True(SpinWait.SpinUntil(() => sink.DroppedFrames is 1, TimeSpan.FromSeconds(2)));
    }

    private GameSession Start(IGameObserver observer)
    {
        var relay = new RelayDecoder(
            TestGame.Translation(),
            new GameServerSelections(_settings, TimeProvider.System),
            new GameAccounts(_settings.SelectionLifetime, TimeProvider.System),
            [observer],
            _logs);
        return Assert.IsType<GameSession>(relay.Intercept(_connection));
    }

    private static void Show(GameSession session, RelayDirection direction, byte[] bytes)
    {
        var interceptor = (IConnectionInterceptor)session;
        Assert.Null(interceptor.OnFrame(direction, new ReadOnlySequence<byte>(bytes), interceptor.Holds(direction)));
    }

    private static byte[] UnknownFrame(string type, ByteString payload)
    {
        return new GameMessage
        {
            Event = new GameEvent
            {
                Content = new Any { TypeUrl = "type.ankama.com/" + type, Value = payload },
            },
        }.ToByteArray();
    }

}
