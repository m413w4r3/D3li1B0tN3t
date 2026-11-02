using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using D3B.Network.Captures;
using D3B.Scripts.Capture;

namespace D3B.Scripts.Tests.Captures;

public sealed class CaptureDiagnosticsTests
{
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
        Assert.Equal("framing.tls", root.GetProperty("captures")[1].GetProperty("diagnostics")[0].GetProperty("code").GetString());
        var captureElement = root.GetProperty("captures")[1];
        Assert.Equal(["capture", "build", "summary", "diagnostics", "status"], PropertyNames(captureElement));
        Assert.Equal(["basename", "sha256", "size", "formatVersion", "startedAt", "listener", "serverHost", "server"],
            PropertyNames(captureElement.GetProperty("capture")));
        Assert.Equal(["clientVersion", "schemaVersion", "mappingVersion"], PropertyNames(captureElement.GetProperty("build")));
        Assert.Equal(["records", "recordsByKind", "bytesByKind", "directions", "truncated"], PropertyNames(captureElement.GetProperty("summary")));
        Assert.Equal(["client", "server", "injected"], PropertyNames(captureElement.GetProperty("summary").GetProperty("directions")));
        Assert.Equal(["reads", "bytes", "frames", "closed"], PropertyNames(captureElement.GetProperty("summary").GetProperty("directions").GetProperty("client")));
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

    private static int HeaderLength()
    {
        return CaptureBytes().Length;
    }
}
