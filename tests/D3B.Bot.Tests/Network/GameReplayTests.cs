using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using D3B.Bot.Network;
using D3B.Data.Tests;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Network.Relay;
using D3B.Network.Tests.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Bot.Tests.Network;

/// <summary>Game sessions a test builds, replayed through the relay while D3B sends the server its requests (D63).</summary>
public sealed class GameReplayTests : IDisposable
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

    private static readonly MapMovementRequest _walk = new() { KeyMovements = { 398, 4495, 457, 461 }, MapId = 188744706 };

    private readonly ProtocolTranslation _translation = TestGame.Translation();
    private readonly FakeLogCollector _log = new();
    private readonly ILoggerFactory _logs;
    private readonly TemporaryDirectory _captures = new();

    public GameReplayTests()
    {
        _logs = LoggerFactory.Create(logging => logging.AddProvider(new FakeLoggerProvider(_log)).SetMinimumLevel(LogLevel.Debug));
    }

    public void Dispose()
    {
        _logs.Dispose();
        _captures.Dispose();
    }

    private static byte[] Framed(byte[] frame)
    {
        var writer = new ArrayBufferWriter<byte>();

        FrameWriter.Write(writer, frame);

        return writer.WrittenSpan.ToArray();
    }

    private static List<byte[]> Frames(byte[] bytes)
    {
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));
        var frames = new List<byte[]>();

        while (FrameReader.TryReadFrame(ref reader, out var frame))
            frames.Add(frame.ToArray());

        Assert.Equal(bytes.Length, reader.Consumed);

        return frames;
    }

    private static async Task<byte[]> ToEndAsync(PipeReader reader)
    {
        using var bytes = new MemoryStream();

        while (true)
        {
            var result = await reader.ReadAsync(TestContext.Current.CancellationToken);

            foreach (var segment in result.Buffer)
                bytes.Write(segment.Span);

            reader.AdvanceTo(result.Buffer.End);

            if (result.IsCompleted)
                break;
        }

        await reader.CompleteAsync();

        return bytes.ToArray();
    }

    /// <summary>A game session as the captures show one, recorded read by read, some frames cut across reads.</summary>
    /// <remarks>
    /// The client presents its ticket, the server accepts it and names the map, the client asks what is on it and the server
    /// answers; then the character walks, on a request that the client does not send, and the client confirms once arrived.
    /// </remarks>
    private static List<CaptureRecord> Session(out List<byte[]> clientFrames)
    {
        clientFrames =
        [
            TestGame.Request(new AuthenticationRequest { Ticket = Secret, Language = "fr" }),
            TestGame.Request(new MapInformationRequest { MapId = 188744706 }),
            TestGame.Request(new MapMovementConfirmRequest()),
        ];

        var information = Framed(TestGame.Event(new MapInformationEvent { MapId = 188744706, SubAreaId = 95 }));
        var asking = Framed(clientFrames[1]);
        var stream = new MemoryStream();
        var header = new CaptureHeader { StartedAt = DateTimeOffset.UnixEpoch, ClientVersion = TestInstall.Version, Listener = string.Empty, ServerHost = string.Empty, Server = string.Empty };

        using (var writer = new CaptureWriter(stream, header, TimeProvider.System))
        {
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(Framed(clientFrames[0])));
            writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>([.. Framed(TestGame.Event(new AuthenticationAcceptedEvent())), .. Framed(TestGame.Event(new MapCurrentEvent { MapId = 188744706 }))]));
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(asking[..5]));
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(asking[5..]));
            writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(information[..10]));
            writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(information[10..]));
            writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(Framed(TestGame.Event(new MapMovementEvent { Cells = { 398, 399, 413, 428, 442, 457, 458, 459, 460, 461 }, ActorId = 7 }))));
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(Framed(clientFrames[2])));
            writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(Framed(TestGame.Response(new MapMovementConfirmResponse()))));
        }

        using var reader = new CaptureReader(new MemoryStream(stream.ToArray()));

        return [.. reader.ReadAll()];
    }

    /// <summary>
    /// Replays a session through the relay, read by a game session, whose observer has D3B send the walk once the server
    /// said what is on the map. Each side's record goes once the other side has everything sent so far, D3B's request
    /// included, so that what each side gets is the same at every run.
    /// </summary>
    private async Task<(byte[] ClientGot, byte[] ServerGot, RecordingObserver Observer)> ReplayAsync(List<CaptureRecord> records)
    {
        var sending = new List<Task>();

        Task[] Sent()
        {
            lock (sending)
                return [.. sending];
        }

        var observer = new RecordingObserver
        {
            OnEachFrame = (session, direction, frame) =>
            {
                if (direction is RelayDirection.FromServer && frame.Content is MapInformationEvent)
                {
                    lock (sending)
                        sending.Add(session.SendAsync(_walk, TestContext.Current.CancellationToken));
                }
            },
        };
        var decoder = new RelayDecoder(_translation, new GameServerSelections(_settings, TimeProvider.System), new GameAccounts(_settings.SelectionLifetime, TimeProvider.System), [observer], _logs);
        var injected = Framed(_translation.EncodeGameRequest(_walk)).Length;

        await using var relay = await TestRelay.StartAsync(capturesDirectory: _captures.Path, interceptor: decoder);

        using var client = await TestRelay.ConnectToAsync(relay.GameEndPoint);
        var clientReceiver = new SocketReceiver(client);
        Socket? server = null;
        SocketReceiver? serverReceiver = null;
        var fromClient = 0L;
        var fromServer = 0L;

        try
        {
            foreach (var record in records)
            {
                if (record.Kind is CaptureRecordKind.FromClient)
                {
                    await client.SendAsync(record.Data, TestContext.Current.CancellationToken);
                    fromClient += record.Data.Length;

                    // The game listener connects to the server once the client's first frame is whole (D52).
                    if (server is null)
                    {
                        server = await relay.AcceptAsync();
                        serverReceiver = new SocketReceiver(server);
                    }

                    await serverReceiver!.WaitForAsync(fromClient + (Sent().Length * injected));
                }
                else if (record.Kind is CaptureRecordKind.FromServer)
                {
                    await server!.SendAsync(record.Data, TestContext.Current.CancellationToken);
                    fromServer += record.Data.Length;
                    await clientReceiver.WaitForAsync(fromServer);

                    // The relay shows a frame before its bytes reach the client: what the observer sent is in the list.
                    var sent = Sent();

                    await Task.WhenAll(sent);
                    await serverReceiver!.WaitForAsync(fromClient + (sent.Length * injected));
                }
            }

            client.Shutdown(SocketShutdown.Send);

            var serverGot = await serverReceiver!.ToEndAsync();

            server!.Shutdown(SocketShutdown.Send);

            var clientGot = await clientReceiver.ToEndAsync();

            await relay.WaitForLogAsync(record => record.Message.Contains(" closed after ", StringComparison.Ordinal));

            return (clientGot, serverGot, observer);
        }
        finally
        {
            server?.Dispose();
        }
    }

    [Fact]
    public async Task D3BsRequestReachesTheServerBetweenTheClientsFramesAndTheClientGetsTheServersBytesAsTheyCame()
    {
        var records = Session(out var clientFrames);

        var (clientGot, serverGot, _) = await ReplayAsync(records);

        Assert.Equal(records.Where(record => record.Kind is CaptureRecordKind.FromServer).SelectMany(record => record.Data.ToArray()), clientGot);

        var frames = Frames(serverGot);

        Assert.Equal([clientFrames[0], clientFrames[1]], frames[..2]);
        Assert.Equal(clientFrames[2], frames[3]);
        Assert.Equal(4, frames.Count);

        var request = _translation.DecodeGame(new ReadOnlySequence<byte>(frames[2]));

        Assert.True(request.IsUnderstood, request.Problem);
        Assert.Equal(ProtocolTranslation.UncountedRequestId, request.Envelope.Request.Id);
        Assert.Equal(_walk, request.Content);
    }

    [Fact]
    public async Task TheObserversSeeEveryFrameTheSidesSentDecodedAndNoneOfD3Bs()
    {
        var (_, _, observer) = await ReplayAsync(Session(out _));

        Assert.Equal(
            [
                (RelayDirection.FromClient, "AuthenticationRequest"),
                (RelayDirection.FromServer, "AuthenticationAcceptedEvent"),
                (RelayDirection.FromServer, "MapCurrentEvent"),
                (RelayDirection.FromClient, "MapInformationRequest"),
                (RelayDirection.FromServer, "MapInformationEvent"),
                (RelayDirection.FromServer, "MapMovementEvent"),
                (RelayDirection.FromClient, "MapMovementConfirmRequest"),
                (RelayDirection.FromServer, "MapMovementConfirmResponse"),
            ],
            observer.Frames.Select(frame => (frame.Direction, frame.Frame.ReadableType?.Name)));
        Assert.All(observer.Frames, frame => Assert.True(frame.Frame.IsRead));
        Assert.Contains(_log.GetSnapshot(), record => record.Level is LogLevel.Information && record.Message.Contains("D3B sent MapMovementRequest", StringComparison.Ordinal));
        Assert.DoesNotContain(_log.GetSnapshot(), record => record.Message.Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheCaptureOfAReplayWithInjectionReplaysAsEachSideGotIt()
    {
        var (clientGot, serverGot, _) = await ReplayAsync(Session(out _));

        using var reader = CaptureReader.Open(Assert.Single(Directory.GetFiles(_captures.Path, "*" + CaptureWriter.FileExtension, SearchOption.AllDirectories)));
        var records = reader.ReadAll().ToList();
        var fromClient = new Pipe();
        var fromServer = new Pipe();
        var injected = new Pipe();

        await CaptureReplay.PlayAsync(records, fromClient.Writer, fromServer.Writer, injected.Writer, TestContext.Current.CancellationToken);

        Assert.Equal(2, reader.FormatVersion);
        Assert.Equal(clientGot, await ToEndAsync(fromServer.Reader));
        Assert.Equal(serverGot, records.Where(record => record.Kind is CaptureRecordKind.FromClient or CaptureRecordKind.Injected).SelectMany(record => record.Data.ToArray()));

        var sent = Assert.Single(Frames(await ToEndAsync(injected.Reader)));

        Assert.Equal(_walk, _translation.DecodeGame(new ReadOnlySequence<byte>(sent)).Content);
        Assert.Equal(3, Frames(await ToEndAsync(fromClient.Reader)).Count);
    }
}
