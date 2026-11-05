using System.Text;
using System.Text.Json;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Replay;

/// <summary>Runs the JSONL replay protocol over caller-owned streams.</summary>
public sealed class ReplayEngineClient
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly Func<CancellationToken, Task<int>>? _waitForExit;

    /// <summary>Creates a replay client over engine stdout and stdin streams.</summary>
    public ReplayEngineClient(
        Stream input,
        Stream output,
        Func<CancellationToken, Task<int>>? waitForExit = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        _input = input;
        _output = output;
        _waitForExit = waitForExit;
    }

    /// <summary>Sends a schema and frames, then validates all engine responses.</summary>
    public async Task<ReplayResult> AnalyzeAsync(
        DiagnosticSchema schema,
        IReadOnlyList<DiagnosticFrame> frames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(frames);
        ValidateFrames(frames);

        var reader = new BoundedJsonLineReader(_input);
        var hello = new DiagnosticHello
        {
            ClientVersion = schema.ClientVersion,
            SchemaSha256 = schema.SchemaSha256,
        };
        await WriteLineAsync(DiagnosticJson.SerializeHello(hello), cancellationToken).ConfigureAwait(false);
        await WriteLineAsync(DiagnosticJson.SerializeSchema(schema), cancellationToken).ConfigureAwait(false);
        var helloLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new DiagnosticProtocolException("The engine closed before its hello.");
        await ThrowIfErrorAsync(helloLine, cancellationToken).ConfigureAwait(false);
        var engineHello = DiagnosticJson.DeserializeHello(Encoding.UTF8.GetBytes(helloLine));
        ValidateEngineHello(engineHello);

        var analyses = new List<DiagnosticAnalysis>(frames.Count);
        foreach (var frame in frames)
        {
            await WriteFrameAsync(frame, cancellationToken).ConfigureAwait(false);
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new DiagnosticProtocolException("The engine output ended before all analyses were received.");
            await ThrowIfErrorAsync(line, cancellationToken).ConfigureAwait(false);
            var analysis = DiagnosticJson.DeserializeAnalysis(Encoding.UTF8.GetBytes(line));
            if (analysis.Kind != "analysis" || analysis.Index != frame.Index ||
                analysis.Direction != frame.Direction || analysis.CarriedType != frame.CarriedType ||
                analysis.TypeUrlOk != IsTypeUrlValid(frame))
            {
                throw new DiagnosticProtocolException("The engine analysis did not match its input frame.");
            }

            analyses.Add(analysis);
        }

        await WriteLineAsync(DiagnosticJson.SerializeEnd(new DiagnosticEnd()), cancellationToken).ConfigureAwait(false);
        var summaryLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new DiagnosticProtocolException("The engine output ended before its summary.");
        await ThrowIfErrorAsync(summaryLine, cancellationToken).ConfigureAwait(false);
        var summary = DiagnosticJson.DeserializeSummary(Encoding.UTF8.GetBytes(summaryLine));
        ValidateSummary(summary, schema, frames.Count, analyses);
        if (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new DiagnosticProtocolException("The engine wrote data after its summary.");
        }

        if (_waitForExit is not null)
        {
            var exitCode = await _waitForExit(cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new DiagnosticEngineException(exitCode, "stderr contained output; contents redacted.");
            }
        }

        return new ReplayResult(engineHello, analyses, summary);
    }

    private static void ValidateEngineHello(DiagnosticEngineHello hello)
    {
        if (hello.Kind != "hello" || hello.ProtocolVersion != DiagnosticProtocol.ProtocolVersion ||
            hello.Engine != "d3b-diagnostic")
        {
            throw new DiagnosticProtocolException("The engine hello is incompatible.");
        }
    }

    private static void ValidateSummary(
        DiagnosticSummary summary,
        DiagnosticSchema schema,
        int frameCount,
        IReadOnlyList<DiagnosticAnalysis> analyses)
    {
        if (summary.Kind != "summary" || summary.ProtocolVersion != DiagnosticProtocol.ProtocolVersion ||
            summary.ClientVersion != schema.ClientVersion || summary.SchemaSha256 != schema.SchemaSha256 ||
            summary.Frames != frameCount || summary.Dropped != 0 || summary.Coverage != "complete")
        {
            throw new DiagnosticProtocolException("The engine summary does not match the replay.");
        }

        var expectedStatuses = analyses.GroupBy(analysis => analysis.Status)
            .ToDictionary(group => group.Key, group => (long)group.Count());
        if (summary.Statuses.Count != expectedStatuses.Count ||
            expectedStatuses.Any(pair => !summary.Statuses.TryGetValue(pair.Key, out var count) || count != pair.Value))
        {
            throw new DiagnosticProtocolException("The engine summary status counts are inconsistent.");
        }

        var expectedProblems = new Dictionary<DiagnosticProblemCode, long>();
        foreach (var analysis in analyses)
        {
            foreach (var problem in analysis.Problems)
            {
                AddProblem(problem.Code);
            }

            foreach (var field in analysis.Fields)
            {
                AddFieldProblems(field);
            }
        }

        if (summary.Problems.Count != expectedProblems.Count ||
            expectedProblems.Any(pair => !summary.Problems.TryGetValue(pair.Key, out var count) || count != pair.Value))
        {
            throw new DiagnosticProtocolException("The engine summary problem counts are inconsistent.");
        }

        void AddProblem(DiagnosticProblemCode problem)
        {
            expectedProblems.TryGetValue(problem, out var count);
            expectedProblems[problem] = count + 1;
        }

        void AddFieldProblems(DiagnosticFieldAnalysis field)
        {
            foreach (var problem in field.Problems)
            {
                AddProblem(problem);
            }

            if (field.Nested is not null)
            {
                foreach (var nested in field.Nested)
                {
                    AddFieldProblems(nested);
                }
            }
        }
    }

    private static void ValidateFrames(IReadOnlyList<DiagnosticFrame> frames)
    {
        long previous = -1;
        foreach (var frame in frames)
        {
            if (frame.Index <= previous)
            {
                throw new DiagnosticProtocolException("Replay frame indices must be strictly increasing.");
            }

            previous = frame.Index;
        }
    }

    private static bool IsTypeUrlValid(DiagnosticFrame frame)
    {
        return frame.TypeUrl is not null && frame.CarriedType is not null &&
            string.Equals(frame.TypeUrl, DiagnosticProtocol.TypeUrlPrefix + frame.CarriedType, StringComparison.Ordinal);
    }

    private async Task WriteFrameAsync(DiagnosticFrame frame, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("kind", "frame");
            writer.WriteNumber("index", frame.Index);
            writer.WriteString("direction", frame.Direction);
            if (frame.TypeUrl is null) writer.WriteNull("typeUrl"); else writer.WriteString("typeUrl", frame.TypeUrl);
            if (frame.CarriedType is null) writer.WriteNull("carriedType"); else writer.WriteString("carriedType", frame.CarriedType);
            writer.WriteString("payloadHex", Convert.ToHexStringLower(frame.Payload));
            writer.WriteEndObject();
        }

        await WriteLineAsync(buffer.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteLineAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        if (bytes.Length > DiagnosticProtocol.MessageLimit)
        {
            throw new DiagnosticProtocolException("A replay line exceeds the protocol limit.");
        }

        await _output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await _output.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ThrowIfErrorAsync(string line, CancellationToken cancellationToken)
    {
        using var document = ParseLine(line);
        if (document.RootElement.TryGetProperty("kind", out var kind) &&
            kind.ValueKind == JsonValueKind.String && kind.GetString() == "error")
        {
            _ = DiagnosticJson.DeserializeError(Encoding.UTF8.GetBytes(line));

            if (_waitForExit is not null)
            {
                var exitCode = await _waitForExit(cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new DiagnosticEngineException(exitCode, "The engine returned a redacted error line.");
                }
            }

            throw new DiagnosticProtocolException("The diagnostic engine returned an error.");
        }
    }

    private static JsonDocument ParseLine(string line)
    {
        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            throw new DiagnosticProtocolException("The engine returned malformed JSON.");
        }
    }
}
