using System.Text;
using System.Text.Json.Nodes;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Tests.Contract;

public sealed class DiagnosticJsonTests
{
    [Fact]
    public void RejectsAnEmptyObjectForEveryProtocolMessage()
    {
        byte[] json = Encoding.UTF8.GetBytes("{}");

        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeAnalysis(json));
        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeSummary(json));
        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeHello(json));
        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeClientHello(json));
        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeError(json));
    }

    [Fact]
    public void RejectsEachMissingRequiredAnalysisProperty()
    {
        const string json = "{\"kind\":\"analysis\",\"index\":0,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"VALID\",\"fields\":[{\"number\":1,\"repeated\":false,\"packed\":false,\"occurrences\":0,\"wireTypes\":[],\"observed\":{},\"problems\":[]}],\"problems\":[]}";
        AssertMissingProperties(json, ["kind", "index", "direction", "typeUrlOk", "status", "fields", "problems"], bytes => DiagnosticJson.DeserializeAnalysis(bytes));
        AssertMissingAnalysisFieldProperties(json, ["number", "repeated", "packed", "occurrences", "wireTypes", "observed", "problems"]);
    }

    [Fact]
    public void RejectsEachMissingRequiredSummaryProperty()
    {
        const string json = "{\"kind\":\"summary\",\"protocolVersion\":1,\"clientVersion\":\"3.7\",\"schemaSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"frames\":0,\"statuses\":{},\"problems\":{},\"dropped\":0,\"coverage\":\"complete\"}";
        AssertMissingProperties(json, ["kind", "protocolVersion", "clientVersion", "schemaSha256", "frames", "statuses", "problems", "dropped", "coverage"], bytes => DiagnosticJson.DeserializeSummary(bytes));
    }

    [Fact]
    public void RejectsEachMissingRequiredHelloAndErrorProperty()
    {
        const string hello = "{\"kind\":\"hello\",\"protocolVersion\":1,\"engine\":\"d3b-diagnostic\",\"engineVersion\":\"0.1.0\",\"snsCommit\":\"commit\"}";
        AssertMissingProperties(hello, ["kind", "protocolVersion", "engine", "engineVersion", "snsCommit"], bytes => DiagnosticJson.DeserializeHello(bytes));
        const string clientHello = "{\"kind\":\"hello\",\"protocolVersion\":1,\"clientVersion\":\"3.7\",\"schemaSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}";
        AssertMissingProperties(clientHello, ["kind", "protocolVersion", "clientVersion", "schemaSha256"], bytes => DiagnosticJson.DeserializeClientHello(bytes));
        const string error = "{\"kind\":\"error\",\"code\":\"INTERNAL_ERROR\",\"detail\":\"redacted\"}";
        AssertMissingProperties(error, ["kind", "code"], bytes => DiagnosticJson.DeserializeError(bytes));
        Assert.Null(DiagnosticJson.DeserializeError(Encoding.UTF8.GetBytes("{\"kind\":\"error\",\"code\":\"INTERNAL_ERROR\"}")).Detail);
    }

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
    public void OmitsNullOptionalAnalysisProperties()
    {
        var analysis = new DiagnosticAnalysis
        {
            Direction = "server",
            TypeUrlOk = false,
            Status = DiagnosticStatus.UnknownType,
        };

        byte[] json = DiagnosticJson.SerializeAnalysis(analysis);
        string jsonText = Encoding.UTF8.GetString(json);
        DiagnosticAnalysis parsed = DiagnosticJson.DeserializeAnalysis(json);

        Assert.Contains("\"kind\":\"analysis\"", jsonText, StringComparison.Ordinal);
        Assert.Contains("\"typeUrlOk\":false", jsonText, StringComparison.Ordinal);
        Assert.DoesNotContain("carriedType", jsonText, StringComparison.Ordinal);
        Assert.DoesNotContain("message", jsonText, StringComparison.Ordinal);
        Assert.DoesNotContain("detailTruncated", jsonText, StringComparison.Ordinal);
        Assert.Null(parsed.CarriedType);
        Assert.Null(parsed.Message);
        Assert.Null(parsed.DetailTruncated);
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

    [Theory]
    [InlineData("{\"kind\":\"schema\",\"messages\":[{\"name\":\"A\",\"mapEntry\":false,\"fields\":[{\"number\":1,\"name\":\"x\",\"kind\":\"INT32\",\"repeated\":false,\"packed\":true,\"hasPresence\":false}]}]}")]
    [InlineData("{\"kind\":\"schema\",\"messages\":[{\"name\":\"A\",\"mapEntry\":false,\"fields\":[{\"number\":1,\"name\":\"x\",\"kind\":\"MESSAGE\",\"repeated\":false,\"packed\":false,\"hasPresence\":false}]}]}")]
    [InlineData("{\"kind\":\"schema\",\"messages\":[{\"name\":\"A\",\"mapEntry\":false,\"fields\":[{\"number\":1,\"name\":\"x\",\"kind\":\"INT32\",\"repeated\":false,\"packed\":false,\"messageType\":\"B\",\"hasPresence\":false}]}]}")]
    [InlineData("{\"kind\":\"schema\",\"messages\":[{\"name\":\"A\",\"mapEntry\":false,\"fields\":[{\"number\":1,\"name\":\"x\",\"kind\":\"INT32\",\"repeated\":false,\"packed\":false,\"enumType\":\"E\",\"hasPresence\":false}]}]}")]
    [InlineData("{\"kind\":\"schema\",\"messages\":[{\"name\":\"A\",\"fields\":[]}]}")]
    [InlineData("{\"kind\":\"schema\",\"messages\":[{\"name\":\"A\",\"mapEntry\":false,\"fields\":[{\"number\":1,\"kind\":\"INT32\",\"repeated\":false,\"packed\":false,\"hasPresence\":false}]}]}")]
    [InlineData("{\"kind\":\"schema\",\"messages\":[{\"name\":\"A\",\"mapEntry\":false,\"fields\":[{\"number\":1,\"name\":\"x\",\"kind\":\"INT32\",\"repeated\":false,\"packed\":false}]}]}")]
    [InlineData("{\"kind\":\"schema\",\"messages\":[{\"name\":\"A\",\"mapEntry\":0,\"fields\":[]}]}")]
    [InlineData("{\"kind\":\"schema\",\"messages\":[{\"name\":\"A\",\"mapEntry\":false,\"fields\":[{\"number\":1,\"name\":\"x\",\"kind\":\"INT32\",\"repeated\":false,\"packed\":false,\"hasPresence\":\"false\"}]}]}")]
    public void RejectsInvalidSchemaFieldRelationshipsAndMissingBooleans(string json)
    {
        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeSchema(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("{\"kind\":\"analysis\",\"index\":-1,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"VALID\",\"fields\":[],\"problems\":[]}")]
    [InlineData("{\"kind\":\"analysis\",\"index\":0,\"direction\":\"other\",\"typeUrlOk\":false,\"status\":\"VALID\",\"fields\":[],\"problems\":[]}")]
    [InlineData("{\"kind\":\"analysis\",\"index\":0,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"FUTURE\",\"fields\":[],\"problems\":[]}")]
    [InlineData("{\"kind\":\"analysis\",\"index\":0,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"VALID\",\"fields\":[{\"number\":536870912,\"repeated\":false,\"packed\":false,\"occurrences\":0,\"wireTypes\":[],\"observed\":{},\"problems\":[]}],\"problems\":[]}")]
    [InlineData("{\"kind\":\"analysis\",\"index\":0,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"VALID\",\"fields\":[{\"number\":1,\"repeated\":false,\"packed\":false,\"occurrences\":0,\"wireTypes\":[\"FUTURE\"],\"observed\":{},\"problems\":[]}],\"problems\":[]}")]
    [InlineData("{\"kind\":\"summary\",\"protocolVersion\":1,\"clientVersion\":\"3.7\",\"schemaSha256\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\",\"frames\":0,\"statuses\":{},\"problems\":{},\"dropped\":0,\"coverage\":\"complete\"}")]
    [InlineData("{\"kind\":\"summary\",\"protocolVersion\":1,\"clientVersion\":\"3.7\",\"schemaSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"frames\":0,\"statuses\":{\"FUTURE\":1},\"problems\":{},\"dropped\":0,\"coverage\":\"complete\"}")]
    [InlineData("{\"kind\":\"summary\",\"protocolVersion\":1,\"clientVersion\":\"3.7\",\"schemaSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"frames\":0,\"statuses\":{},\"problems\":{},\"dropped\":0,\"coverage\":\"unknown\"}")]
    public void RejectsInvalidAnalysisAndSummaryValues(string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        if (json.Contains("\"kind\":\"analysis\"", StringComparison.Ordinal))
        {
            Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeAnalysis(bytes));
        }
        else
        {
            Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeSummary(bytes));
        }
    }

    [Fact]
    public void RejectsNestedAnalysisFieldsWithMissingRequiredProperties()
    {
        const string json = "{\"kind\":\"analysis\",\"index\":0,\"direction\":\"server\",\"typeUrlOk\":false,\"status\":\"VALID\",\"fields\":[{\"number\":1,\"repeated\":false,\"packed\":false,\"occurrences\":0,\"wireTypes\":[],\"observed\":{},\"problems\":[],\"nested\":[{\"repeated\":false,\"packed\":false,\"occurrences\":0,\"wireTypes\":[],\"observed\":{},\"problems\":[]}]}],\"problems\":[]}";

        Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeAnalysis(Encoding.UTF8.GetBytes(json)));
    }

    private static void AssertMissingProperties<T>(string json, string[] properties, Func<byte[], T> deserialize)
    {
        foreach (string property in properties)
        {
            JsonObject value = JsonNode.Parse(json)!.AsObject();
            value.Remove(property);
            byte[] bytes = Encoding.UTF8.GetBytes(value.ToJsonString());
            Assert.Throws<DiagnosticProtocolException>(() => deserialize(bytes));
        }
    }

    private static void AssertMissingAnalysisFieldProperties(string json, string[] properties)
    {
        foreach (string property in properties)
        {
            JsonObject value = JsonNode.Parse(json)!.AsObject();
            value["fields"]!.AsArray()[0]!.AsObject().Remove(property);
            byte[] bytes = Encoding.UTF8.GetBytes(value.ToJsonString());
            Assert.Throws<DiagnosticProtocolException>(() => DiagnosticJson.DeserializeAnalysis(bytes));
        }
    }
}
