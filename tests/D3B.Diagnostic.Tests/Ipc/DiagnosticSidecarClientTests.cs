using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Ipc;
using D3B.Diagnostic.Tests.Replay;

namespace D3B.Diagnostic.Tests.Ipc;

public sealed class DiagnosticSidecarClientTests
{
    [Fact]
    public async Task ExchangesMessagesWithUnixSocketServer()
    {
        string path = Path.Combine(Path.GetTempPath(), $"d3b-ipc-{Guid.NewGuid():N}.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(path));
            listener.Listen(1);
        }
        catch (Exception exception) when (exception is PlatformNotSupportedException or SocketException or NotSupportedException)
        {
            Assert.Skip("Unix domain sockets are unavailable on this platform.");
        }

        DiagnosticSchema schema = ReplayTestData.CreateSchema();
        Task server = Task.Run(async () =>
        {
            using Socket accepted = await listener.AcceptAsync();
            using var stream = new NetworkStream(accepted, ownsSocket: false);
            await ReadFrameAsync(stream);
            await ReadFrameAsync(stream);
            await WriteFrameAsync(stream, ReplayTestData.EngineHelloLine());
            string frameJson = Encoding.UTF8.GetString(await ReadFrameAsync(stream));
            using System.Text.Json.JsonDocument frame = System.Text.Json.JsonDocument.Parse(frameJson);
            long sequence = frame.RootElement.GetProperty("sequence").GetInt64();
            await WriteFrameAsync(stream, ReplayTestData.AnalysisLine(sequence)
                .Replace("\"index\":", "\"sequence\":", StringComparison.Ordinal));
            await ReadFrameAsync(stream);
            await WriteFrameAsync(stream, ReplayTestData.SummaryLine(schema, 1));
        }, TestContext.Current.CancellationToken);

        using var clientSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await clientSocket.ConnectAsync(new UnixDomainSocketEndPoint(path), TestContext.Current.CancellationToken);
        await using var client = new DiagnosticSidecarClient(new NetworkStream(clientSocket, ownsSocket: false), new SidecarOptions());
        await client.StartAsync(schema, TestContext.Current.CancellationToken);
        DiagnosticAnalysis analysis = await client.AnalyzeAsync(ReplayTestData.CreateFrame(), TestContext.Current.CancellationToken);
        DiagnosticSummary summary = await client.CompleteAsync(TestContext.Current.CancellationToken);
        await server;

        Assert.Equal(7, analysis.Index);
        Assert.Equal(1, summary.Frames);
        File.Delete(path);
    }

