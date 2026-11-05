using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Ipc;
using D3B.Diagnostic.Observation;
using D3B.Diagnostic.Replay;
using D3B.Diagnostic.Tests.Replay;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Diagnostic.Tests.Observation;

public sealed class DiagnosticSessionCrashTests
{
    [Fact]
    public async Task ASidecarCrashDuringAnalysisMarksCoveragePartialWithoutThrowingToCaller()
    {
        var sidecar = new FakeDiagnosticSidecar
        {
            Analyze = _ => throw new EndOfStreamException("sidecar exited"),
        };
        var session = new DiagnosticSession(
            new SidecarOptions
            {
                EnginePath = "unused",
                SocketDirectory = "unused",
                QueueCapacity = 4,
                HandshakeTimeout = TimeSpan.FromSeconds(2),
                ResponseTimeout = TimeSpan.FromSeconds(2),
            },
            ReplayTestData.CreateSchema(),
            new FakeDiagnosticSidecarFactory(sidecar),
            new FakeLogger<DiagnosticSession>(new FakeLogCollector()));

        bool accepted = session.TryEnqueue(new DiagnosticFrame(0, "server", "type.ankama.com/lok", "lok", [0x08, 0x01]));
        await session.DisposeAsync();

        Assert.True(accepted);
        Assert.Equal("partial", session.Snapshot.Coverage);
        Assert.Equal(DiagnosticErrorCode.IoError, session.Snapshot.ErrorCode);
        Assert.Equal(1, session.Snapshot.DroppedFrames);
    }
}
