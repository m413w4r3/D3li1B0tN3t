using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Protocol.Mapping;
using D3B.Scripts.Capture;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace D3B.Scripts.Tests.Captures;

public sealed class CaptureDiagnosticsTests
{
    private const string TypeUrlPrefix = "type.ankama.com/";

    private static readonly CaptureHeader _header = new()
    {
        StartedAt = new DateTimeOffset(2026, 10, 27, 20, 0, 0, 123, TimeSpan.Zero),
        ClientVersion = "3.7.12.12",
        Listener = "127.0.0.1:5555",
        ServerHost = "connection.test",
        Server = "192.0.2.1:5555",
    };

    [Fact]
    public void AValidCaptureReportsIdentityCountsBytesAndFrames()
    {
        using var directory = new CaptureTestDirectory();
        var client = new byte[] { 1, 0x08 };
        var server = new byte[] { 2, 0x08, 0x01 };
        var path = WriteCapture(directory, "session.d3bcap",
            (CaptureRecordKind.FromClient, client),
            (CaptureRecordKind.FromServer, server),
            (CaptureRecordKind.ClientClosed, []),
            (CaptureRecordKind.ServerClosed, []));
        var bytes = File.ReadAllBytes(path);

        var report = CaptureDiagnostics.Read(path);
        var capture = Assert.Single(report.Captures);

        Assert.Equal("ok", report.Status);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal("ok", capture.Status);
        Assert.Equal("session.d3bcap", capture.Capture.Basename);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), capture.Capture.Sha256);
        Assert.Equal((long?)bytes.Length, capture.Capture.Size);
        Assert.Equal((int?)2, capture.Capture.FormatVersion);
        Assert.Equal((DateTimeOffset?)_header.StartedAt, capture.Capture.StartedAt);
        Assert.Equal(_header.Listener, capture.Capture.Listener);
        Assert.Equal(_header.ServerHost, capture.Capture.ServerHost);
        Assert.Equal(_header.Server, capture.Capture.Server);
        Assert.Equal(_header.ClientVersion, capture.Build.ClientVersion);
        Assert.Null(capture.Build.SchemaVersion);
        Assert.Null(capture.Build.MappingVersion);
        Assert.Equal(4L, capture.Summary.Records);
        Assert.Equal(1L, capture.Summary.RecordsByKind["fromClient"]);
        Assert.Equal(1L, capture.Summary.RecordsByKind["fromServer"]);
        Assert.Equal(1L, capture.Summary.RecordsByKind["clientClosed"]);
        Assert.Equal(1L, capture.Summary.RecordsByKind["serverClosed"]);
        Assert.Equal(2L, capture.Summary.BytesByKind["fromClient"]);
        Assert.Equal(3L, capture.Summary.BytesByKind["fromServer"]);
        Assert.Equal(1L, capture.Summary.Directions.Client.Frames);
        Assert.Equal(1L, capture.Summary.Directions.Server.Frames);
        Assert.Equal(1L, capture.Summary.Directions.Client.Closed);
        Assert.Equal(1L, capture.Summary.Directions.Server.Closed);
        Assert.False(capture.Summary.Truncated);
    }

    [Fact]
    public void AnEmptyCaptureIsOkAndInformational()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "empty.d3bcap");

        var report = CaptureDiagnostics.Read(path);
        var capture = Assert.Single(report.Captures);

        Assert.Equal("ok", report.Status);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal(0L, capture.Summary.Records);
        var diagnostic = Assert.Single(capture.Diagnostics);
        Assert.Equal(CaptureDiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal("capture.empty", diagnostic.Code);

        var noCaptures = CaptureDiagnostics.Read(Array.Empty<string>());

        Assert.Empty(noCaptures.Captures);
        Assert.Equal("error", noCaptures.Status);
        Assert.Equal(2, noCaptures.ExitCode);
    }

    [Fact]
    public void AlternatingClientAndServerRecordsKeepDirectionSummaries()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "alternating.d3bcap",
            (CaptureRecordKind.FromClient, new byte[] { 1, 0x08 }),
            (CaptureRecordKind.FromServer, new byte[] { 1, 0x10 }),
            (CaptureRecordKind.FromClient, new byte[] { 1, 0x18 }));

        var capture = Assert.Single(CaptureDiagnostics.Read(path).Captures);

        Assert.Equal(3L, capture.Summary.Records);
        Assert.Equal(2L, capture.Summary.Directions.Client.Reads);
        Assert.Equal(2L, capture.Summary.Directions.Client.Frames);
        Assert.Equal(1L, capture.Summary.Directions.Server.Reads);
        Assert.Equal(1L, capture.Summary.Directions.Server.Frames);
    }

    [Fact]
    public void ACompletedCaptureHasNoTruncationDiagnostic()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "complete.d3bcap", (CaptureRecordKind.FromClient, new byte[] { 1, 0x08 }));

        var capture = Assert.Single(CaptureDiagnostics.Read(path).Captures);

        Assert.False(capture.Summary.Truncated);
        Assert.DoesNotContain(capture.Diagnostics, diagnostic => diagnostic.Code == "capture.record_truncated");
    }

    [Fact]
    public void ARecordCutShortIsReportedAsAWarningAtItsRecordPosition()
    {
        using var directory = new CaptureTestDirectory();
        var complete = CaptureBytes((CaptureRecordKind.FromClient, new byte[] { 1, 0x08, 0x01 }));
        var path = directory.Combine("cut-short.d3bcap");
        File.WriteAllBytes(path, complete[..^1]);

        var report = CaptureDiagnostics.Read(path);
        var capture = Assert.Single(report.Captures);
        var diagnostic = Assert.Single(capture.Diagnostics);

        Assert.Equal("warning", report.Status);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal("warning", capture.Status);
        Assert.True(capture.Summary.Truncated);
        Assert.Equal("capture.record_truncated", diagnostic.Code);
        Assert.Equal(CaptureDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(CaptureDiagnosticStage.Capture, diagnostic.Stage);
        Assert.Equal((long?)0, diagnostic.RecordIndex);
        Assert.Equal((long?)HeaderLength(), diagnostic.ByteOffset);
    }

    [Fact]
    public void AnUnsupportedFormatIsAnErrorAndDoesNotEscapeAsAnException()
    {
        using var directory = new CaptureTestDirectory();
        var bytes = CaptureBytes();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, sizeof(int)), 99);
        var path = directory.Combine("unsupported.d3bcap");
        File.WriteAllBytes(path, bytes);

        var report = CaptureDiagnostics.Read(path);
        var capture = Assert.Single(report.Captures);
        var diagnostic = Assert.Single(capture.Diagnostics);

        Assert.Equal("error", report.Status);
        Assert.Equal(1, report.ExitCode);
        Assert.Equal((int?)99, capture.Capture.FormatVersion);
        Assert.Equal("capture.format_unsupported", diagnostic.Code);
        Assert.Equal(CaptureDiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void AnUnknownRecordKindIsReportedAsAnError()
    {
        using var directory = new CaptureTestDirectory();
        var bytes = CaptureBytes((CaptureRecordKind.FromClient, new byte[] { 1, 0x08 }));
        bytes[HeaderLength()] = 99;
        var path = directory.Combine("unknown-kind.d3bcap");
        File.WriteAllBytes(path, bytes);

        var report = CaptureDiagnostics.Read(path);
        var capture = Assert.Single(report.Captures);
        var diagnostic = Assert.Single(capture.Diagnostics);

        Assert.Equal("error", report.Status);
        Assert.Equal(1, report.ExitCode);
        Assert.Equal("capture.record_invalid", diagnostic.Code);
        Assert.Equal((long?)0, diagnostic.RecordIndex);
        Assert.Equal((long?)HeaderLength(), diagnostic.ByteOffset);
    }

    [Fact]
    public void AnInvalidFrameLengthNamesItsDirectionAndByteOffset()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "invalid-frame.d3bcap",
            (CaptureRecordKind.FromClient, new byte[] { 0xff, 0xff, 0xff, 0xff, 0x10 }));

        var report = CaptureDiagnostics.Read(path);
        var capture = Assert.Single(report.Captures);
        var diagnostic = Assert.Single(capture.Diagnostics);

        Assert.Equal("error", report.Status);
        Assert.Equal(1, report.ExitCode);
        Assert.Equal("framing.length_invalid", diagnostic.Code);
        Assert.Equal(CaptureDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(CaptureDiagnosticStage.Framing, diagnostic.Stage);
        Assert.Equal("client", diagnostic.Direction);
        Assert.Equal((long?)0, diagnostic.RecordIndex);
        Assert.Equal((long?)0, diagnostic.FrameIndex);
        Assert.Equal((long?)0, diagnostic.ByteOffset);
    }

    [Fact]
    public void InjectedRecordsHaveTheirOwnCountsAndFrameSummary()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "injected.d3bcap",
            (CaptureRecordKind.FromClient, new byte[] { 1, 0x08 }),
            (CaptureRecordKind.Injected, new byte[] { 1, 0x10 }));

        var capture = Assert.Single(CaptureDiagnostics.Read(path).Captures);

        Assert.Equal(2L, capture.Summary.Records);
        Assert.Equal(1L, capture.Summary.RecordsByKind["injected"]);
        Assert.Equal(2L, capture.Summary.BytesByKind["injected"]);
        Assert.Equal(0L, capture.Summary.Directions.Server.Reads);
        Assert.Equal(1L, capture.Summary.Directions.Injected.Reads);
        Assert.Equal(1L, capture.Summary.Directions.Injected.Frames);
        Assert.Equal(2L, capture.Summary.Directions.Injected.Bytes);
    }

    [Fact]
    public void TlsAndIncompleteFramesHaveStableFramingDiagnostics()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "framing.d3bcap",
            (CaptureRecordKind.FromClient, new byte[] { 0x16, 0x03, 0x01 }),
            (CaptureRecordKind.FromServer, new byte[] { 2, 0x08 }));

        var capture = Assert.Single(CaptureDiagnostics.Read(path).Captures);

        Assert.Contains(capture.Diagnostics, diagnostic => diagnostic.Code == "framing.tls" && diagnostic.Direction == "client");
        var incomplete = Assert.Single(capture.Diagnostics, diagnostic => diagnostic.Code == "framing.frame_incomplete");
        Assert.Equal("server", incomplete.Direction);
        Assert.Equal((long?)0, incomplete.ByteOffset);
        Assert.Equal("warning", capture.Status);
    }

    [Fact]
    public void JsonHasStableKeysValuesAndOrderingWithoutPathsStacksOrPayloads()
    {
        using var directory = new CaptureTestDirectory();
        var payload = new byte[] { 6, (byte)'S', (byte)'E', (byte)'C', (byte)'R', (byte)'E', (byte)'T' };
        var z = WriteCapture(directory, "z.d3bcap",
            (CaptureRecordKind.FromClient, payload),
            (CaptureRecordKind.FromServer, new byte[] { 0x16, 0x03, 0x01 }));
        var a = WriteCapture(directory, "a.d3bcap");

        var first = CaptureDiagnostics.ToJson(CaptureDiagnostics.Read([z, a]));
        var repeated = CaptureDiagnostics.ToJson(CaptureDiagnostics.Read([a, z]));

        Assert.Equal(first, repeated);
        using var document = JsonDocument.Parse(first);
        var root = document.RootElement;

        Assert.Equal(["schemaVersion", "captures", "status", "exitCode"], PropertyNames(root));
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(["a.d3bcap", "z.d3bcap"], root.GetProperty("captures").EnumerateArray()
            .Select(capture => capture.GetProperty("capture").GetProperty("basename").GetString()).ToArray());
        Assert.Equal("capture.empty", root.GetProperty("captures")[0].GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Equal("message.unknown_type", root.GetProperty("captures")[1].GetProperty("diagnostics")[0].GetProperty("code").GetString());
        var captureElement = root.GetProperty("captures")[1];
        Assert.Equal(["capture", "build", "summary", "diagnostics", "status"], PropertyNames(captureElement));
        Assert.Equal(["basename", "sha256", "size", "formatVersion", "startedAt", "listener", "serverHost", "server"],
            PropertyNames(captureElement.GetProperty("capture")));
        Assert.Equal(["clientVersion", "schemaVersion", "schemaSha256", "mappingVersion", "mappingSha256", "mappingClientVersionCompatible"], PropertyNames(captureElement.GetProperty("build")));
        Assert.Equal(["records", "recordsByKind", "bytesByKind", "directions", "messages", "truncated"], PropertyNames(captureElement.GetProperty("summary")));
        Assert.Equal(["client", "server", "injected"], PropertyNames(captureElement.GetProperty("summary").GetProperty("directions")));
        Assert.Equal(["reads", "bytes", "frames", "closed"], PropertyNames(captureElement.GetProperty("summary").GetProperty("directions").GetProperty("client")));
        Assert.Equal(["total", "byMember", "bySide", "byTypeUrl", "byMemberAndTypeUrl", "known", "unknown", "mapped", "unmapped"], PropertyNames(captureElement.GetProperty("summary").GetProperty("messages")));
        var firstDiagnostic = root.GetProperty("captures")[0].GetProperty("diagnostics")[0];
        Assert.Equal(["code", "severity", "stage", "direction", "recordIndex", "frameIndex", "byteOffset", "message", "details"], PropertyNames(firstDiagnostic));
        Assert.Equal("info", firstDiagnostic.GetProperty("severity").GetString());
        Assert.Equal("capture", firstDiagnostic.GetProperty("stage").GetString());
        Assert.True(firstDiagnostic.GetProperty("direction").ValueKind is JsonValueKind.Null);
        Assert.True(firstDiagnostic.GetProperty("recordIndex").ValueKind is JsonValueKind.Null);
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("ok", captureElement.GetProperty("status").GetString());
        Assert.DoesNotContain(directory.Path, first, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET", first, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", first, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MessageSummaryCountsEnvelopeMembersTypeUrlsAndInjectedFrames()
    {
        using var directory = new CaptureTestDirectory();
        var content = new byte[] { 0x08, 0x01 };
        var path = WriteCapture(directory, "messages.d3bcap",
            (CaptureRecordKind.FromClient, Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", content))),
            (CaptureRecordKind.FromServer, Frame(Envelope(1, 2, TypeUrlPrefix + "d3b.game.Known", content))),
            (CaptureRecordKind.FromServer, Frame(Envelope(3, 1, TypeUrlPrefix + "d3b.game.Known", content))),
            (CaptureRecordKind.Injected, Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", content))));
        var schemaPath = WriteSchema(directory, ["Known"]);
        var mappingPath = WriteMapping(directory, schemaPath, ["d3b.game.Known"]);

        var report = CaptureDiagnostics.Read([path], schemaPath, mappingPath);
        var capture = Assert.Single(report.Captures);
        var messages = capture.Summary.Messages;

        Assert.Equal(4L, messages.Total);
        Assert.Equal(2L, messages.ByMember["request"]);
        Assert.Equal(1L, messages.ByMember["response"]);
        Assert.Equal(1L, messages.ByMember["event"]);
        Assert.Equal(1L, messages.BySide["injected"]);
        Assert.Equal(4L, messages.ByTypeUrl[TypeUrlPrefix + "d3b.game.Known"]);
        Assert.Equal(2L, Assert.Single(messages.ByMemberAndTypeUrl, message => message.Member == "request").Count);
        Assert.Equal(4L, messages.Known);
        Assert.Equal(0L, messages.Unknown);
        Assert.Equal(4L, messages.Mapped);
        Assert.Equal("1", capture.Build.SchemaVersion);
        Assert.Equal("15", capture.Build.MappingVersion);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(schemaPath))), capture.Build.SchemaSha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(mappingPath))), capture.Build.MappingSha256);
        Assert.True(capture.Build.MappingClientVersionCompatible);
        Assert.DoesNotContain(capture.Diagnostics, diagnostic => diagnostic.Code == "schema.not_provided");
        Assert.DoesNotContain(capture.Diagnostics, diagnostic => diagnostic.Code == "mapping.message_unknown");
    }

    [Fact]
    public void AFrameWithoutAnAnyTypeUrlIsInformationalAndKeepsItsProvenance()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "missing-type-url.d3bcap",
            (CaptureRecordKind.FromClient, Frame(LengthField(4, LengthField(2, [])))));

        var report = CaptureDiagnostics.Read(path);
        var capture = Assert.Single(report.Captures);
        var diagnostic = Assert.Single(capture.Diagnostics, item => item.Code == "message.unknown_type");

        Assert.Equal(CaptureDiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal(CaptureDiagnosticStage.Message, diagnostic.Stage);
        Assert.Equal("client", diagnostic.Direction);
        Assert.Equal(0L, diagnostic.RecordIndex);
        Assert.Equal(0L, diagnostic.FrameIndex);
        Assert.Equal(0L, diagnostic.ByteOffset);
        Assert.Equal("request", diagnostic.Details!["member"]);
        Assert.Equal("0", diagnostic.Details["messageIndex"]);
        Assert.Equal(["member", "messageIndex"], diagnostic.Details.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(1L, capture.Summary.Messages.ByTypeUrl["(none)"]);
        Assert.Equal("ok", capture.Status);
        Assert.Equal("ok", report.Status);
        Assert.Equal(0, report.ExitCode);
    }

    [Fact]
    public void StructuredMessagesKeepFrameRecordSideAndMergedMessageIndices()
    {
        using var directory = new CaptureTestDirectory();
        var first = Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x01]));
        var second = Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x02]));
        var server = Frame(Envelope(1, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x03]));
        var injected = Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x04]));
        var path = WriteCapture(directory, "indexed.d3bcap",
            (CaptureRecordKind.FromClient, [.. first, .. second]),
            (CaptureRecordKind.FromServer, server),
            (CaptureRecordKind.Injected, injected));
        List<(CaptureRecord Record, long Index)> records;

        using (var reader = CaptureReader.Open(path))
            records = reader.ReadAll().Select((record, index) => (record, (long)index)).ToList();

        var messages = GameMessages.Extract(records);
        var client = messages.Where(message => message.Side == "client").ToArray();
        var serverMessage = Assert.Single(messages, message => message.Side == "server");
        var injectedMessage = Assert.Single(messages, message => message.Side == "injected");

        Assert.Equal([0L, 1L], client.Select(message => message.FrameIndex).ToArray());
        Assert.Equal([0L, 0L], client.Select(message => message.RecordIndex).ToArray());
        Assert.Equal([0L, 1L, 2L, 3L], messages.Select(message => message.MessageIndex).Order().ToArray());
        Assert.Equal((long)1, serverMessage.RecordIndex);
        Assert.Equal((long)2, injectedMessage.RecordIndex);
        Assert.Equal((long)0, serverMessage.FrameIndex);
        Assert.Equal((long)0, injectedMessage.FrameIndex);
        Assert.Equal(TypeUrlPrefix + "d3b.game.Known", injectedMessage.TypeUrl);
        Assert.Equal(new byte[] { 0x08, 0x04 }, injectedMessage.Content);
        Assert.Equal(1L, Assert.Single(CaptureDiagnostics.Read([path]).Captures).Summary.Messages.BySide["injected"]);
    }

    [Fact]
    public void MessageTextOutputRetainsItsExistingFormat()
    {
        var data = Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x01]));
        var records = new List<CaptureRecord> { new(CaptureRecordKind.FromClient, TimeSpan.Zero, data) };
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

        GameMessages.Write(output, records, null, null, false);

        Assert.Equal("    00:00:00.000 client request d3b.game.Known, 2 bytes\n  1 frames, 1 messages: d3b.game.Known 1\n", output.ToString());
    }

    [Fact]
    public void UnknownSchemaMessageIsAWarningWithFrameAndRecordProvenance()
    {
        using var directory = new CaptureTestDirectory();
        var url = TypeUrlPrefix + "d3b.game.Missing";
        var path = WriteCapture(directory, "unknown.d3bcap",
            (CaptureRecordKind.FromClient, Frame(Envelope(4, 2, url, [0x08, 0x01]))));
        var schemaPath = WriteSchema(directory, ["Known"]);

        var report = CaptureDiagnostics.Read([path], schemaPath);
        var capture = Assert.Single(report.Captures);
        var diagnostic = Assert.Single(capture.Diagnostics, item => item.Code == "schema.message_unknown");

        Assert.Equal("warning", report.Status);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal(CaptureDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(CaptureDiagnosticStage.Schema, diagnostic.Stage);
        Assert.Equal("client", diagnostic.Direction);
        Assert.Equal((long?)0, diagnostic.RecordIndex);
        Assert.Equal((long?)0, diagnostic.FrameIndex);
        Assert.Equal(url, diagnostic.Details!["typeUrl"]);
        Assert.Equal(1L, capture.Summary.Messages.Unknown);
    }

    [Fact]
    public void SchemaPayloadMismatchIsAnErrorWithoutPayloadValues()
    {
        using var directory = new CaptureTestDirectory();
        var secret = System.Text.Encoding.ASCII.GetBytes("SECRET");
        var content = LengthField(7, secret);
        var url = TypeUrlPrefix + "d3b.game.Known";
        var path = WriteCapture(directory, "mismatch.d3bcap",
            (CaptureRecordKind.FromClient, Frame(Envelope(4, 2, url, content))));
        var schemaPath = WriteSchema(directory, ["Known"]);

        var report = CaptureDiagnostics.Read([path], schemaPath);
        var capture = Assert.Single(report.Captures);
        var diagnostic = Assert.Single(capture.Diagnostics, item => item.Code == "schema.payload_mismatch");
        var json = CaptureDiagnostics.ToJson(report);

        Assert.Equal("error", report.Status);
        Assert.Equal(1, report.ExitCode);
        Assert.Equal(CaptureDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(url, diagnostic.Details!["typeUrl"]);
        Assert.Equal((long?)0, diagnostic.FrameIndex);
        Assert.Equal((long?)0, diagnostic.RecordIndex);
        Assert.Equal("0", diagnostic.Details!["messageIndex"]);
        Assert.DoesNotContain("SECRET", json, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(content), json, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSchemaAndMappingAreInformationalOncePerCapture()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "unconfigured.d3bcap",
            (CaptureRecordKind.FromClient, Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x01]))),
            (CaptureRecordKind.FromServer, Frame(Envelope(1, 2, TypeUrlPrefix + "d3b.game.Other", [0x08, 0x02]))));

        var report = CaptureDiagnostics.Read([path]);
        var capture = Assert.Single(report.Captures);

        Assert.Equal("ok", report.Status);
        Assert.Equal(0, report.ExitCode);
        Assert.Single(capture.Diagnostics, diagnostic => diagnostic.Code == "schema.not_provided");
        Assert.Single(capture.Diagnostics, diagnostic => diagnostic.Code == "mapping.not_provided");
        Assert.Equal(2L, capture.Summary.Messages.Total);
    }

    [Fact]
    public void InvalidMappingFileIsAnErrorWithoutFileDetails()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "bad-mapping.d3bcap",
            (CaptureRecordKind.FromClient, Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x01]))));
        var mappingPath = directory.Combine("invalid-mapping.binpb");
        File.WriteAllBytes(mappingPath, [0xff]);

        var report = CaptureDiagnostics.Read([path], mappingPath: mappingPath);
        var capture = Assert.Single(report.Captures);

        Assert.Equal("error", report.Status);
        Assert.Equal(1, report.ExitCode);
        var diagnostic = Assert.Single(capture.Diagnostics, item => item.Code == "mapping.file_invalid");
        Assert.Equal(CaptureDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.DoesNotContain(directory.Path, CaptureDiagnostics.ToJson(report), StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidSchemaFileIsAnErrorWithoutFileDetails()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "bad-schema.d3bcap",
            (CaptureRecordKind.FromClient, Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x01]))));
        var schemaPath = directory.Combine("invalid-schema.binpb");
        File.WriteAllBytes(schemaPath, [0xff]);

        var report = CaptureDiagnostics.Read([path], schemaPath);
        var capture = Assert.Single(report.Captures);

        Assert.Equal("error", report.Status);
        Assert.Equal(1, report.ExitCode);
        var diagnostic = Assert.Single(capture.Diagnostics, item => item.Code == "schema.file_invalid");
        Assert.Equal(CaptureDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.DoesNotContain(directory.Path, CaptureDiagnostics.ToJson(report), StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedMappingFormatIsAnError()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "unsupported-mapping.d3bcap",
            (CaptureRecordKind.FromClient, Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x01]))));
        var mappingPath = WriteMapping(directory, MappingFile.FormatVersion + 1, "3.7.12.12", [], []);

        var report = CaptureDiagnostics.Read([path], mappingPath: mappingPath);
        var capture = Assert.Single(report.Captures);
        var diagnostic = Assert.Single(capture.Diagnostics, item => item.Code == "mapping.format_unsupported");

        Assert.Equal("error", report.Status);
        Assert.Equal(1, report.ExitCode);
        Assert.Equal(CaptureDiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void MappingClientVersionMismatchIsAnError()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "build-mismatch.d3bcap",
            (CaptureRecordKind.FromClient, Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x01]))));
        var mappingPath = WriteMapping(directory, MappingFile.FormatVersion, "different-build", [], []);

        var report = CaptureDiagnostics.Read([path], mappingPath: mappingPath);
        var capture = Assert.Single(report.Captures);
        var diagnostic = Assert.Single(capture.Diagnostics, item => item.Code == "mapping.build_mismatch");

        Assert.Equal("error", report.Status);
        Assert.Equal(1, report.ExitCode);
        Assert.False(capture.Build.MappingClientVersionCompatible);
        Assert.Equal(CaptureDiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void MappingSchemaDigestMismatchIsAnError()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "schema-mismatch.d3bcap",
            (CaptureRecordKind.FromClient, Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x01]))));
        var schemaPath = WriteSchema(directory, ["Known"]);
        var mappingPath = WriteMapping(directory, MappingFile.FormatVersion, "3.7.12.12", new byte[32], ["d3b.game.Known"]);

        var report = CaptureDiagnostics.Read([path], schemaPath, mappingPath);
        var capture = Assert.Single(report.Captures);
        var diagnostic = Assert.Single(capture.Diagnostics, item => item.Code == "mapping.schema_mismatch");

        Assert.Equal("error", report.Status);
        Assert.Equal(1, report.ExitCode);
        Assert.Equal(CaptureDiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void MissingMappingEntryIsInformationalAndDoesNotChangeStatus()
    {
        using var directory = new CaptureTestDirectory();
        var path = WriteCapture(directory, "unmapped.d3bcap",
            (CaptureRecordKind.FromClient, Frame(Envelope(4, 2, TypeUrlPrefix + "d3b.game.Known", [0x08, 0x01]))));
        var schemaPath = WriteSchema(directory, ["Known"]);
        var mappingPath = WriteMapping(directory, schemaPath, []);

        var report = CaptureDiagnostics.Read([path], schemaPath, mappingPath);
        var capture = Assert.Single(report.Captures);
        var diagnostic = Assert.Single(capture.Diagnostics, item => item.Code == "mapping.message_unknown");

        Assert.Equal("ok", report.Status);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal(CaptureDiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal(1L, capture.Summary.Messages.Known);
        Assert.Equal(0L, capture.Summary.Messages.Mapped);
    }

    private static string[] PropertyNames(JsonElement element)
    {
        return element.EnumerateObject().Select(property => property.Name).ToArray();
    }

    private static string WriteCapture(CaptureTestDirectory directory, string name, params (CaptureRecordKind Kind, byte[] Data)[] records)
    {
        var path = directory.Combine(name);
        File.WriteAllBytes(path, CaptureBytes(records));

        return path;
    }

    private static byte[] CaptureBytes(params (CaptureRecordKind Kind, byte[] Data)[] records)
    {
        using var stream = new MemoryStream();

        using (var writer = new CaptureWriter(stream, _header, TimeProvider.System))
        {
            foreach (var (kind, data) in records)
            {
                if (kind is CaptureRecordKind.ClientClosed or CaptureRecordKind.ServerClosed)
                    writer.WriteClosed(kind);
                else
                    writer.Write(kind, new ReadOnlySequence<byte>(data));
            }
        }

        return stream.ToArray();
    }

    private static string WriteSchema(CaptureTestDirectory directory, string[] messageNames)
    {
        var file = new FileDescriptorProto { Name = "messages.proto", Package = "d3b.game", Syntax = "proto3" };

        foreach (var name in messageNames)
        {
            file.MessageType.Add(new DescriptorProto
            {
                Name = name,
                Field =
                {
                    new FieldDescriptorProto
                    {
                        Name = "value",
                        Number = 1,
                        Label = FieldDescriptorProto.Types.Label.Optional,
                        Type = FieldDescriptorProto.Types.Type.Int32,
                    },
                },
            });
        }

        var path = directory.Combine("schema.binpb");
        File.WriteAllBytes(path, new FileDescriptorSet { File = { file } }.ToByteArray());

        return path;
    }

    private static string WriteMapping(CaptureTestDirectory directory, string schemaPath, string[] obfuscatedNames)
    {
        return WriteMapping(directory, MappingFile.FormatVersion, "3.7.12.12", SHA256.HashData(File.ReadAllBytes(schemaPath)), obfuscatedNames);
    }

    private static string WriteMapping(CaptureTestDirectory directory, int formatVersion, string clientVersion, byte[] schemaSha256, string[] obfuscatedNames)
    {
        var mapping = new ProtocolMapping
        {
            FormatVersion = formatVersion,
            ClientVersion = clientVersion,
            SchemaSha256 = ByteString.CopyFrom(schemaSha256),
        };

        foreach (var name in obfuscatedNames)
            mapping.Messages.Add(new MessagePair { Obfuscated = name, Readable = "d3b.game.Readable" + name[(name.LastIndexOf('.') + 1)..] });

        var path = directory.Combine("mapping.binpb");
        MappingFile.Write(path, mapping);

        return path;
    }

    private static byte[] Envelope(int memberNumber, int anyNumber, string typeUrl, byte[] content)
    {
        var any = new Any { TypeUrl = typeUrl, Value = ByteString.CopyFrom(content) };

        return LengthField(memberNumber, LengthField(anyNumber, any.ToByteArray()));
    }

    private static byte[] LengthField(int fieldNumber, byte[] content)
    {
        using var stream = new MemoryStream();

        using (var coded = new CodedOutputStream(stream, true))
        {
            coded.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
            coded.WriteBytes(ByteString.CopyFrom(content));
        }

        return stream.ToArray();
    }

    private static byte[] Frame(byte[] payload)
    {
        var writer = new System.Buffers.ArrayBufferWriter<byte>();
        FrameWriter.Write(writer, payload);

        return writer.WrittenSpan.ToArray();
    }

    private static int HeaderLength()
    {
        return CaptureBytes().Length;
    }
}
