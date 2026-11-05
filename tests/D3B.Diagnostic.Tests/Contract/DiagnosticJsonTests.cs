using System.Text;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Tests.Contract;

public sealed class DiagnosticJsonTests
{
    [Fact]
    public void ParsesValidAnalysisAndSummary()
    {
        const string analysisJson = "{\"kind\":\"analysis\",\"index\":0,\"direction\":\"server\",\"carriedType\":\"lok\",\"typeUrlOk\":true,\"message\":\"lok\",\"status\":\"SCHEMA_MISMATCH\",\"fields\":[{\"number\":1,\"name\":\"value\",\"declaredKind\":\"INT32\",\"repeated\":false,\"packed\":false,\"occurrences\":1,\"wireTypes\":[\"VARINT\"],\"observed\":{\"varintBytes\":[1]},\"problems\":[\"MALFORMED\"]}],\"problems\":[{\"code\":\"MALFORMED\",\"fieldPath\":\"1\",\"detail\":\"invalid tag\"}]}";
        const string summaryJson = "{\"kind\":\"summary\",\"protocolVersion\":1,\"clientVersion\":\"3.7.13.17\",\"schemaSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"frames\":1,\"statuses\":{\"SCHEMA_MISMATCH\":1},\"problems\":{\"MALFORMED\":2},\"dropped\":0,\"coverage\":\"complete\"}";

        var analysis = DiagnosticJson.DeserializeAnalysis(Encoding.UTF8.GetBytes(analysisJson));
        var summary = DiagnosticJson.DeserializeSummary(Encoding.UTF8.GetBytes(summaryJson));

        Assert.Equal(DiagnosticStatus.SchemaMismatch, analysis.Status);
        Assert.Equal(0, analysis.Index);
        Assert.Equal(DiagnosticProblemCode.Malformed, Assert.Single(analysis.Problems).Code);
        Assert.Equal(DiagnosticProblemCode.Malformed, Assert.Single(Assert.Single(analysis.Fields).Problems));
        Assert.Equal(1, summary.Frames);
        Assert.Equal(1, summary.Statuses[DiagnosticStatus.SchemaMismatch]);
        Assert.Equal(2, summary.Problems[DiagnosticProblemCode.Malformed]);
    }

    [Fact]
    public void RejectsAnUnknownProtocolVersion()
    {
        const string json = "{\"kind\":\"hello\",\"protocolVersion\":2,\"engine\":\"d3b-diagnostic\",\"engineVersion\":\"0.1.0\",\"snsCommit\":\"commit\"}";

        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeHello(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void RejectsAnUnknownStatus()
    {
        const string json = "{\"kind\":\"analysis\",\"index\":0,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"FUTURE_STATUS\",\"fields\":[],\"problems\":[]}";

        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeAnalysis(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void RejectsAnUnknownProblemCode()
    {
        const string json = "{\"kind\":\"analysis\",\"index\":0,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"UNKNOWN_TYPE\",\"fields\":[],\"problems\":[\"FUTURE_PROBLEM\"]}";

        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeAnalysis(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("VARINT", DiagnosticWireType.Varint)]
    [InlineData("I64", DiagnosticWireType.I64)]
    [InlineData("LEN", DiagnosticWireType.Len)]
    [InlineData("I32", DiagnosticWireType.I32)]
    [InlineData("GROUP", DiagnosticWireType.Group)]
    [InlineData("UNKNOWN", DiagnosticWireType.Unknown)]
    public void ParsesContractWireTypeNames(string wireTypeName, DiagnosticWireType expected)
    {
        string json = "{\"kind\":\"analysis\",\"index\":0,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"VALID\",\"fields\":[{\"number\":1,\"repeated\":false,\"packed\":false,\"occurrences\":1,\"wireTypes\":[\"" + wireTypeName + "\"],\"observed\":{},\"problems\":[]}],\"problems\":[]}";

        DiagnosticAnalysis analysis = DiagnosticJson.DeserializeAnalysis(Encoding.UTF8.GetBytes(json));

        Assert.Equal(expected, Assert.Single(Assert.Single(analysis.Fields).WireTypes));
    }

    [Fact]
    public void RejectsAnUnknownWireTypeName()
    {
        const string json = "{\"kind\":\"analysis\",\"index\":0,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"VALID\",\"fields\":[{\"number\":1,\"repeated\":false,\"packed\":false,\"occurrences\":1,\"wireTypes\":[\"FUTURE_WIRE\"],\"observed\":{},\"problems\":[]}],\"problems\":[]}";

        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeAnalysis(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("CONTRACT_INVALID", DiagnosticErrorCode.ContractInvalid)]
    [InlineData("VERSION_UNSUPPORTED", DiagnosticErrorCode.VersionUnsupported)]
    [InlineData("SCHEMA_FINGERPRINT_INVALID", DiagnosticErrorCode.SchemaFingerprintInvalid)]
    [InlineData("SCHEMA_INVALID", DiagnosticErrorCode.SchemaInvalid)]
    [InlineData("UNKNOWN_COMMAND", DiagnosticErrorCode.UnknownCommand)]
    [InlineData("INTERNAL_ERROR", DiagnosticErrorCode.InternalError)]
    [InlineData("IO_ERROR", DiagnosticErrorCode.IoError)]
    [InlineData("SOCKET_FAILED", DiagnosticErrorCode.SocketFailed)]
    [InlineData("UNSUPPORTED_PLATFORM", DiagnosticErrorCode.UnsupportedPlatform)]
    public void ParsesEveryContractErrorCode(string code, DiagnosticErrorCode expected)
    {
        string json = "{\"kind\":\"error\",\"code\":\"" + code + "\",\"detail\":\"redacted\"}";

        DiagnosticError error = DiagnosticJson.DeserializeError(Encoding.UTF8.GetBytes(json));

        Assert.Equal(expected, error.Code);
    }

    [Fact]
    public void RejectsAnUnknownErrorCode()
    {
        const string json = "{\"kind\":\"error\",\"code\":\"UNKNOWN_TYPE\",\"detail\":\"redacted\"}";

        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeError(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void RejectsTheRetiredUnpackedProblemCode()
    {
        const string json = "{\"kind\":\"analysis\",\"index\":0,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"VALID\",\"fields\":[],\"problems\":[{\"code\":\"UNPACKED_NOT_ALLOWED\"}]}";

        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeAnalysis(Encoding.UTF8.GetBytes(json)));
    }
}
