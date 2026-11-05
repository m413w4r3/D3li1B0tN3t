using System.CommandLine;
using D3B.Diagnostic.Reporting;
using D3B.Diagnostic.Resolution;
using D3B.Diagnostic.Schema;
using D3B.Protocol.Mapping;

namespace D3B.Scripts.Diagnose;

internal static class ResolveCommand
{
    private static readonly DiagnosticReportOptions _maximumReportOptions = new()
    {
        MaxTypes = 20_000,
        MaxFieldsPerType = 4_096,
        MaxFieldNodesPerType = 65_536,
        MaxNestedDepth = 32,
        MaxVariantsPerType = 1_024,
        MaxShapeValues = 4_096,
        MaxProblemsPerType = 4_096,
        MaxBytes = 64 * 1024 * 1024
    };

    public static Command Create()
    {
        var command = new Command("resolve", "Resolve a redacted diagnostic report against its exact build schema.");
        var report = new Option<FileInfo>("--report") { Required = true, Description = "The redacted P2L diagnostic report JSON." };
        var schema = new Option<FileInfo>("--schema") { Required = true, Description = "The installed build's schema-1.binpb." };
        var version = new Option<string>("--client-version") { Required = true, Description = "Client build version associated with the report and schema." };
        var sns = new Option<FileInfo?>("--sns") { Description = "Read-only SNS Mapping.v2.json reference." };
        var billbot = new Option<FileInfo?>("--billbot") { Description = "Read-only BillBot game_mappings_detailed.json reference." };
        var corpus = new Option<FileInfo?>("--corpus") { Description = "Optional read-only reference corpus JSON." };
        var pins = new Option<FileInfo?>("--pins") { Description = "Optional build-specific pinned pairs JSON." };
        var output = new Option<FileInfo?>("--out") { Description = "Write compact candidate JSON to this path; stdout by default." };
        command.Options.Add(report);
        command.Options.Add(schema);
        command.Options.Add(version);
        command.Options.Add(sns);
        command.Options.Add(billbot);
        command.Options.Add(corpus);
        command.Options.Add(pins);
        command.Options.Add(output);
        command.SetAction(result => DiagnoseIO.Run(() =>
        {
            FileInfo reportFile = result.GetValue(report)!;
            if (!reportFile.Exists)
            {
                throw new DiagnoseIO.DiagnoseFailure("report missing");
            }

            LoadedDiagnosticReport loaded = DiagnoseIO.Stage("report invalid or oversized", () =>
                DiagnosticReportJson.LoadFile(reportFile.FullName, _maximumReportOptions));
            string clientVersion = result.GetValue(version)!;
            if (!string.Equals(clientVersion, loaded.Report.ClientVersion, StringComparison.Ordinal))
            {
                throw new DiagnoseIO.DiagnoseFailure("client version mismatch");
            }

            FileInfo schemaFile = result.GetValue(schema)!;
            if (!schemaFile.Exists)
            {
                throw new DiagnoseIO.DiagnoseFailure("schema missing");
            }

            var buildSchema = DiagnoseIO.Stage("schema invalid", () =>
                DiagnosticSchemaExporter.Export(clientVersion, ProtocolSchema.Load(schemaFile.FullName)));
            if (!string.Equals(buildSchema.ClientVersion, loaded.Report.ClientVersion, StringComparison.Ordinal)
                || !string.Equals(buildSchema.SchemaSha256, loaded.Report.SchemaSha256, StringComparison.Ordinal))
            {
                throw new DiagnoseIO.DiagnoseFailure("report does not match schema");
            }

            var references = new List<ReferenceCorpus>();
            DiagnoseIO.Stage("references or pins invalid", () =>
            {
                if (result.GetValue(sns) is { } snsFile)
                {
                    references.Add(SnsMappingLoader.Load(snsFile.FullName));
                }

                if (result.GetValue(billbot) is { } billFile)
                {
                    references.Add(BillBotMappingLoader.Load(billFile.FullName));
                }

                if (result.GetValue(corpus) is { } corpusFile)
                {
                    references.Add(ReferenceCorpusJsonLoader.LoadFile(corpusFile.FullName));
                }

                return true;
            });
            IReadOnlyList<PriorMapping> prior = DiagnoseIO.Stage("pins invalid", () => result.GetValue(pins) is { } pinFile
                ? PriorMapping.From(PinnedPairsFile.Read(pinFile.FullName, clientVersion))
                : Array.Empty<PriorMapping>());
            CandidateReport resolved = DiagnoseIO.Stage("report resolution failed", () =>
                DiagnosticReportResolver.Resolve(buildSchema, loaded, references, prior));
            DiagnoseIO.Write(result.GetValue(output), CandidateReportJson.Serialize(resolved));
        }));

        return command;
    }
}
