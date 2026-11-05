using System.CommandLine;
using System.Text;
using System.Text.Json;
using D3B.Bot.Inspection;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Replay;
using D3B.Diagnostic.Schema;
using D3B.Protocol.Game;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;

namespace D3B.Scripts.Diagnose;

internal static class ReplayCommand
{
    private const int MaximumFrames = 100_000;
    private const long MaximumCorpusCharacters = 64L * 1024 * 1024;
    private const long MaximumCaptureBytes = 64L * 1024 * 1024;

    public static Command Create()
    {
        var command = new Command("replay", "Analyze a bounded synthetic corpus or authorized capture.");
        var engine = new Option<FileInfo>("--engine") { Required = true, Description = "Path to the local d3b-diagnostic executable." };
        var schema = new Option<FileInfo>("--schema") { Required = true, Description = "The installed build's schema-1.binpb." };
        var version = new Option<string>("--client-version") { Required = true, Description = "Client build version associated with the schema." };
        var corpus = new Option<FileInfo?>("--corpus") { Description = "Synthetic contract JSONL input; use either --corpus or --capture." };
        var capture = new Option<FileInfo?>("--capture") { Description = "Authorized .d3bcap input; use either --corpus or --capture." };
        var output = new Option<FileInfo?>("--out") { Description = "Write redacted compact JSON to this path; stdout by default." };
        command.Options.Add(engine); command.Options.Add(schema); command.Options.Add(version);
        command.Options.Add(corpus); command.Options.Add(capture); command.Options.Add(output);
        command.SetAction(async (result, cancellationToken) => await DiagnoseIO.RunAsync(async () =>
        {
            var schemaFile = result.GetValue(schema)!;
            if (!schemaFile.Exists) throw new DiagnoseIO.DiagnoseFailure("schema missing");
            var schemaPath = schemaFile.FullName;
            var protocolSchema = DiagnoseIO.Stage("schema invalid", () => ProtocolSchema.Load(schemaPath));
            var versionValue = result.GetValue(version)!;
            var diagnosticSchema = DiagnoseIO.Stage("schema invalid", () => DiagnosticSchemaExporter.Export(versionValue, protocolSchema));
            var buildTokens = diagnosticSchema.Messages.Select(message => message.Name).ToHashSet(StringComparer.Ordinal);
            var corpusFile = result.GetValue(corpus);
            var captureFile = result.GetValue(capture);
            if ((corpusFile is null) == (captureFile is null)) throw new InvalidDataException();
            var frameRead = corpusFile is not null
                ? DiagnoseIO.Stage("corpus invalid or oversized", () => ReadCorpus(corpusFile.FullName, buildTokens))
                : DiagnoseIO.Stage("capture or mapping invalid", () => ReadCapture(captureFile!.FullName, schemaPath, protocolSchema, versionValue, buildTokens));
            IReadOnlyList<DiagnosticFrame> frames = frameRead.Frames;
            int skippedFrames = frameRead.Skipped;
            var engineFile = result.GetValue(engine)!;
            if (!engineFile.Exists) throw new DiagnoseIO.DiagnoseFailure("engine missing");
            var replay = await DiagnoseIO.StageAsync("engine failed", async () =>
            {
                try
                {
                    return await ReplayEngineProcess.AnalyzeAsync(engineFile.FullName, ["replay"], diagnosticSchema, frames, cancellationToken);
                }
                catch (DiagnosticProtocolException exception) when (exception.Message == "The engine summary problem counts are inconsistent.")
                {
                    // Legacy fallback for an engine binary that still counted a field problem twice; current Rust and
                    // C# both count analysis-level problems only.
                    return await ReplayEngineFallback.AnalyzeAsync(engineFile.FullName, ["replay"], diagnosticSchema, frames, cancellationToken);
                }
            });
            if (skippedFrames > 0)
            {
                replay = replay with
                {
                    Summary = replay.Summary with
                    {
                        Dropped = checked(replay.Summary.Dropped + skippedFrames),
                        Coverage = "partial",
                    },
                };
            }

            var json = SerializeReplay(replay);
            DiagnoseIO.Write(result.GetValue(output), json);
        }));
        return command;
    }

