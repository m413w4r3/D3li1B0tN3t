using System.Text;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Replay;

namespace D3B.Diagnostic.Tests.Replay;

public sealed class ReplayCorpusTests
{
    [Fact]
    public void ReadsAnOrderedCorpusWithAnInBandSchema()
    {
        byte[] corpusBytes = Encoding.UTF8.GetBytes(ReplayTestData.CreateCorpus());
        using var input = new MemoryStream(corpusBytes);

        var corpus = ReplayCorpus.Read(input);

        Assert.Equal("3.7.13.17", corpus.Hello.ClientVersion);
        Assert.Equal(ReplayTestData.SchemaHash, corpus.Schema.SchemaSha256);
        Assert.Equal(0, Assert.Single(corpus.Frames).Index);
    }

    [Fact]
    public void RejectsFrameIndicesThatAreNotIncreasing()
    {
        var corpus = ReplayTestData.CreateCorpus().Replace("\"index\":0", "\"index\":2", StringComparison.Ordinal)
            .Replace("\"kind\":\"end\"", "{\"kind\":\"frame\",\"index\":1,\"direction\":\"server\",\"typeUrl\":null,\"carriedType\":null,\"payloadHex\":\"\"}\n{\"kind\":\"end\"}", StringComparison.Ordinal);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(corpus));

        Assert.Throws<DiagnosticProtocolException>(() => ReplayCorpus.Read(input));
    }

    [Fact]
    public void RejectsFramesBeforeTheSchema()
    {
        var corpus = ReplayTestData.CreateCorpus();
        var lines = corpus.Split('\n');
        (lines[1], lines[2]) = (lines[2], lines[1]);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', lines)));

        Assert.Throws<DiagnosticProtocolException>(() => ReplayCorpus.Read(input));
    }

    [Fact]
    public void RejectsMalformedJsonWithoutEchoingTheLine()
    {
        const string invalidLine = "{\"kind\":\"hello\",\"payloadHex\":\"deadbeef";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(invalidLine));

        DiagnosticProtocolException exception = Assert.Throws<DiagnosticProtocolException>(() => ReplayCorpus.Read(input));

        Assert.DoesNotContain("deadbeef", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsOversizedJsonLines()
    {
        var hello = "{\"kind\":\"hello\",\"protocolVersion\":1,\"clientVersion\":\"test\",\"schemaSha256\":\"" + ReplayTestData.SchemaHash + "\"}\n";
        var oversizedLine = new string('x', DiagnosticProtocol.MessageLimit + 1);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(hello + oversizedLine));

        Assert.Throws<DiagnosticProtocolException>(() => ReplayCorpus.Read(input));
    }
}
