using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Ipc;
using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Reporting;
using D3B.Diagnostic.Replay;
using D3B.Diagnostic.Tests.Replay;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Diagnostic.Tests.Observation;

public sealed class DiagnosticSessionReportTests
{
    [Fact]
    public async Task EveryAnalysisOfASessionReachesTheReport()
    {
        DiagnosticSchema schema = SchemaWithSecondType();
        var sidecar = SuccessfulSidecar(3);
        sidecar.Analyze = frame => Task.FromResult(new DiagnosticAnalysis
        {
            Index = frame.Index,
            Direction = frame.Direction,
            CarriedType = frame.CarriedType,
            Message = frame.CarriedType,
            Status = DiagnosticStatus.Valid,
        });
        var writer = new FakeDiagnosticReportWriter();
        var session = CreateSession(sidecar, writer, schema);

        Assert.True(session.TryEnqueue(Frame(0, "client", "lok")));
        Assert.True(session.TryEnqueue(Frame(1, "server", "other")));
        Assert.True(session.TryEnqueue(Frame(2, "d3b", "lok")));
        await session.DisposeAsync();

        DiagnosticReport report = Assert.Single(writer.Reports);
        Assert.Equal(2, Assert.Single(report.Types, type => type.Token == "lok").Occurrences);
        Assert.Equal(1, Assert.Single(report.Types, type => type.Token == "other").Occurrences);
        Assert.Contains(Assert.Single(report.Types, type => type.Token == "lok").Directions, count => count.Direction == "client");
        Assert.Contains(Assert.Single(report.Types, type => type.Token == "lok").Directions, count => count.Direction == "d3b");
        Assert.Contains(Assert.Single(report.Types, type => type.Token == "other").Directions, count => count.Direction == "server");
    }

    [Fact]
    public async Task NormalEndWritesOneCompleteReport()
    {
        var hello = new DiagnosticEngineHello
        {
            ProtocolVersion = 1,
            Engine = "d3b-diagnostic",
            EngineVersion = "0.1.0",
            SnsCommit = "abc123",
        };
        var sidecar = SuccessfulSidecar(1);
        sidecar.Hello = hello;
        var writer = new FakeDiagnosticReportWriter();
        var session = CreateSession(sidecar, writer);
        Assert.True(session.TryEnqueue(Frame(0)));

        await session.DisposeAsync();

        DiagnosticReport report = Assert.Single(writer.Reports);
        Assert.Equal(DiagnosticReportSessionState.Completed, report.Session.State);
        Assert.Equal("complete", report.Coverage);
        Assert.Equal("d3b-diagnostic", report.Engine.Engine);
        Assert.Equal("0.1.0", report.Engine.EngineVersion);
        Assert.True(report.Session.IncludeReadable);
        Assert.Equal(8, report.Session.QueueCapacity);
        Assert.Equal(1_048_576, report.Session.QueueByteCapacity);
        Assert.Equal(hello, session.Snapshot.EngineHello);
    }

    [Fact]
    public async Task HandshakeFailureWritesEmptyEngineIdentityAndFailureCode()
    {
        var sidecar = new FakeDiagnosticSidecar
        {
            Start = _ => throw new DiagnosticSidecarException(DiagnosticErrorCode.ContractInvalid),
        };
        var writer = new FakeDiagnosticReportWriter();
        var session = CreateSession(sidecar, writer);

        await session.DisposeAsync();

        DiagnosticReport report = Assert.Single(writer.Reports);
        Assert.Equal(DiagnosticReportSessionState.Failed, report.Session.State);
        Assert.Equal(DiagnosticErrorCode.ContractInvalid, report.Session.ErrorCode);
        Assert.Equal(string.Empty, report.Engine.Engine);
        Assert.Equal(string.Empty, report.Engine.EngineVersion);
        Assert.Equal(string.Empty, report.Engine.SnsCommit);
        Assert.Null(session.Snapshot.EngineHello);
    }

    [Fact]
    public async Task SidecarCrashWritesAFailedPartialReport()
    {
        var sidecar = new FakeDiagnosticSidecar
        {
            Analyze = _ => throw new EndOfStreamException("sidecar stopped"),
        };
        var writer = new FakeDiagnosticReportWriter();
        var session = CreateSession(sidecar, writer);
        Assert.True(session.TryEnqueue(Frame(0)));

        await session.DisposeAsync();

        DiagnosticReport report = Assert.Single(writer.Reports);
        Assert.Equal(DiagnosticReportSessionState.Failed, report.Session.State);
        Assert.Equal(DiagnosticErrorCode.IoError, report.Session.ErrorCode);
        Assert.Equal(1, report.Session.DroppedFrames);
        Assert.Equal("partial", report.Coverage);
    }