    private static string SerializeReplay(ReplayResult replay)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("engine");
            JsonSerializer.Serialize(writer, replay.EngineHello, DiagnosticJsonContext.Protocol.DiagnosticEngineHello);
            writer.WritePropertyName("analyses");
            writer.WriteStartArray();
            foreach (var analysis in replay.Analyses)
            {
                JsonSerializer.Serialize(writer, analysis, DiagnosticJsonContext.Protocol.DiagnosticAnalysis);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("summary");
            JsonSerializer.Serialize(writer, replay.Summary, DiagnosticJsonContext.Protocol.DiagnosticSummary);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static (List<DiagnosticFrame> Frames, int Skipped) ReadCorpus(string path, IReadOnlySet<string> buildTokens)
    {
        var input = new FileInfo(path);
        if (!input.Exists || input.Length > MaximumCorpusCharacters) throw new InvalidDataException();
        var frames = new List<DiagnosticFrame>();
        long totalCharacters = 0;
        var inputFrames = 0;
        var skippedFrames = 0;
        foreach (string line in File.ReadLines(path))
        {
            totalCharacters += line.Length;
            if (line.Length > (DiagnosticProtocol.FramePayloadLimit * 2L) + 4096 || totalCharacters > MaximumCorpusCharacters) throw new InvalidDataException();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (++inputFrames > MaximumFrames) throw new InvalidDataException();
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
            var properties = root.EnumerateObject().ToArray();
            if (properties.Any(property => property.Name is not ("direction" or "typeUrl" or "carriedType" or "payloadHex")) ||
                properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) throw new InvalidDataException();
            var direction = root.GetProperty("direction").GetString();
            if (direction is not ("client" or "server")) throw new InvalidDataException();
            var typeUrl = ReadOptionalString(root, "typeUrl");
            var carried = ReadOptionalString(root, "carriedType");
            var hex = root.GetProperty("payloadHex").GetString();
            if (hex is null) throw new InvalidDataException();
            if (hex.Length > DiagnosticProtocol.FramePayloadLimit * 2) throw new InvalidDataException();
            if (hex.Any(character => !(character is >= '0' and <= '9' or >= 'a' and <= 'f'))) throw new InvalidDataException();
            if (!DiagnosticFrameEligibility.IsEligible(typeUrl, carried, buildTokens))
            {
                skippedFrames++;
                continue;
            }

            frames.Add(new DiagnosticFrame(frames.Count, direction, typeUrl, carried, Convert.FromHexString(hex)));
        }
        return (frames, skippedFrames);
    }

    private static (List<DiagnosticFrame> Frames, int Skipped) ReadCapture(
        string path,
        string schemaPath,
        ProtocolSchema schema,
        string version,
        IReadOnlySet<string> buildTokens)
    {
        var captureFile = new FileInfo(path);
        if (!captureFile.Exists || captureFile.Length > MaximumCaptureBytes) throw new InvalidDataException();
        var mappingPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(schemaPath))!, MappingFile.FileName);
        if (!File.Exists(mappingPath)) throw new FileNotFoundException();
        var mapping = MappingFile.Read(mappingPath);
        if (!string.Equals(mapping.ClientVersion, version, StringComparison.Ordinal)) throw new InvalidDataException();
        var translation = ProtocolTranslation.Create(schema, mapping);
        var captured = CaptureFrames.Read(path, translation);
        if (captured.IsTruncated || captured.Frames.Count > MaximumFrames) throw new InvalidDataException();
        var result = new List<DiagnosticFrame>();
        var skippedFrames = 0;
        foreach (var item in captured.Frames)
        {
            var decoded = item.Frame;
            var content = decoded.Envelope?.ContentCase switch
            {
                GameMessage.ContentOneofCase.Request => decoded.Envelope.Request.Content,
                GameMessage.ContentOneofCase.Response => decoded.Envelope.Response.Content,
                GameMessage.ContentOneofCase.Event => decoded.Envelope.Event.Content,
                _ => null,
            };
            if (content is null)
            {
                skippedFrames++;
                continue;
            }

            var direction = item.Source switch { FrameSource.Client => "client", FrameSource.Server => "server", _ => "d3b" };
            if (content.Value.Length > DiagnosticProtocol.FramePayloadLimit) throw new InvalidDataException();
            if (!DiagnosticFrameEligibility.IsEligible(content.TypeUrl, decoded.CarriedType, buildTokens))
            {
                skippedFrames++;
                continue;
            }

            result.Add(new DiagnosticFrame(result.Count, direction, content.TypeUrl, decoded.CarriedType, content.Value.ToByteArray()));
        }
        return (result, skippedFrames);
    }

    private static string? ReadOptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new InvalidDataException();
        return value.GetString();
    }
}
