using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Protocol.Mapping;
using Google.Protobuf;
using Google.Protobuf.Reflection;

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
        return Read(paths, null, null);
    }

    /// <summary>Reads captures and optionally checks their messages against supplied schema and mapping files.</summary>
    /// <param name="paths">The capture paths.</param>
    /// <param name="schemaPath">A schema file, or null when schema diagnostics are unavailable.</param>
    /// <param name="mappingPath">A mapping file, or null when mapping diagnostics are unavailable.</param>
    /// <returns>The report.</returns>
    public static CaptureDiagnosticReport Read(IEnumerable<string> paths, string? schemaPath = null, string? mappingPath = null)
    {
        ArgumentNullException.ThrowIfNull(paths);

        SchemaMatch? schema = null;
        CaptureDiagnostic? schemaError = null;

        if (schemaPath is not null)
        {
            try
            {
                schema = SchemaMatch.Load(schemaPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidProtocolBufferException or DescriptorValidationException)
            {
                schemaError = InputError("schema.file_invalid", CaptureDiagnosticStage.Schema, "Schema file could not be loaded.");
            }
        }

        ProtocolMapping? mapping = null;
        string? mappingSha256 = null;
        CaptureDiagnostic? mappingError = null;

        if (mappingPath is not null)
        {
            try
            {
                var mappingBytes = File.ReadAllBytes(mappingPath);
                mappingSha256 = Convert.ToHexStringLower(SHA256.HashData(mappingBytes));
                mapping = MappingFile.Read(mappingPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidProtocolBufferException)
            {
                mappingError = InputError("mapping.file_invalid", CaptureDiagnosticStage.Mapping, "Mapping file could not be loaded.");
            }
        }

        var captures = paths
            .Select(path => ReadCapture(path, schema, schemaError, mapping, mappingSha256, mappingError))
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

    private static CaptureDiagnosticCapture ReadCapture(
        string path,
        SchemaMatch? schema,
        CaptureDiagnostic? schemaError,
        ProtocolMapping? mapping,
        string? mappingSha256,
        CaptureDiagnostic? mappingError)
    {
        var fileName = Path.GetFileName(path);
        var diagnostics = new List<CaptureDiagnostic>();

        if (schemaError is not null)
            diagnostics.Add(schemaError);

        if (mappingError is not null)
            diagnostics.Add(mappingError);

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
        var messages = GameMessages.Extract(records, true);
        var messageSummary = AnalyzeMessages(messages, schema, schemaError, mapping, mappingError, header?.ClientVersion, diagnostics);

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
                SchemaVersion = schema is null ? null : SchemaFile.FormatVersion.ToString(CultureInfo.InvariantCulture),
                SchemaSha256 = schema?.Sha256,
                MappingVersion = mapping is null ? null : mapping.FormatVersion.ToString(CultureInfo.InvariantCulture),
                MappingSha256 = mappingSha256,
                MappingClientVersionCompatible = mapping is null || string.IsNullOrEmpty(header?.ClientVersion) ? null : mapping.ClientVersion == header.ClientVersion,
            },
            Summary = new CaptureDiagnosticSummary
            {
                Records = records.Count,
                RecordsByKind = counts,
                BytesByKind = bytes,
                Directions = new CaptureDiagnosticDirections { Client = client, Server = server, Injected = injected },
                Messages = messageSummary,
                Truncated = truncated,
            },
            Diagnostics = diagnostics
                .OrderBy(diagnostic => diagnostic.RecordIndex ?? -1)
                .ThenBy(diagnostic => diagnostic.FrameIndex ?? -1)
                .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ToArray(),
            Status = captureStatus,
        };
    }

    private static CaptureDiagnosticMessages AnalyzeMessages(
        IReadOnlyList<GameMessage> messages,
        SchemaMatch? schema,
        CaptureDiagnostic? schemaError,
        ProtocolMapping? mapping,
        CaptureDiagnostic? mappingError,
        string? clientVersion,
        List<CaptureDiagnostic> diagnostics)
    {
        var withTypeUrl = messages.Where(message => message.TypeUrl is not null).ToArray();
        foreach (var message in messages.Where(message => message.TypeUrl is null))
        {
            diagnostics.Add(new CaptureDiagnostic
            {
                Code = "message.unknown_type",
                Severity = CaptureDiagnosticSeverity.Info,
                Stage = CaptureDiagnosticStage.Message,
                Direction = message.Side,
                RecordIndex = message.RecordIndex,
                FrameIndex = message.FrameIndex,
                ByteOffset = message.ByteOffset,
                Message = "Frame has no message type URL.",
                Details = new Dictionary<string, string>
                {
                    ["member"] = message.Member,
                    ["messageIndex"] = message.MessageIndex.ToString(CultureInfo.InvariantCulture),
                },
            });
        }

        var known = 0L;
        var unknown = 0L;
        var mapped = 0L;
        var unmapped = 0L;
        var schemaMessages = schema?.Messages.ToDictionary(message => message.FullName, StringComparer.Ordinal);

        if (schema is null)
        {
            if (schemaError is null && withTypeUrl.Length > 0)
            {
                diagnostics.Add(new CaptureDiagnostic
                {
                    Code = "schema.not_provided",
                    Severity = CaptureDiagnosticSeverity.Info,
                    Stage = CaptureDiagnosticStage.Schema,
                    Message = "No schema was supplied; message types and payloads were not checked.",
                });
            }
        }
        else
        {
            foreach (var message in withTypeUrl)
            {
                var descriptor = schemaMessages!.GetValueOrDefault(message.TypeName!);

                if (descriptor is null)
                {
                    unknown++;
                    diagnostics.Add(MessageDiagnostic(
                        message,
                        "schema.message_unknown",
                        CaptureDiagnosticSeverity.Warning,
                        CaptureDiagnosticStage.Schema,
                        "Type URL does not name a message in the supplied schema."));

                    continue;
                }

                known++;

                if (!SchemaMatch.Accepts(descriptor, message.Content))
                {
                    diagnostics.Add(MessageDiagnostic(
                        message,
                        "schema.payload_mismatch",
                        CaptureDiagnosticSeverity.Error,
                        CaptureDiagnosticStage.Schema,
                        "Message payload fields do not fit the declared schema message."));
                }
            }
        }

        var mappingSupported = mapping is not null && mapping.FormatVersion == MappingFile.FormatVersion;

        if (mapping is not null && !mappingSupported)
        {
            diagnostics.Add(new CaptureDiagnostic
            {
                Code = "mapping.format_unsupported",
                Severity = CaptureDiagnosticSeverity.Error,
                Stage = CaptureDiagnosticStage.Mapping,
                Message = $"Mapping format {mapping.FormatVersion} is not supported; expected {MappingFile.FormatVersion}.",
                Details = new Dictionary<string, string>
                {
                    ["formatVersion"] = mapping.FormatVersion.ToString(CultureInfo.InvariantCulture),
                    ["supportedFormatVersion"] = MappingFile.FormatVersion.ToString(CultureInfo.InvariantCulture),
                },
            });
        }

        if (mappingSupported)
        {
            if (!string.IsNullOrEmpty(clientVersion) && mapping!.ClientVersion != clientVersion)
            {
                diagnostics.Add(new CaptureDiagnostic
                {
                    Code = "mapping.build_mismatch",
                    Severity = CaptureDiagnosticSeverity.Error,
                    Stage = CaptureDiagnosticStage.Mapping,
                    Message = "Mapping client version does not match the capture header.",
                    Details = new Dictionary<string, string>
                    {
                        ["captureClientVersion"] = clientVersion,
                        ["mappingClientVersion"] = mapping.ClientVersion,
                    },
                });
            }

            if (schema is not null && !mapping!.SchemaSha256.Span.SequenceEqual(Convert.FromHexString(schema.Sha256)))
            {
                diagnostics.Add(new CaptureDiagnostic
                {
                    Code = "mapping.schema_mismatch",
                    Severity = CaptureDiagnosticSeverity.Error,
                    Stage = CaptureDiagnosticStage.Mapping,
                    Message = "Mapping schema SHA-256 does not match the supplied schema file.",
                    Details = new Dictionary<string, string>
                    {
                        ["mappingSchemaSha256"] = Convert.ToHexStringLower(mapping.SchemaSha256.Span),
                        ["schemaSha256"] = schema.Sha256,
                    },
                });
            }

            var mappedNames = mapping!.Messages.Select(pair => pair.Obfuscated).ToHashSet(StringComparer.Ordinal);

            foreach (var message in withTypeUrl)
            {
                if (mappedNames.Contains(message.TypeName!))
                {
                    mapped++;

                    continue;
                }

                unmapped++;
                diagnostics.Add(MessageDiagnostic(
                    message,
                    "mapping.message_unknown",
                    CaptureDiagnosticSeverity.Info,
                    CaptureDiagnosticStage.Mapping,
                    "Type URL has no message entry in the supplied mapping."));
            }
        }
        else if (mapping is null && mappingError is null && withTypeUrl.Length > 0)
        {
            diagnostics.Add(new CaptureDiagnostic
            {
                Code = "mapping.not_provided",
                Severity = CaptureDiagnosticSeverity.Info,
                Stage = CaptureDiagnosticStage.Mapping,
                Message = "No mapping was supplied; readable message names were not checked.",
            });
        }

        return new CaptureDiagnosticMessages
        {
            Total = messages.Count,
            ByMember = CountBy(messages, message => message.Member),
            BySide = CountBy(messages, message => message.Side),
            ByTypeUrl = CountBy(messages, message => message.TypeUrl ?? "(none)"),
            ByMemberAndTypeUrl = messages
                .GroupBy(message => (message.Member, TypeUrl: message.TypeUrl ?? "(none)"))
                .OrderBy(group => group.Key.Member, StringComparer.Ordinal)
                .ThenBy(group => group.Key.TypeUrl, StringComparer.Ordinal)
                .Select(group => new CaptureDiagnosticMessageCount
                {
                    Member = group.Key.Member,
                    TypeUrl = group.Key.TypeUrl,
                    Count = group.LongCount(),
                })
                .ToArray(),
            Known = known,
            Unknown = unknown,
            Mapped = mapped,
            Unmapped = unmapped,
        };
    }

    private static CaptureDiagnostic MessageDiagnostic(
        GameMessage message,
        string code,
        CaptureDiagnosticSeverity severity,
        CaptureDiagnosticStage stage,
        string description)
    {
        return new CaptureDiagnostic
        {
            Code = code,
            Severity = severity,
            Stage = stage,
            Direction = message.Side,
            RecordIndex = message.RecordIndex,
            FrameIndex = message.FrameIndex,
            ByteOffset = message.ByteOffset,
            Message = description,
            Details = new Dictionary<string, string>
            {
                ["typeUrl"] = message.TypeUrl!,
                ["messageIndex"] = message.MessageIndex.ToString(CultureInfo.InvariantCulture),
            },
        };
    }

    private static SortedDictionary<string, long> CountBy(IReadOnlyList<GameMessage> messages, Func<GameMessage, string> keyOf)
    {
        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);

        foreach (var message in messages)
        {
            var key = keyOf(message);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    private static CaptureDiagnostic InputError(string code, CaptureDiagnosticStage stage, string message)
    {
        return new CaptureDiagnostic
        {
            Code = code,
            Severity = CaptureDiagnosticSeverity.Error,
            Stage = stage,
            Message = message,
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
