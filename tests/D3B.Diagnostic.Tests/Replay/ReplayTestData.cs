using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Replay;

namespace D3B.Diagnostic.Tests.Replay;

internal static class ReplayTestData
{
    public const string SchemaHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static DiagnosticSchema CreateSchema()
    {
        return new DiagnosticSchema
        {
            ClientVersion = "3.7.13.17",
            SchemaSha256 = SchemaHash,
            Messages =
            [
                new DiagnosticMessage
                {
                    Name = "lok",
                    MapEntry = false,
                    Fields =
                    [
                        new DiagnosticField
                        {
                            Number = 1,
                            Name = "value",
                            Kind = DiagnosticFieldKind.SignedInteger32,
                            Repeated = false,
                            Packed = false,
                            HasPresence = true,
                        },
                    ],
                },
            ],
        };
    }

    public static DiagnosticFrame CreateFrame(long index = 7)
    {
        return new DiagnosticFrame(index, "server", "type.ankama.com/lok", "lok", [0x08, 0x01]);
    }

    public static string EngineHelloLine()
    {
        return "{\"kind\":\"hello\",\"protocolVersion\":1,\"engine\":\"d3b-diagnostic\",\"engineVersion\":\"0.1.0\",\"snsCommit\":\"c780035c9020058a772ac8a2eef09555acd5a173\"}";
    }

    public static string AnalysisLine(long index)
    {
        return "{\"kind\":\"analysis\",\"index\":" + index + ",\"direction\":\"server\",\"carriedType\":\"lok\",\"typeUrlOk\":true,\"message\":\"lok\",\"status\":\"VALID\",\"fields\":[],\"problems\":[]}";
    }

    public static string SummaryLine(DiagnosticSchema schema, int frameCount)
    {
        return "{\"kind\":\"summary\",\"protocolVersion\":1,\"clientVersion\":\"" + schema.ClientVersion + "\",\"schemaSha256\":\"" + schema.SchemaSha256 + "\",\"frames\":" + frameCount + ",\"statuses\":{\"VALID\":" + frameCount + "},\"problems\":{},\"dropped\":0,\"coverage\":\"complete\"}";
    }

    public static string CreateResponse(DiagnosticSchema schema, params long[] indexes)
    {
        List<string> lines = [EngineHelloLine()];
        lines.AddRange(indexes.Select(AnalysisLine));
        lines.Add(SummaryLine(schema, indexes.Length));
        return string.Join('\n', lines) + "\n";
    }

    public static string CreateCorpus(long frameIndex = 0)
    {
        DiagnosticSchema schema = CreateSchema();
        return "{\"kind\":\"hello\",\"protocolVersion\":1,\"clientVersion\":\"" + schema.ClientVersion + "\",\"schemaSha256\":\"" + schema.SchemaSha256 + "\"}\n" +
            schema.ToJson() + "\n" +
            "{\"kind\":\"frame\",\"index\":" + frameIndex + ",\"direction\":\"server\",\"typeUrl\":\"type.ankama.com/lok\",\"carriedType\":\"lok\",\"payloadHex\":\"0801\"}\n" +
            "{\"kind\":\"end\"}\n";
    }
}