    [Fact]
    public async Task ResponseTimeoutWritesAPartialReportAndDisposeStaysBounded()
    {
        var sidecar = SuccessfulSidecar(1);
        sidecar.AnalyzeWithToken = async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new DiagnosticAnalysis();
        };
        var writer = new FakeDiagnosticReportWriter();
        var session = CreateSession(
            sidecar,
            writer,
            options: Options(2) with { ResponseTimeout = TimeSpan.FromMilliseconds(80) });
        Assert.True(session.TryEnqueue(Frame(0)));
        var timer = System.Diagnostics.Stopwatch.StartNew();

        await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        timer.Stop();

        DiagnosticReport report = Assert.Single(writer.Reports);
        Assert.Equal(DiagnosticReportSessionState.Failed, report.Session.State);
        Assert.Equal("partial", report.Coverage);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(6));
    }

    [Fact]
    public async Task QueueOverflowAndByteOverflowMarkCoveragePartial()
    {
        var queueReport = await CreateOverflowReport(queueCapacity: 1, queueByteCapacity: 1_048_576);
        var byteReport = await CreateOverflowReport(queueCapacity: 10, queueByteCapacity: 4);

        Assert.Equal(1, queueReport.Session.DroppedFrames);
        Assert.Equal(1, byteReport.Session.DroppedFrames);
        Assert.Equal("partial", queueReport.Coverage);
        Assert.Equal("partial", byteReport.Coverage);
    }

    [Fact]
    public async Task PartialSummaryMarksCoveragePartial()
    {
        var sidecar = SuccessfulSidecar(1);
        sidecar.Complete = () => Task.FromResult(new DiagnosticSummary { Frames = 1, Coverage = "partial" });
        var writer = new FakeDiagnosticReportWriter();
        var session = CreateSession(sidecar, writer);
        Assert.True(session.TryEnqueue(Frame(0)));

        await session.DisposeAsync();

        DiagnosticReport report = Assert.Single(writer.Reports);
        Assert.Equal("partial", report.Coverage);
        Assert.Contains("session-incomplete", report.Truncation.Reasons);
    }

    [Fact]
    public async Task SummaryFrameMismatchMarksCoveragePartial()
    {
        var sidecar = SuccessfulSidecar(0);
        sidecar.Complete = () => Task.FromResult(new DiagnosticSummary { Frames = 9, Coverage = "complete" });
        var writer = new FakeDiagnosticReportWriter();
        var session = CreateSession(sidecar, writer);

        await session.DisposeAsync();

        DiagnosticReport report = Assert.Single(writer.Reports);
        Assert.Equal("partial", report.Coverage);
        Assert.Contains("summary-mismatch", report.Truncation.Reasons);
    }

    [Fact]
    public async Task AggregationFailureMarksReportPartialAndContinuesThePump()
    {
        var sidecar = SuccessfulSidecar(2);
        sidecar.Analyze = frame => Task.FromResult(new DiagnosticAnalysis
        {
            Index = frame.Index,
            Direction = frame.Direction,
            CarriedType = "lok",
            Message = "lok",
            Status = DiagnosticStatus.Valid,
            Fields = frame.Index == 0 ? null! : Array.Empty<DiagnosticFieldAnalysis>(),
        });
        var writer = new FakeDiagnosticReportWriter();
        var session = CreateSession(sidecar, writer);
        Assert.True(session.TryEnqueue(Frame(0)));
        Assert.True(session.TryEnqueue(Frame(1)));

        await session.DisposeAsync();

        DiagnosticReport report = Assert.Single(writer.Reports);
        Assert.Equal(2, session.Snapshot.AnalyzedFrames);
        Assert.Equal("partial", report.Coverage);
        Assert.Contains("aggregation-failed", report.Truncation.Reasons);
    }

    [Fact]
    public async Task NoSensitiveDataAppearsInTheReportFile()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            var hello = new DiagnosticEngineHello
            {
                ProtocolVersion = 1,
                Engine = "\u0001ENGINE-DETAIL-CANARY",
                EngineVersion = "0.1.0",
                SnsCommit = "abc123",
            };
            var sidecar = SuccessfulSidecar(1);
            sidecar.Hello = hello;
            sidecar.Analyze = frame => Task.FromResult(new DiagnosticAnalysis
            {
                Index = frame.Index,
                Direction = frame.Direction,
                CarriedType = "lok",
                Message = "lok",
                Status = DiagnosticStatus.Valid,
            });
            var reportOptions = ReportOptions();
            var logger = new FakeLogCollector();
            var writer = new AtomicDiagnosticReportWriter(reportOptions, directory, TimeProvider.System);
            var session = CreateSession(
                sidecar,
                writer,
                reportOptions: reportOptions,
                logger: new FakeLogger<DiagnosticSession>(logger));
            byte[] payload = "PAYLOAD-CANARY-TICKET-CANARY-ACCOUNT-CANARY"u8.ToArray();
            Assert.True(session.TryEnqueue(Frame(0, "server", "lok", payload)));

            await session.DisposeAsync();

            string path = Assert.Single(Directory.GetFiles(directory, "*.json"));
            string contents = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.Contains("lok", DiagnosticReportJson.LoadFile(path, reportOptions).Report.Types.Select(type => type.Token));
            Assert.DoesNotContain("PAYLOAD-CANARY", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("TICKET-CANARY", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("ACCOUNT-CANARY", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("ENGINE-DETAIL-CANARY", contents, StringComparison.Ordinal);
            string logMessages = string.Join('\n', logger.GetSnapshot().Select(record => record.Message));
            Assert.DoesNotContain("CANARY", logMessages, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ReportFileAndDirectoryHavePrivateUnixModes()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string directory = CreateTemporaryDirectory();
        try
        {
            var writer = new AtomicDiagnosticReportWriter(ReportOptions(), directory, TimeProvider.System);
            await writer.WriteAsync(CreateReport(), TestContext.Current.CancellationToken);

            string path = Assert.Single(Directory.GetFiles(directory, "*.json"));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(directory));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Single(Directory.GetFiles(directory));
            Assert.StartsWith("diagnostic-report-", Path.GetFileName(path), StringComparison.Ordinal);
            _ = DiagnosticReportJson.LoadFile(path, ReportOptions());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ReportWriterRefusesASymlinkDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string parent = CreateTemporaryDirectory();
        string target = Path.Combine(parent, "target");
        string link = Path.Combine(parent, "link");
        Directory.CreateDirectory(target);
        try
        {
            Directory.CreateSymbolicLink(link, target);
            var writer = new AtomicDiagnosticReportWriter(ReportOptions(), link, TimeProvider.System);

            await Assert.ThrowsAsync<IOException>(async () => await writer.WriteAsync(CreateReport(), TestContext.Current.CancellationToken));

            Assert.Empty(Directory.GetFiles(target));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task DisabledReportWritesNothingAndKeepsDiagnosticsWorking()
    {
        var sidecar = SuccessfulSidecar(1);
        var writer = new FakeDiagnosticReportWriter();
        var session = CreateSession(sidecar, writer, reportOptions: new DiagnosticReportOptions { Enabled = false });
        Assert.True(session.TryEnqueue(Frame(0)));

        await session.DisposeAsync();

        Assert.Empty(writer.Reports);
        Assert.Equal(1, session.Snapshot.AnalyzedFrames);
        Assert.Equal("complete", session.Snapshot.Coverage);
    }

    [Fact]
    public async Task ReportWriterFailureDoesNotEscapeOrDisableDiagnosticAnalysis()
    {
        var sidecar = SuccessfulSidecar(1);
        var logs = new FakeLogCollector();
        var writer = new FakeDiagnosticReportWriter { Failure = new IOException("CANARY-PRIVATE-PATH") };
        var session = CreateSession(sidecar, writer, logger: new FakeLogger<DiagnosticSession>(logs));
        Assert.True(session.TryEnqueue(Frame(0)));

        Exception? exception = await Record.ExceptionAsync(async () => await session.DisposeAsync());

        Assert.Null(exception);
        Assert.Single(writer.Reports);
        Assert.Equal(1, session.Snapshot.AnalyzedFrames);
        Assert.Equal("complete", session.Snapshot.Coverage);
        string logMessages = string.Join('\n', logs.GetSnapshot().Select(record => record.Message));
        Assert.DoesNotContain("CANARY-PRIVATE-PATH", logMessages, StringComparison.Ordinal);
    }

    private static async Task<DiagnosticReport> CreateOverflowReport(int queueCapacity, long queueByteCapacity)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sidecar = SuccessfulSidecar(2);
        sidecar.Analyze = async frame =>
        {
            entered.TrySetResult();
            await release.Task;
            return new DiagnosticAnalysis
            {
                Index = frame.Index,
                Direction = frame.Direction,
                CarriedType = "lok",
                Message = "lok",
                Status = DiagnosticStatus.Valid,
            };
        };
        var writer = new FakeDiagnosticReportWriter();
        var session = CreateSession(
            sidecar,
            writer,
            options: Options(queueCapacity, queueByteCapacity));

        Assert.True(session.TryEnqueue(Frame(0, payload: [1, 2])));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(session.TryEnqueue(Frame(1, payload: [3, 4, 5])));
        Assert.False(session.TryEnqueue(Frame(2, payload: [6, 7, 8])));
        release.TrySetResult();
        await session.DisposeAsync();

        return Assert.Single(writer.Reports);
    }

    private static DiagnosticSession CreateSession(
        FakeDiagnosticSidecar sidecar,
        IDiagnosticReportWriter writer,
        DiagnosticSchema? schema = null,
        SidecarOptions? options = null,
        DiagnosticReportOptions? reportOptions = null,
        FakeLogger<DiagnosticSession>? logger = null)
    {
        return new DiagnosticSession(
            options ?? Options(8),
            schema ?? SchemaWithSecondType(),
            new FakeDiagnosticSidecarFactory(sidecar),
            logger ?? new FakeLogger<DiagnosticSession>(new FakeLogCollector()),
            reportOptions ?? ReportOptions(),
            writer,
            TimeProvider.System,
            includeReadable: true);
    }

    private static FakeDiagnosticSidecar SuccessfulSidecar(long frames)
    {
        return new FakeDiagnosticSidecar
        {
            Analyze = frame => Task.FromResult(new DiagnosticAnalysis
            {
                Index = frame.Index,
                Direction = frame.Direction,
                CarriedType = frame.CarriedType,
                Message = frame.CarriedType,
                Status = DiagnosticStatus.Valid,
            }),
            Complete = () => Task.FromResult(new DiagnosticSummary { Frames = frames, Coverage = "complete" }),
        };
    }

    private static DiagnosticSchema SchemaWithSecondType()
    {
        DiagnosticSchema schema = ReplayTestData.CreateSchema();
        return schema with
        {
            Messages =
            [
                .. schema.Messages,
                new DiagnosticMessage { Name = "other", Fields = Array.Empty<DiagnosticField>() },
            ],
        };
    }

    private static SidecarOptions Options(int queueCapacity, long queueByteCapacity = 1_048_576)
    {
        return new SidecarOptions
        {
            EnginePath = "unused",
            SocketDirectory = "unused",
            QueueCapacity = queueCapacity,
            QueueByteCapacity = queueByteCapacity,
            HandshakeTimeout = TimeSpan.FromSeconds(2),
            ResponseTimeout = TimeSpan.FromSeconds(2),
        };
    }

    private static DiagnosticReportOptions ReportOptions()
    {
        return new DiagnosticReportOptions { Enabled = true };
    }

    private static DiagnosticFrame Frame(long index, string direction = "server", string type = "lok", byte[]? payload = null)
    {
        return new DiagnosticFrame(index, direction, "type.ankama.com/" + type, type, payload ?? [0x08, 0x01]);
    }

    private static DiagnosticReport CreateReport()
    {
        var aggregator = new DiagnosticReportAggregator(
            SchemaWithSecondType(),
            ReportOptions(),
            new DiagnosticReportBuildContext
            {
                ClientVersion = "3.7.13.17",
                SchemaSha256 = ReplayTestData.SchemaHash,
                Engine = new DiagnosticEngineHello { ProtocolVersion = 1, Engine = "d3b-diagnostic", EngineVersion = "0.1.0", SnsCommit = "abc123" },
                StartedAtUtc = DateTimeOffset.UnixEpoch,
            });
        return aggregator.Build(new DiagnosticReportBuildOutcome
        {
            State = DiagnosticReportSessionState.Completed,
            Coverage = "complete",
            CompletedAtUtc = DateTimeOffset.UnixEpoch.AddMinutes(1),
        });
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "d3b-diagnostic-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
