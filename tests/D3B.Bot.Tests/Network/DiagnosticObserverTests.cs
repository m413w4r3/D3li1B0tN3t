using System.Buffers;
using System.Diagnostics;
using D3B.Bot.Network;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Ipc;
using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Reporting;
using D3B.Diagnostic.Replay;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.DependencyInjection;

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
        var observer = CreateObserver(factory, false);
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
        var ignored = Start(CreateObserver(ignoredFactory, false));
        Show(ignored, RelayDirection.FromClient, TestGame.Request(new AuthenticationRequest { Ticket = "ticket" }));
        Assert.Empty(Assert.IsType<FakeDiagnosticSink>(ignoredFactory.LastSink).Frames);

        var includedFactory = new FakeDiagnosticSessionFactory();
        var included = Start(CreateObserver(includedFactory, true));
        Show(included, RelayDirection.FromClient, TestGame.Request(new AuthenticationRequest { Ticket = "ticket" }));
        var includedSink = Assert.IsType<FakeDiagnosticSink>(includedFactory.LastSink);
        Assert.True(SpinWait.SpinUntil(() => includedSink.Frames.Count is 1, TimeSpan.FromSeconds(2)));
        Assert.Equal("client", Assert.Single(includedSink.Frames).Direction);
    }

    [Fact]
    public void EnablingReadablePayloadsLogsSensitiveDataWarningOnce()
    {
        var log = new FakeLogCollector();
        var observer = new DiagnosticObserver(
            new FakeDiagnosticSessionFactory(),
            true,
            new FakeLogger<DiagnosticObserver>(log),
            new HashSet<string>(StringComparer.Ordinal)
            {
                Any.GetTypeName(TestGame.TypeUrl(new AuthenticationRequest())),
            });

        _ = observer;
        var records = log.GetSnapshot();
        Assert.Single(records);
        Assert.Contains("may contain sensitive data", records[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AThrowingSinkDoesNotEscapeTheObserver()
    {
        var factory = new FakeDiagnosticSessionFactory { Create = () => new ThrowingDiagnosticSink() };
        var session = Start(CreateObserver(factory, false));

        var exception = Record.Exception(() => Show(session, RelayDirection.FromServer, UnknownFrame("opaque.Token", ByteString.CopyFrom(1))));

        Assert.Null(exception);
    }

    [Fact]
    public void ABlockingSinkDoesNotDelayRelayForwardingAndQueueDropsAreReported()
    {
        var sink = new BlockingDiagnosticSink();
        var factory = new FakeDiagnosticSessionFactory { Create = () => sink };
        var observer = CreateObserver(factory, false, 1);
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

    [Fact]
    public void PeerControlledOrForeignTypesAreNeverEnqueued()
    {
        var factory = new FakeDiagnosticSessionFactory();
        var session = Start(CreateObserver(factory, true));
        Show(session, RelayDirection.FromServer, UnknownFrame("123456", ByteString.CopyFrom(1)));
        Show(session, RelayDirection.FromServer, ForeignFrame("https://foreign.invalid/opaque.Token", ByteString.CopyFrom(2)));

        Assert.Empty(Assert.IsType<FakeDiagnosticSink>(factory.LastSink).Frames);
    }

    [Fact]
    public void OversizedPayloadIsDroppedBeforeItCanBeCopied()
    {
        var factory = new FakeDiagnosticSessionFactory();
        var session = Start(CreateObserver(factory, false));
        Show(session, RelayDirection.FromServer, UnknownFrame("opaque.Token", ByteString.CopyFrom(new byte[DiagnosticProtocol.FramePayloadLimit + 1])));

        Assert.Empty(Assert.IsType<FakeDiagnosticSink>(factory.LastSink).Frames);
    }

    [Fact]
    public void ObserverQueueByteBudgetDropsFramesBeyondItsLimit()
    {
        var sink = new BlockingDiagnosticSink();
        var factory = new FakeDiagnosticSessionFactory { Create = () => sink };
        var session = Start(CreateObserver(factory, false, queueCapacity: 10, queueByteCapacity: 4));
        var bytes = UnknownFrame("opaque.Token", ByteString.CopyFrom(1, 2, 3));

        Show(session, RelayDirection.FromServer, bytes);
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        Show(session, RelayDirection.FromServer, bytes);
        Show(session, RelayDirection.FromServer, bytes);

        sink.Release.Set();
        Assert.True(SpinWait.SpinUntil(() => sink.DroppedFrames is 1, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task DisposeAsyncAwaitsSinkDisposeAsync()
    {
        var sink = new FakeDiagnosticSink { BlockDisposeAsync = true };
        var factory = new FakeDiagnosticSessionFactory { Create = () => sink };
        var observer = CreateObserver(factory, false);
        _ = Start(observer);

        Task dispose = observer.DisposeAsync().AsTask();
        await sink.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.False(dispose.IsCompleted);
        sink.DisposeRelease.TrySetResult();
        await dispose.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(1, sink.DisposeCount);
    }

    [Fact]
    public async Task DisposeFlushesSynchronously()
    {
        var sink = new FakeDiagnosticSink { BlockDisposeAsync = true };
        var observer = CreateObserver(new FakeDiagnosticSessionFactory { Create = () => sink }, false);
        _ = Start(observer);

        Task dispose = Task.Run(observer.Dispose, TestContext.Current.CancellationToken);
        await sink.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.False(dispose.IsCompleted);
        sink.DisposeRelease.TrySetResult();
        await dispose.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(1, sink.DisposeCount);
    }

    [Fact]
    public async Task DoubleDisposalIsHarmless()
    {
        var sink = new FakeDiagnosticSink();
        var observer = CreateObserver(new FakeDiagnosticSessionFactory { Create = () => sink }, false);
        _ = Start(observer);

        await observer.DisposeAsync();
        observer.Dispose();
        await observer.DisposeAsync();

        Assert.Equal(1, sink.DisposeCount);
    }

    [Fact]
    public void AServiceProviderCanDisposeTheObserverSynchronously()
    {
        var sink = new FakeDiagnosticSink();
        var factory = new FakeDiagnosticSessionFactory { Create = () => sink };
        var services = new ServiceCollection();
        services.AddSingleton<DiagnosticObserver>(_ => CreateObserver(factory, false));
        services.AddSingleton<IGameObserver>(provider => provider.GetRequiredService<DiagnosticObserver>());
        ServiceProvider provider = services.BuildServiceProvider();
        _ = Start(provider.GetRequiredService<DiagnosticObserver>());

        Exception? exception = Record.Exception(provider.Dispose);

        Assert.Null(exception);
        Assert.Equal(1, sink.DisposeCount);
    }

    [Fact]
    public async Task OnFramePathDoesNotWaitForAReportWriterFlush()
    {
        var firstSink = new FakeDiagnosticSink { BlockDisposeAsync = true };
        var secondSink = new FakeDiagnosticSink();
        int created = 0;
        var factory = new FakeDiagnosticSessionFactory
        {
            Create = () => Interlocked.Increment(ref created) == 1 ? firstSink : secondSink,
        };
        var observer = CreateObserver(factory, false);
        var firstSession = Start(observer);
        Show(firstSession, RelayDirection.FromServer, UnknownFrame("opaque.Token", ByteString.CopyFrom(1)));
        _connection.Close();
        await firstSink.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        using var secondConnection = new FakeRelayConnection("diagnostic-second", RelayRoute.Game);
        var secondSession = Start(observer, secondConnection);
        var timer = Stopwatch.StartNew();
        Show(secondSession, RelayDirection.FromServer, UnknownFrame("opaque.Token", ByteString.CopyFrom(2)));
        timer.Stop();

        firstSink.DisposeRelease.TrySetResult();
        await observer.DisposeAsync();
        Assert.True(timer.Elapsed < TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public async Task ObserverShutdownWaitsForReportWriterBeforeCompleting()
    {
        var writer = new BlockingReportWriter();
        var schema = new DiagnosticSchema
        {
            ClientVersion = "3.7.13.17",
            SchemaSha256 = new string('a', 64),
            Messages = [new DiagnosticMessage { Name = "opaque.Token" }],
        };
        var sidecar = new ObserverTestSidecar();
        var sessions = new DiagnosticSink(
            new SidecarOptions
            {
                EnginePath = "unused",
                SocketDirectory = "unused",
                QueueCapacity = 8,
                QueueByteCapacity = 1_048_576,
                HandshakeTimeout = TimeSpan.FromSeconds(2),
                ResponseTimeout = TimeSpan.FromSeconds(2),
            },
            schema,
            new ObserverTestSidecarFactory(sidecar),
            _logs,
            new DiagnosticReportOptions { Enabled = true },
            writer,
            TimeProvider.System);
        var observer = CreateObserver(sessions, false);
        GameSession session = Start(observer);
        Show(session, RelayDirection.FromServer, UnknownFrame("opaque.Token", ByteString.CopyFrom(1)));

        Task shutdown = observer.DisposeAsync().AsTask();
        await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.False(shutdown.IsCompleted);
        writer.Release.TrySetResult();
        await shutdown.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.Equal(1, writer.Calls);
    }

    private DiagnosticObserver CreateObserver(IDiagnosticSessionFactory factory, bool includeReadable, int queueCapacity = 256, long queueByteCapacity = 16L * 1024 * 1024)
    {
        IReadOnlySet<string> messageNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "opaque.Token",
            Any.GetTypeName(TestGame.TypeUrl(new AuthenticationRequest())),
        };
        return new DiagnosticObserver(factory, includeReadable, _logs.CreateLogger<DiagnosticObserver>(), messageNames, queueCapacity, queueByteCapacity);
    }

    private GameSession Start(IGameObserver observer)
    {
        return Start(observer, _connection);
    }

    private GameSession Start(IGameObserver observer, IRelayConnection connection)
    {
        var relay = new RelayDecoder(
            TestGame.Translation(),
            new GameServerSelections(_settings, TimeProvider.System),
            new GameAccounts(_settings.SelectionLifetime, TimeProvider.System),
            [observer],
            _logs);
        return Assert.IsType<GameSession>(relay.Intercept(connection));
    }

    private static void Show(GameSession session, RelayDirection direction, byte[] bytes)
    {
        var interceptor = (IConnectionInterceptor)session;
        Assert.Null(interceptor.OnFrame(direction, new ReadOnlySequence<byte>(bytes), interceptor.Holds(direction)));
    }

    private static byte[] UnknownFrame(string type, ByteString payload)
    {
        return ForeignFrame("type.ankama.com/" + type, payload);
    }

    private static byte[] ForeignFrame(string typeUrl, ByteString payload)
    {
        return new GameMessage
        {
            Event = new GameEvent
            {
                Content = new Any { TypeUrl = typeUrl, Value = payload },
            },
        }.ToByteArray();
    }

    private sealed class ObserverTestSidecarFactory : IDiagnosticSidecarFactory
    {
        private readonly IDiagnosticSidecar _sidecar;

        public ObserverTestSidecarFactory(IDiagnosticSidecar sidecar)
        {
            _sidecar = sidecar;
        }

        public Task<SidecarStartResult> StartAsync(SidecarOptions options, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SidecarStartResult(_sidecar, null));
        }
    }

    private sealed class ObserverTestSidecar : IDiagnosticSidecar
    {
        public Task<DiagnosticEngineHello> StartAsync(DiagnosticSchema schema, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DiagnosticEngineHello
            {
                ProtocolVersion = DiagnosticProtocol.ProtocolVersion,
                Engine = "observer-test",
                EngineVersion = "1.0",
            });
        }

        public Task<DiagnosticAnalysis> AnalyzeAsync(DiagnosticFrame frame, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DiagnosticAnalysis
            {
                Index = frame.Index,
                Direction = frame.Direction,
                CarriedType = frame.CarriedType,
                Message = frame.CarriedType,
                Status = DiagnosticStatus.Valid,
            });
        }

        public Task<DiagnosticSummary> CompleteAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DiagnosticSummary { Frames = 1, Coverage = "complete" });
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingReportWriter : IDiagnosticReportWriter
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls;

        public async ValueTask WriteAsync(DiagnosticReport report, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

}
