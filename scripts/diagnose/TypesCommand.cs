using System.CommandLine;
using D3B.Diagnostic.Resolution;
using D3B.Diagnostic.Schema;
using D3B.Protocol.Mapping;

namespace D3B.Scripts.Diagnose;

internal static class TypesCommand
{
    public static Command Create()
    {
        var command = new Command("types", "Compare build schema structure with readable messages and references.");
        var schema = new Option<FileInfo>("--schema") { Required = true, Description = "The installed build's schema-1.binpb." };
        var version = new Option<string>("--client-version") { Required = true, Description = "Client build version associated with the schema." };
        var engine = new Option<FileInfo>("--engine") { Required = true, Description = "Path to the local d3b-diagnostic executable." };
        var tokens = new Option<FileInfo>("--tokens") { Required = true, Description = "File of requested message tokens, whitespace or newline separated." };
        var sns = new Option<FileInfo?>("--sns") { Description = "Read-only SNS Mapping.v2.json reference." };
        var billbot = new Option<FileInfo?>("--billbot") { Description = "Read-only BillBot game_mappings_detailed.json reference." };
        var corpus = new Option<FileInfo?>("--corpus") { Description = "Optional read-only reference corpus JSON." };
        var pins = new Option<FileInfo?>("--pins") { Description = "Optional build-specific pinned pairs JSON." };
        var output = new Option<FileInfo?>("--out") { Description = "Write compact candidate JSON to this path; stdout by default." };
        command.Options.Add(schema); command.Options.Add(version); command.Options.Add(engine); command.Options.Add(tokens);
        command.Options.Add(sns); command.Options.Add(billbot); command.Options.Add(corpus); command.Options.Add(pins); command.Options.Add(output);
        command.SetAction(result => DiagnoseIO.Run(() =>
        {
            if (!result.GetValue(engine)!.Exists) throw new DiagnoseIO.DiagnoseFailure("engine missing");
            var versionValue = result.GetValue(version)!;
            var schemaFile = result.GetValue(schema)!;
            if (!schemaFile.Exists) throw new DiagnoseIO.DiagnoseFailure("schema missing");
            var buildSchema = DiagnoseIO.Stage("schema invalid", () => DiagnosticSchemaExporter.Export(versionValue, ProtocolSchema.Load(schemaFile.FullName)));
            var references = new List<ReferenceCorpus>();
            DiagnoseIO.Stage("references or pins invalid", () =>
            {
                if (result.GetValue(sns) is { } snsFile) references.Add(SnsMappingLoader.Load(snsFile.FullName));
                if (result.GetValue(billbot) is { } billFile) references.Add(BillBotMappingLoader.Load(billFile.FullName));
                if (result.GetValue(corpus) is { } corpusFile) references.Add(ReferenceCorpusJsonLoader.LoadFile(corpusFile.FullName));
                return true;
            });
            IReadOnlyList<PriorMapping> prior = DiagnoseIO.Stage("pins invalid", () => result.GetValue(pins) is { } pinFile
                ? PriorMapping.From(PinnedPairsFile.Read(pinFile.FullName, versionValue)) : Array.Empty<PriorMapping>());
            var report = new ProtocolResolver(ReadableProtocol.Files, references, prior).Resolve(buildSchema, [], null);
            var tokensFile = result.GetValue(tokens)!;
            if (!tokensFile.Exists) throw new DiagnoseIO.DiagnoseFailure("tokens file missing");
            var wanted = DiagnoseIO.Stage("tokens invalid or oversized", () => ReadTokens(tokensFile.FullName));
            var filteredCandidates = report.Candidates.Where(candidate => wanted.Contains(candidate.Token))
                .Select(candidate => candidate with { FixtureProposal = candidate.FixtureProposal is null ? null : candidate.FixtureProposal with { PayloadHex = string.Empty } })
                .ToArray();
            var filtered = report with { Candidates = filteredCandidates };
            filtered = filtered with { Summary = new CandidateSummary { Verified = filtered.Candidates.Count(c => c.Classification == CandidateClassification.Verified), Candidate = filtered.Candidates.Count(c => c.Classification == CandidateClassification.Candidate), Conflict = filtered.Candidates.Count(c => c.Classification == CandidateClassification.Conflict), Unknown = filtered.Candidates.Count(c => c.Classification == CandidateClassification.Unknown) } };
            DiagnoseIO.Write(result.GetValue(output), CandidateReportJson.Serialize(filtered));
        }));
        return command;
    }

    private static HashSet<string> ReadTokens(string path)
    {
        const long maximumTokenFileBytes = 1_048_576;
        const int maximumTokens = 10_000;
        var file = new FileInfo(path);
        if (!file.Exists || file.Length > maximumTokenFileBytes)
        {
            throw new InvalidDataException();
        }

        var tokens = File.ReadLines(path)
            .SelectMany(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToArray();
        if (tokens.Length > maximumTokens || tokens.Any(token => token.Length is 0 or > 1024 ||
            token.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'))))
        {
            throw new InvalidDataException();
        }

        return tokens.ToHashSet(StringComparer.Ordinal);
    }
}
