using System.Diagnostics;
using System.Text;

namespace D3B.Diagnostic.Replay;

/// <summary>Starts a caller-selected diagnostic engine executable for one replay.</summary>
public sealed class ReplayEngineProcess
{
    /// <summary>Runs the requested executable with redirected standard streams.</summary>
    public static async Task<ReplayResult> AnalyzeAsync(
        string executable,
        IReadOnlyList<string> arguments,
        D3B.Diagnostic.Contract.DiagnosticSchema schema,
        IReadOnlyList<DiagnosticFrame> frames,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        using var process = new Process
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
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The diagnostic engine process did not start.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new DiagnosticEngineException(-1, "stderr unavailable; contents redacted.");
        }

        var stderrTask = ReadStderrSummaryAsync(process.StandardError.BaseStream, cancellationToken);
        var client = new ReplayEngineClient(
            process.StandardOutput.BaseStream,
            process.StandardInput.BaseStream);
        ReplayResult result;
        try
        {
            result = await client.AnalyzeAsync(schema, frames, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            var caughtStderrSummary = await stderrTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new DiagnosticEngineException(process.ExitCode, caughtStderrSummary);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stderrSummary = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new DiagnosticEngineException(process.ExitCode, stderrSummary);
        }

        return result;
    }

    private static async Task<string> ReadStderrSummaryAsync(Stream stderr, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var tail = new byte[4096];
        var tailCount = 0;
        var tailPosition = 0;
        long count = 0;
        int read;
        while ((read = await stderr.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            count += read;
            for (var index = 0; index < read; index++)
            {
                tail[tailPosition] = buffer[index];
                tailPosition = (tailPosition + 1) % tail.Length;
                tailCount = Math.Min(tailCount + 1, tail.Length);
            }
        }

        var orderedTail = new byte[tailCount];
        var tailStart = (tailPosition - tailCount + tail.Length) % tail.Length;
        for (var index = 0; index < tailCount; index++)
        {
            orderedTail[index] = tail[(tailStart + index) % tail.Length];
        }

        var recognizedCodes = ExtractKnownCodes(Encoding.UTF8.GetString(orderedTail));
        var codeSummary = recognizedCodes.Length == 0 ? "none" : string.Join(", ", recognizedCodes);
        return $"stderr bytes: {count}; tail codes: {codeSummary}; other content redacted.";
    }

    private static string[] ExtractKnownCodes(string tail)
    {
        string[] knownCodes =
        [
            "UNKNOWN_TYPE",
            "UNKNOWN_FIELD_NUMBER",
            "WIRE_TYPE_MISMATCH",
            "PACKED_NOT_ALLOWED",
            "INVALID_UTF8",
            "GROUP_UNSUPPORTED",
            "DEPTH_EXCEEDED",
            "LENGTH_OVERFLOW",
            "VARINT_OVERFLOW",
            "TRUNCATED",
            "LIMIT_EXCEEDED",
            "SCHEMA_MESSAGE_MISSING",
            "SCHEMA_INVALID",
        ];
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var tokenStart = -1;
        for (var index = 0; index <= tail.Length; index++)
        {
            var isTokenCharacter = index < tail.Length &&
                (char.IsAsciiLetterOrDigit(tail[index]) || tail[index] == '_');
            if (isTokenCharacter && tokenStart < 0)
            {
                tokenStart = index;
            }
            else if (!isTokenCharacter && tokenStart >= 0)
            {
                var token = tail[tokenStart..index];
                if (knownCodes.Contains(token, StringComparer.Ordinal))
                {
                    counts.TryGetValue(token, out var currentCount);
                    counts[token] = currentCount + 1;
                }

                tokenStart = -1;
            }
        }

        return counts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value == 1 ? pair.Key : $"{pair.Key} x{pair.Value}")
            .ToArray();
    }
}
