using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Ipc;
using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Replay;
using D3B.Diagnostic.Tests.Replay;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Diagnostic.Tests.Observation;

public sealed class DiagnosticSessionTests
{
    [Fact]
    public async Task QueueOverflowDropsAndCountsTheNewestFrame()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sidecar = new FakeDiagnosticSidecar
        {
            Analyze = async frame =>
            {
                entered.TrySetResult();
                await release.Task;
                return new DiagnosticAnalysis { Index = frame.Index };
            },
        };
        var session = CreateSession(sidecar, queueCapacity: 1);

        Assert.True(session.TryEnqueue(Frame(1)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(session.TryEnqueue(Frame(2)));
        Assert.False(session.TryEnqueue(Frame(3)));
        Assert.Equal(1, session.Snapshot.DroppedFrames);

        release.TrySetResult();
        await session.DisposeAsync();

        Assert.Equal(2, session.Snapshot.AnalyzedFrames);
        Assert.Equal("partial", session.Snapshot.Coverage);
        Assert.Equal(1, session.Snapshot.Summary?.Dropped);
    }

    [Fact]
    public async Task QueueByteBudgetDropsFramesThatWouldExceedCapacity()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sidecar = new FakeDiagnosticSidecar
        {
            Analyze = async frame =>
            {
                entered.TrySetResult();
                await release.Task;
                return new DiagnosticAnalysis { Index = frame.Index };
            },
        };
        var session = new DiagnosticSession(
            Options(4, queueByteCapacity: 4),
            ReplayTestData.CreateSchema(),
            new FakeDiagnosticSidecarFactory(sidecar),
            new FakeLogger<DiagnosticSession>(new FakeLogCollector()));

        Assert.True(session.TryEnqueue(Frame(1)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(session.TryEnqueue(Frame(2, [1, 2, 3])));
        Assert.False(session.TryEnqueue(Frame(3, [4, 5, 6])));
        Assert.Equal(1, session.Snapshot.DroppedFrames);

        release.TrySetResult();
        await session.DisposeAsync();
        Assert.Equal(2, session.Snapshot.AnalyzedFrames);
    }

    [Fact]
    public async Task ABlockedSidecarDoesNotBlockTryEnqueue()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sidecar = new FakeDiagnosticSidecar
        {
            Analyze = async _ =>
            {
                entered.TrySetResult();
                await release.Task;
                return new DiagnosticAnalysis();
            },
        };
        var session = CreateSession(sidecar, queueCapacity: 4);
        Assert.True(session.TryEnqueue(Frame(1)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        var timer = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(session.TryEnqueue(Frame(2)));
        timer.Stop();

        release.TrySetResult();
        await session.DisposeAsync();
        Assert.True(timer.Elapsed < TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public async Task DisposeReturnsWithinTheResponseBoundWhenTheSidecarIgnoresCancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sidecar = new FakeDiagnosticSidecar
        {
            Analyze = async _ =>
            {
                entered.TrySetResult();
                await release.Task;
                return new DiagnosticAnalysis();
            },
        };
        var session = new DiagnosticSession(
            Options(2) with { ResponseTimeout = TimeSpan.FromMilliseconds(50) },
            ReplayTestData.CreateSchema(),
            new FakeDiagnosticSidecarFactory(sidecar),
            new FakeLogger<DiagnosticSession>(new FakeLogCollector()));
        Assert.True(session.TryEnqueue(Frame(1)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Task dispose = session.DisposeAsync().AsTask();
        await dispose.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        release.TrySetResult();
        await session.DisposeAsync();

        Assert.Equal("partial", session.Snapshot.Coverage);
    }

    [Fact]
    public async Task ASidecarFailureMarksCoveragePartialAndLogsNoFrameContent()
    {
        var log = new FakeLogCollector();
        var logger = new FakeLogger<DiagnosticSession>(log);
        var sidecar = new FakeDiagnosticSidecar
        {
            Analyze = _ => throw new DiagnosticSidecarException(DiagnosticErrorCode.ContractInvalid),
        };
        var session = CreateSession(sidecar, logger);

        Assert.True(session.TryEnqueue(new DiagnosticFrame(0, "server", "type.ankama.com/SensitiveType", "SensitiveType", "secret"u8.ToArray())));
        await session.DisposeAsync();

        Assert.Equal("partial", session.Snapshot.Coverage);
        Assert.Equal(DiagnosticErrorCode.ContractInvalid, session.Snapshot.ErrorCode);
        var messages = string.Join('\n', log.GetSnapshot().Select(record => record.Message));
        Assert.DoesNotContain("SensitiveType", messages, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", messages, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedStartupCountsQueuedFramesAsPartialObservation()
    {
        var options = Options(2);
        var session = new DiagnosticSession(
            options,
            ReplayTestData.CreateSchema(),
            new FakeDiagnosticSidecarFactory(DiagnosticErrorCode.SocketFailed),
            new FakeLogger<DiagnosticSession>(new FakeLogCollector()));
        Assert.True(session.TryEnqueue(Frame(1)));

        await session.DisposeAsync();

        Assert.Equal("partial", session.Snapshot.Coverage);
        Assert.Equal(DiagnosticErrorCode.SocketFailed, session.Snapshot.ErrorCode);
        Assert.Equal(1, session.Snapshot.DroppedFrames);
    }

    private static DiagnosticSession CreateSession(FakeDiagnosticSidecar sidecar, int queueCapacity = 2)
    {
        return CreateSession(sidecar, new FakeLogger<DiagnosticSession>(new FakeLogCollector()), queueCapacity);
    }

    private static DiagnosticSession CreateSession(FakeDiagnosticSidecar sidecar, FakeLogger<DiagnosticSession> logger, int queueCapacity = 2)
    {
        return new DiagnosticSession(
            Options(queueCapacity),
            ReplayTestData.CreateSchema(),
            new FakeDiagnosticSidecarFactory(sidecar),
            logger);
    }

    private static SidecarOptions Options(int queueCapacity, long queueByteCapacity = 16L * 1024 * 1024)
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

    private static DiagnosticFrame Frame(long index, byte[]? payload = null)
    {
        return new DiagnosticFrame(index, "server", "type.ankama.com/lok", "lok", payload ?? [0x08, 0x01]);
    }
}
