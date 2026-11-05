using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Replay;

namespace D3B.Diagnostic.Tests.Replay;

public sealed class ReplayEngineClientTests
{
    [Fact]
    public async Task StreamsTheHandshakeFrameAndSummary()
    {
        var schema = ReplayTestData.CreateSchema();
        var frame = ReplayTestData.CreateFrame();
        using var duplex = new InMemoryDuplexStream(ReplayTestData.CreateResponse(schema, frame.Index));
        var client = new ReplayEngineClient(duplex, duplex, _ => Task.FromResult(0));

        var result = await client.AnalyzeAsync(schema, [frame], CancellationToken.None);

        Assert.Equal("d3b-diagnostic", result.EngineHello.Engine);
        Assert.Equal(frame.Index, Assert.Single(result.Analyses).Index);
        Assert.Equal(1, result.Summary.Frames);
        Assert.Contains("\"payloadHex\":\"0801\"", duplex.WrittenText, StringComparison.Ordinal);
        Assert.EndsWith("{\"kind\":\"end\"}\n", duplex.WrittenText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OmitsNullOptionalFrameProperties()
    {
        var schema = ReplayTestData.CreateSchema();
        var frame = new DiagnosticFrame(7, "server", null, null, [0x08, 0x01]);
        const string analysisLine = "{\"kind\":\"analysis\",\"index\":7,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"UNKNOWN_TYPE\",\"fields\":[],\"problems\":[{\"code\":\"UNKNOWN_TYPE\",\"detail\":\"2\"}]}";
        string summaryLine = "{\"kind\":\"summary\",\"protocolVersion\":1,\"clientVersion\":\"" + schema.ClientVersion + "\",\"schemaSha256\":\"" + schema.SchemaSha256 + "\",\"frames\":1,\"statuses\":{\"UNKNOWN_TYPE\":1},\"problems\":{\"UNKNOWN_TYPE\":1},\"dropped\":0,\"coverage\":\"complete\"}";
        string response = ReplayTestData.EngineHelloLine() + "\n" + analysisLine + "\n" + summaryLine + "\n";
        using var duplex = new InMemoryDuplexStream(response);
        var client = new ReplayEngineClient(duplex, duplex);

        _ = await client.AnalyzeAsync(schema, [frame], CancellationToken.None);

        Assert.DoesNotContain("\"typeUrl\"", duplex.WrittenText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"carriedType\"", duplex.WrittenText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ReportsTheEngineExitCode(int exitCode)
    {
        var schema = ReplayTestData.CreateSchema();
        const string errorLine = "{\"kind\":\"error\",\"code\":\"SCHEMA_INVALID\",\"detail\":\"private-payload-content\"}\n";
        using var duplex = new InMemoryDuplexStream(errorLine);
        var client = new ReplayEngineClient(duplex, duplex, _ => Task.FromResult(exitCode));

        DiagnosticEngineException exception = await Assert.ThrowsAsync<DiagnosticEngineException>(
            () => client.AnalyzeAsync(schema, [ReplayTestData.CreateFrame()], CancellationToken.None));

        Assert.Equal(exitCode, exception.ExitCode);
        Assert.DoesNotContain("private-payload-content", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsAnAnalysisWithTheWrongIndex()
    {
        var schema = ReplayTestData.CreateSchema();
        using var duplex = new InMemoryDuplexStream(ReplayTestData.CreateResponse(schema, 8));
        var client = new ReplayEngineClient(duplex, duplex);

        await Assert.ThrowsAsync<DiagnosticProtocolException>(
            () => client.AnalyzeAsync(schema, [ReplayTestData.CreateFrame(7)], CancellationToken.None));
    }

    [Fact]
    public async Task RejectsMissingSummaryOutput()
    {
        var schema = ReplayTestData.CreateSchema();
        var response = ReplayTestData.EngineHelloLine() + "\n" + ReplayTestData.AnalysisLine(7) + "\n";
        using var duplex = new InMemoryDuplexStream(response);
        var client = new ReplayEngineClient(duplex, duplex);

        await Assert.ThrowsAsync<DiagnosticProtocolException>(
            () => client.AnalyzeAsync(schema, [ReplayTestData.CreateFrame()], CancellationToken.None));
    }

    [Fact]
    public async Task RejectsTruncatedOutputWithoutLeakingPayloadBytes()
    {
        var schema = ReplayTestData.CreateSchema();
        using var duplex = new InMemoryDuplexStream(ReplayTestData.EngineHelloLine() + "\n");
        var client = new ReplayEngineClient(duplex, duplex);

        DiagnosticProtocolException exception = await Assert.ThrowsAsync<DiagnosticProtocolException>(
            () => client.AnalyzeAsync(schema, [ReplayTestData.CreateFrame()], CancellationToken.None));

        Assert.DoesNotContain("0801", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoesNotLeakPayloadBytesFromMalformedOutput()
    {
        var schema = ReplayTestData.CreateSchema();
        using var duplex = new InMemoryDuplexStream("{\"kind\":\"hello\",\"payload\":\"0801");
        var client = new ReplayEngineClient(duplex, duplex);

        Exception exception = await Assert.ThrowsAnyAsync<Exception>(
            () => client.AnalyzeAsync(schema, [ReplayTestData.CreateFrame()], CancellationToken.None));

        Assert.DoesNotContain("0801", exception.Message, StringComparison.Ordinal);
    }
}
