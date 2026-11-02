using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using D3B.Network.Captures;
using D3B.Network.Framing;

namespace D3B.Scripts.Capture;

/// <summary>Reads captures into deterministic health and framing diagnostics.</summary>
public static class CaptureDiagnostics
{
    private const int SchemaVersion = 1;
    /// <summary>Reads one capture into the JSON report contract.</summary>
    /// <param name="path">The capture path.</param>
    /// <returns>The report containing the capture.</returns>
    public static CaptureDiagnosticReport Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Read([path]);
    }

    /// <summary>Reads captures into a report sorted by basename.</summary>
    /// <param name="paths">The capture paths.</param>
    /// <returns>The report.</returns>
    public static CaptureDiagnosticReport Read(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var captures = paths
            .Select(ReadCapture)
            .OrderBy(capture => capture.Capture.Basename, StringComparer.Ordinal)
            .ThenBy(capture => capture.Capture.Sha256, StringComparer.Ordinal)
            .ThenBy(capture => capture.Capture.Size)
            .ToArray();
        var status = captures.Length is 0 ? "error" : OverallStatus(captures.Select(capture => capture.Status));

        return new CaptureDiagnosticReport
        {
            SchemaVersion = SchemaVersion,
            Captures = captures,
            Status = status,
            ExitCode = captures.Length is 0 ? 2 : status is "error" ? 1 : 0,
        };
    }

    /// <summary>Serializes a report using stable camel-case JSON names and string enums.</summary>
    /// <param name="report">The report.</param>
    /// <returns>One JSON document.</returns>
    public static string ToJson(CaptureDiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return JsonSerializer.Serialize(report, CaptureDiagnosticJsonContext.Default.CaptureDiagnosticReport);
    }

    private static CaptureDiagnosticCapture ReadCapture(string path)
    {
        var fileName = Path.GetFileName(path);
        var diagnostics = new List<CaptureDiagnostic>();
        var records = new List<(CaptureRecord Record, long Index)>();
        string? sha256 = null;
        long? size = null;
        int? formatVersion = null;
        CaptureHeader? header = null;
        var truncated = false;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            size = stream.Length;
            sha256 = Convert.ToHexStringLower(SHA256.HashData(stream));
            formatVersion = ReadFormatVersion(stream);
            stream.Position = 0;

            using var reader = new CaptureReader(stream);
            formatVersion = reader.FormatVersion;
            header = reader.Header;

            while (true)
            {
                var recordOffset = stream.Position;

                try
                {
                    if (!reader.TryRead(out var record))
                    {
                        truncated = reader.IsTruncated;

                        if (truncated)
                        {
                            diagnostics.Add(new CaptureDiagnostic
                            {
                                Code = "capture.record_truncated",
                                Severity = CaptureDiagnosticSeverity.Warning,
                                Stage = CaptureDiagnosticStage.Capture,
                                RecordIndex = records.Count,
                                ByteOffset = recordOffset,
                                Message = $"Capture ended during record {records.Count + 1}.",
                                Details = new Dictionary<string, string> { ["recordsRead"] = records.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                            });
                        }

                        break;
                    }

                    records.Add((record, records.Count));
                }
                catch (CaptureFormatException exception)
                {
                    diagnostics.Add(new CaptureDiagnostic
                    {
                        Code = "capture.record_invalid",
                        Severity = CaptureDiagnosticSeverity.Error,
                        Stage = CaptureDiagnosticStage.Capture,
                        RecordIndex = records.Count,
                        ByteOffset = recordOffset,
                        Message = $"Record {records.Count + 1} is invalid: {exception.Message}",
                    });

                    break;
                }
            }
        }
        catch (CaptureFormatException exception)
        {
            var unsupported = formatVersion is int version && (version < 1 || version > 2);

            diagnostics.Add(new CaptureDiagnostic
            {
                Code = unsupported ? "capture.format_unsupported" : "capture.header_invalid",
                Severity = CaptureDiagnosticSeverity.Error,
                Stage = CaptureDiagnosticStage.Capture,
                ByteOffset = unsupported ? sizeof(long) : null,
                Message = unsupported
                    ? $"Capture format version {formatVersion} is not supported."
                    : $"Capture header is invalid: {exception.Message}",
                Details = unsupported ? new Dictionary<string, string> { ["formatVersion"] = formatVersion!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) } : null,
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            diagnostics.Add(new CaptureDiagnostic
            {
                Code = "capture.file_unreadable",
                Severity = CaptureDiagnosticSeverity.Error,
                Stage = CaptureDiagnosticStage.Capture,
                Message = "Capture file could not be read.",
            });
        }

        var counts = EmptyCounts();
        var bytes = EmptyCounts();

        foreach (var (record, _) in records)
        {
            var key = KindName(record.Kind);
            counts[key]++;
            bytes[key] += record.Data.Length;
        }

        var client = SummarizeDirection(records, CaptureRecordKind.FromClient, CaptureRecordKind.ClientClosed, "client", diagnostics);
        var server = SummarizeDirection(records, CaptureRecordKind.FromServer, CaptureRecordKind.ServerClosed, "server", diagnostics);
        var injected = SummarizeDirection(records, CaptureRecordKind.Injected, null, "injected", diagnostics);

        if (header is not null && records.Count is 0 && !truncated && diagnostics.Count is 0)
        {
            diagnostics.Add(new CaptureDiagnostic
            {
                Code = "capture.empty",
                Severity = CaptureDiagnosticSeverity.Info,
                Stage = CaptureDiagnosticStage.Capture,
                Message = "Capture contains no records.",
            });
        }

        var captureStatus = StatusOf(diagnostics);

        return new CaptureDiagnosticCapture
        {
            Capture = new CaptureDiagnosticFile
            {
                Basename = fileName,
                Sha256 = sha256,
                Size = size,
                FormatVersion = formatVersion,
                StartedAt = header?.StartedAt,
                Listener = header?.Listener,
                ServerHost = header?.ServerHost,
                Server = header?.Server,
            },
            Build = new CaptureDiagnosticBuild
            {
                ClientVersion = string.IsNullOrEmpty(header?.ClientVersion) ? null : header.ClientVersion,
                SchemaVersion = null,
                MappingVersion = null,
            },
            Summary = new CaptureDiagnosticSummary
            {
                Records = records.Count,
                RecordsByKind = counts,
                BytesByKind = bytes,
                Directions = new CaptureDiagnosticDirections { Client = client, Server = server, Injected = injected },
                Truncated = truncated,
            },
            Diagnostics = diagnostics,
            Status = captureStatus,
        };
    }

    private static CaptureDiagnosticDirection SummarizeDirection(
        List<(CaptureRecord Record, long Index)> records,
        CaptureRecordKind kind,
        CaptureRecordKind? closedKind,
        string direction,
        List<CaptureDiagnostic> diagnostics)
    {
        var reads = records.Where(item => item.Record.Kind == kind).ToArray();
        var byteCount = reads.Sum(item => (long)item.Record.Data.Length);
        var closed = closedKind is null ? 0 : records.LongCount(item => item.Record.Kind == closedKind);
        var frames = 0L;

        if (byteCount is 0)
            return new CaptureDiagnosticDirection { Reads = reads.LongLength, Bytes = 0, Frames = 0, Closed = closed };

        var data = BuildSequence(reads);
        var prefix = new SequenceReader<byte>(data);
        var isTls = data.Length > 2
            && prefix.TryRead(out var recordType)
            && prefix.TryRead(out var majorVersion)
            && recordType is 0x16
            && majorVersion is 0x03;

        if (isTls)
        {
            diagnostics.Add(new CaptureDiagnostic
            {
                Code = "framing.tls",
                Severity = CaptureDiagnosticSeverity.Info,
                Stage = CaptureDiagnosticStage.Framing,
                Direction = direction,
                RecordIndex = RecordIndexAt(reads, 0),
                ByteOffset = 0,
                Message = "Captured bytes begin with a TLS handshake; frame parsing was skipped.",
            });

            return new CaptureDiagnosticDirection { Reads = reads.LongLength, Bytes = byteCount, Frames = 0, Closed = closed };
        }

        var reader = new SequenceReader<byte>(data);

        while (true)
        {
            var frameOffset = reader.Consumed;

            try
            {
                if (!FrameReader.TryReadFrame(ref reader, out _))
                {
                    if (reader.Remaining > 0)
                    {
                        diagnostics.Add(new CaptureDiagnostic
                        {
                            Code = "framing.frame_incomplete",
                            Severity = CaptureDiagnosticSeverity.Warning,
                            Stage = CaptureDiagnosticStage.Framing,
                            Direction = direction,
                            RecordIndex = RecordIndexAt(reads, frameOffset),
                            FrameIndex = frames,
                            ByteOffset = frameOffset,
                            Message = $"The direction ends with an incomplete frame at byte offset {frameOffset}.",
                            Details = new Dictionary<string, string>
                            {
                                ["remainingBytes"] = reader.Remaining.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            },
                        });
                    }

                    break;
                }

                frames++;
            }
            catch (FrameFormatException exception)
            {
                diagnostics.Add(new CaptureDiagnostic
                {
                    Code = "framing.length_invalid",
                    Severity = CaptureDiagnosticSeverity.Error,
                    Stage = CaptureDiagnosticStage.Framing,
                    Direction = direction,
                    RecordIndex = RecordIndexAt(reads, frameOffset),
                    FrameIndex = frames,
                    ByteOffset = frameOffset,
                    Message = $"Frame length is invalid at byte offset {frameOffset}: {exception.Message}",
                });

                break;
            }
        }

        return new CaptureDiagnosticDirection { Reads = reads.LongLength, Bytes = byteCount, Frames = frames, Closed = closed };
    }

    private static long? RecordIndexAt((CaptureRecord Record, long Index)[] reads, long byteOffset)
    {
        var offset = 0L;

        foreach (var (record, index) in reads)
        {
            var end = offset + record.Data.Length;

            if (byteOffset >= offset && byteOffset < end)
                return index;

            offset = end;
        }

        return null;
    }

    private static ReadOnlySequence<byte> BuildSequence((CaptureRecord Record, long Index)[] reads)
    {
        var first = new CaptureDiagnosticByteSegment(reads[0].Record.Data);
        var last = first;

        foreach (var read in reads.Skip(1))
            last = last.Append(read.Record.Data);

        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private static Dictionary<string, long> EmptyCounts()
    {
        return new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["fromClient"] = 0,
            ["fromServer"] = 0,
            ["clientClosed"] = 0,
            ["serverClosed"] = 0,
            ["injected"] = 0,
        };
    }

    private static string KindName(CaptureRecordKind kind)
    {
        return kind switch
        {
            CaptureRecordKind.FromClient => "fromClient",
            CaptureRecordKind.FromServer => "fromServer",
            CaptureRecordKind.ClientClosed => "clientClosed",
            CaptureRecordKind.ServerClosed => "serverClosed",
            CaptureRecordKind.Injected => "injected",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown capture record kind."),
        };
    }

    private static int? ReadFormatVersion(FileStream stream)
    {
        if (stream.Length < 12)
            return null;

        try
        {
            stream.Position = 0;
            Span<byte> header = stackalloc byte[12];
            stream.ReadExactly(header);

            if (!header[..8].SequenceEqual("D3B-CAPT"u8))
                return null;

            return BinaryPrimitives.ReadInt32LittleEndian(header[8..]);
        }
        finally
        {
            stream.Position = 0;
        }
    }

    private static string StatusOf(IEnumerable<CaptureDiagnostic> diagnostics)
    {
        if (diagnostics.Any(diagnostic => diagnostic.Severity is CaptureDiagnosticSeverity.Error))
            return "error";

        if (diagnostics.Any(diagnostic => diagnostic.Severity is CaptureDiagnosticSeverity.Warning))
            return "warning";

        return "ok";
    }

    private static string OverallStatus(IEnumerable<string> statuses)
    {
        if (statuses.Contains("error", StringComparer.Ordinal))
            return "error";

        if (statuses.Contains("warning", StringComparer.Ordinal))
            return "warning";

        return "ok";
    }

}