    [Fact]
    public async Task SendsSequenceAndCompletesSession()
    {
        DiagnosticSchema schema = ReplayTestData.CreateSchema();
        byte[] responses = Combine(
            Frame(ReplayTestData.EngineHelloLine()),
            Frame(ReplayTestData.AnalysisLine(7).Replace("\"index\":", "\"sequence\":", StringComparison.Ordinal)),
            Frame(ReplayTestData.SummaryLine(schema, 1)));
        using var stream = new FramedMemoryStream(responses);
        var client = new DiagnosticSidecarClient(stream, new SidecarOptions());

        await client.StartAsync(schema, TestContext.Current.CancellationToken);
        DiagnosticAnalysis analysis = await client.AnalyzeAsync(ReplayTestData.CreateFrame(), TestContext.Current.CancellationToken);
        DiagnosticSummary summary = await client.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(7, analysis.Index);
        Assert.Equal(1, summary.Frames);
        Assert.Contains("\"sequence\":7", Encoding.UTF8.GetString(stream.Written.ToArray()), StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"bye\"", Encoding.UTF8.GetString(stream.Written.ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsSequenceMismatch()
    {
        DiagnosticSchema schema = ReplayTestData.CreateSchema();
        byte[] response = Combine(
            Frame(ReplayTestData.EngineHelloLine()),
            Frame(ReplayTestData.AnalysisLine(8).Replace("\"index\":", "\"sequence\":", StringComparison.Ordinal)));
        using var stream = new FramedMemoryStream(response);
        var client = new DiagnosticSidecarClient(stream, new SidecarOptions());
        await client.StartAsync(schema, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DiagnosticProtocolException>(() => client.AnalyzeAsync(ReplayTestData.CreateFrame(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsSequenceMismatchOverUnixSocket()
    {
        await RunSocketSessionAsync(new SidecarOptions(), async stream =>
        {
            await ReadFrameAsync(stream);
            await ReadFrameAsync(stream);
            await WriteFrameAsync(stream, ReplayTestData.EngineHelloLine());
            await ReadFrameAsync(stream);
            await WriteFrameAsync(stream, ReplayTestData.AnalysisLine(8)
                .Replace("\"index\":", "\"sequence\":", StringComparison.Ordinal));
        }, async client =>
        {
            await client.StartAsync(ReplayTestData.CreateSchema(), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<DiagnosticProtocolException>(() => client.AnalyzeAsync(ReplayTestData.CreateFrame(), TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task RejectsEngineErrorOverUnixSocketWithoutLeakingDetail()
    {
        await RunSocketSessionAsync(new SidecarOptions(), async stream =>
        {
            await ReadFrameAsync(stream);
            await ReadFrameAsync(stream);
            await WriteFrameAsync(stream, "{\"kind\":\"error\",\"code\":\"SCHEMA_INVALID\",\"detail\":\"secret-value\"}");
        }, async client =>
        {
            DiagnosticSidecarException exception = await Assert.ThrowsAsync<DiagnosticSidecarException>(
                () => client.StartAsync(ReplayTestData.CreateSchema(), TestContext.Current.CancellationToken));
            Assert.Equal(DiagnosticErrorCode.SchemaInvalid, exception.ErrorCode);
            Assert.DoesNotContain("secret-value", exception.Message, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("")]
    [InlineData("oversized")]
    public async Task RejectsMalformedOrInvalidSocketFrames(string responseKind)
    {
        await RunSocketSessionAsync(new SidecarOptions(), async stream =>
        {
            await ReadFrameAsync(stream);
            await ReadFrameAsync(stream);
            if (responseKind == "oversized")
            {
                byte[] prefix = new byte[sizeof(int)];
                BinaryPrimitives.WriteInt32BigEndian(prefix, 1_048_577);
                await stream.WriteAsync(prefix);
                await stream.FlushAsync();
            }
            else if (responseKind.Length == 0)
            {
                await stream.WriteAsync(new byte[sizeof(int)]);
                await stream.FlushAsync();
            }
            else
            {
                await WriteFrameAsync(stream, responseKind);
            }
        }, async client =>
        {
            await Assert.ThrowsAsync<DiagnosticProtocolException>(
                () => client.StartAsync(ReplayTestData.CreateSchema(), TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task TimesOutWaitingForSocketHandshake()
    {
        var options = new SidecarOptions { HandshakeTimeout = TimeSpan.FromMilliseconds(80) };
        await RunSocketSessionAsync(options, async stream =>
        {
            await ReadFrameAsync(stream);
            await ReadFrameAsync(stream);
            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }, async client =>
        {
            DiagnosticSidecarException exception = await Assert.ThrowsAsync<DiagnosticSidecarException>(
                () => client.StartAsync(ReplayTestData.CreateSchema(), TestContext.Current.CancellationToken));
            Assert.Equal(DiagnosticErrorCode.IoError, exception.ErrorCode);
        });
    }

    [Fact]
    public async Task TimesOutWaitingForSocketAnalysisResponse()
    {
        var options = new SidecarOptions { ResponseTimeout = TimeSpan.FromMilliseconds(80) };
        await RunSocketSessionAsync(options, async stream =>
        {
            await ReadFrameAsync(stream);
            await ReadFrameAsync(stream);
            await WriteFrameAsync(stream, ReplayTestData.EngineHelloLine());
            await ReadFrameAsync(stream);
            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }, async client =>
        {
            await client.StartAsync(ReplayTestData.CreateSchema(), TestContext.Current.CancellationToken);
            DiagnosticSidecarException exception = await Assert.ThrowsAsync<DiagnosticSidecarException>(
                () => client.AnalyzeAsync(ReplayTestData.CreateFrame(), TestContext.Current.CancellationToken));
            Assert.Equal(DiagnosticErrorCode.IoError, exception.ErrorCode);
        });
    }

    [Fact]
    public async Task ReportsAbruptSocketCloseDuringHandshake()
    {
        await RunSocketSessionAsync(new SidecarOptions(), async stream =>
        {
            await ReadFrameAsync(stream);
            await ReadFrameAsync(stream);
        }, async client =>
        {
            await Assert.ThrowsAsync<DiagnosticProtocolException>(
                () => client.StartAsync(ReplayTestData.CreateSchema(), TestContext.Current.CancellationToken));
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_048_577)]
    public async Task RejectsInvalidResponseLengths(int length)
    {
        byte[] prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(prefix, length);
        using var stream = new FramedMemoryStream(prefix);
        var client = new DiagnosticSidecarClient(stream, new SidecarOptions());
        await Assert.ThrowsAsync<DiagnosticProtocolException>(() => client.StartAsync(ReplayTestData.CreateSchema(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RedactsEngineErrorDetails()
    {
        using var stream = new FramedMemoryStream(Frame(
            "{\"kind\":\"error\",\"code\":\"SCHEMA_INVALID\",\"detail\":\"secret-payload\"}"));
        var client = new DiagnosticSidecarClient(stream, new SidecarOptions());

        DiagnosticSidecarException exception = await Assert.ThrowsAsync<DiagnosticSidecarException>(
            () => client.StartAsync(ReplayTestData.CreateSchema(), TestContext.Current.CancellationToken));

        Assert.Equal(DiagnosticErrorCode.SchemaInvalid, exception.ErrorCode);
        Assert.DoesNotContain("secret-payload", exception.Message, StringComparison.Ordinal);
    }

    private static byte[] Frame(string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        byte[] prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(prefix, bytes.Length);
        return [.. prefix, .. bytes];
    }

    private static byte[] Combine(params byte[][] frames)
    {
        return frames.SelectMany(frame => frame).ToArray();
    }

    private static async Task<byte[]> ReadFrameAsync(Stream stream)
    {
        byte[] prefix = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, prefix);
        int length = BinaryPrimitives.ReadInt32BigEndian(prefix);
        byte[] content = new byte[length];
        await ReadExactlyAsync(stream, content);
        return content;
    }

    private static async Task WriteFrameAsync(Stream stream, string json)
    {
        byte[] content = Encoding.UTF8.GetBytes(json);
        byte[] prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(prefix, content.Length);
        await stream.WriteAsync(prefix);
        await stream.WriteAsync(content);
        await stream.FlushAsync();
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> content)
    {
        int offset = 0;
        while (offset < content.Length)
        {
            int read = await stream.ReadAsync(content[offset..]);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }

    private static async Task RunSocketSessionAsync(
        SidecarOptions options,
        Func<Stream, Task> serverScript,
        Func<IDiagnosticSidecar, Task> clientScript)
    {
        string path = Path.Combine(Path.GetTempPath(), $"d3b-ipc-{Guid.NewGuid():N}.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(path));
            listener.Listen(1);
        }
        catch (Exception exception) when (exception is PlatformNotSupportedException or SocketException or NotSupportedException)
        {
            Assert.Skip("Unix domain sockets are unavailable on this platform.");
        }

        Task server = Task.Run(async () =>
        {
            using Socket accepted = await listener.AcceptAsync();
            using var serverStream = new NetworkStream(accepted, ownsSocket: false);
            await serverScript(serverStream);
        });
        using var clientSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await clientSocket.ConnectAsync(new UnixDomainSocketEndPoint(path));
        await using var client = new DiagnosticSidecarClient(new NetworkStream(clientSocket, ownsSocket: false), options);
        try
        {
            await clientScript(client);
            await server;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
