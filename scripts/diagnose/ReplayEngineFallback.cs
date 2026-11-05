using System.Diagnostics;
using System.Text.Json;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Replay;

namespace D3B.Scripts.Diagnose;

internal static class ReplayEngineFallback
{
    public static async Task<ReplayResult> AnalyzeAsync(
        string executable,
        IReadOnlyList<string> arguments,
        DiagnosticSchema schema,
        IReadOnlyList<DiagnosticFrame> frames,
        CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        using (process)
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The diagnostic engine did not start.");
            }

            Task stderrDrain = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
            try
            {
                await WriteLineAsync(process.StandardInput.BaseStream, DiagnosticJson.SerializeHello(new DiagnosticHello
                {
                    ClientVersion = schema.ClientVersion,
                    SchemaSha256 = schema.SchemaSha256,
                }), cancellationToken);
                await WriteLineAsync(process.StandardInput.BaseStream, DiagnosticJson.SerializeSchema(schema), cancellationToken);

                var reader = new BoundedLineReader(process.StandardOutput.BaseStream);
                byte[] helloLine = await ReadRequiredLineAsync(reader, cancellationToken);
                ThrowIfEngineError(helloLine);
                var engineHello = DiagnosticJson.DeserializeHello(helloLine);
                if (engineHello.Kind != "hello" || engineHello.ProtocolVersion != DiagnosticProtocol.ProtocolVersion ||
                    engineHello.Engine != "d3b-diagnostic")
                {
                    throw new InvalidDataException();
                }

                List<DiagnosticAnalysis> analyses = new(frames.Count);
                foreach (DiagnosticFrame frame in frames)
                {
                    await WriteFrameAsync(process.StandardInput.BaseStream, frame, cancellationToken);
                    byte[] analysisLine = await ReadRequiredLineAsync(reader, cancellationToken);
                    ThrowIfEngineError(analysisLine);
                    DiagnosticAnalysis analysis = DiagnosticJson.DeserializeAnalysis(analysisLine);
                    if (analysis.Kind != "analysis" || analysis.Index != frame.Index || analysis.Direction != frame.Direction ||
                        analysis.CarriedType != frame.CarriedType || analysis.TypeUrlOk != IsTypeUrlValid(frame))
                    {
                        throw new InvalidDataException();
                    }

                    analyses.Add(analysis);
                }

                await WriteLineAsync(process.StandardInput.BaseStream, DiagnosticJson.SerializeEnd(new DiagnosticEnd()), cancellationToken);
                process.StandardInput.Close();
                byte[] summaryLine = await ReadRequiredLineAsync(reader, cancellationToken);
                ThrowIfEngineError(summaryLine);
                DiagnosticSummary summary = DiagnosticJson.DeserializeSummary(summaryLine);
                ValidateSummary(summary, schema, frames.Count, analyses);
                if (await reader.ReadLineAsync(cancellationToken) is not null)
                {
                    throw new InvalidDataException();
                }

                await process.WaitForExitAsync(cancellationToken);
                await stderrDrain;
                if (process.ExitCode != 0)
                {
                    throw new DiagnosticEngineException(process.ExitCode, "engine output redacted");
                }

                return new ReplayResult(engineHello, analyses, summary);
            }
            catch
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }

                try
                {
                    await stderrDrain;
                }
                catch (OperationCanceledException)
                {
                }

                throw;
            }
        }
    }

    private static async Task WriteFrameAsync(Stream output, DiagnosticFrame frame, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("kind", "frame");
            writer.WriteNumber("index", frame.Index);
            writer.WriteString("direction", frame.Direction);
            if (frame.TypeUrl is not null)
            {
                writer.WriteString("typeUrl", frame.TypeUrl);
            }

            if (frame.CarriedType is not null)
            {
                writer.WriteString("carriedType", frame.CarriedType);
            }

            writer.WriteString("payloadHex", Convert.ToHexStringLower(frame.Payload));
            writer.WriteEndObject();
        }

        await WriteLineAsync(output, buffer.ToArray(), cancellationToken);
    }

    private static async Task WriteLineAsync(Stream output, byte[] line, CancellationToken cancellationToken)
    {
        if (line.Length > DiagnosticProtocol.MessageLimit)
        {
            throw new InvalidDataException();
        }

        await output.WriteAsync(line, cancellationToken);
        await output.WriteAsync("\n"u8.ToArray(), cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private static async Task<byte[]> ReadRequiredLineAsync(BoundedLineReader reader, CancellationToken cancellationToken)
    {
        return await reader.ReadLineAsync(cancellationToken) ?? throw new InvalidDataException();
    }

    private static void ThrowIfEngineError(byte[] line)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        if (document.RootElement.TryGetProperty("kind", out JsonElement kind) && kind.GetString() == "error")
        {
            DiagnosticError error = DiagnosticJson.DeserializeError(line);
            throw new InvalidOperationException("The diagnostic engine returned " + error.Code + ".");
        }
    }

    private static bool IsTypeUrlValid(DiagnosticFrame frame)
    {
        return frame.TypeUrl is not null && frame.CarriedType is not null &&
            string.Equals(frame.TypeUrl, DiagnosticProtocol.TypeUrlPrefix + frame.CarriedType, StringComparison.Ordinal);
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
            throw new InvalidDataException();
        }

        Dictionary<DiagnosticStatus, long> expectedStatuses = analyses
            .GroupBy(analysis => analysis.Status)
            .ToDictionary(group => group.Key, group => (long)group.Count());
        if (summary.Statuses.Count != expectedStatuses.Count || expectedStatuses.Any(pair =>
            !summary.Statuses.TryGetValue(pair.Key, out long count) || count != pair.Value))
        {
            throw new InvalidDataException();
        }
    }

    private sealed class BoundedLineReader
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer = new byte[8192];
        private int _count;
        private int _offset;

        public BoundedLineReader(Stream stream)
        {
            _stream = stream;
        }

        public async Task<byte[]?> ReadLineAsync(CancellationToken cancellationToken)
        {
            using var line = new MemoryStream();
            while (true)
            {
                if (_offset == _count)
                {
                    _count = await _stream.ReadAsync(_buffer, cancellationToken);
                    _offset = 0;
                    if (_count == 0)
                    {
                        return line.Length == 0 ? null : line.ToArray();
                    }
                }

                int newline = Array.IndexOf(_buffer, (byte)'\n', _offset, _count - _offset);
                int length = newline < 0 ? _count - _offset : newline - _offset;
                if (line.Length + length > DiagnosticProtocol.MessageLimit)
                {
                    throw new InvalidDataException();
                }

                line.Write(_buffer, _offset, length);
                _offset += length;
                if (newline >= 0)
                {
                    _offset++;
                    return line.ToArray();
                }
            }
        }
    }
}
