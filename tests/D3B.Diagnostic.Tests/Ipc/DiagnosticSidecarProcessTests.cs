using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Ipc;
using D3B.Diagnostic.Tests.Replay;

namespace D3B.Diagnostic.Tests.Ipc;

public sealed class DiagnosticSidecarProcessTests
{
    [Fact]
    public async Task RealEngineCompletesAnEmptySessionWhenConfigured()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The diagnostic sidecar process requires Linux Unix-domain sockets.");
        }

        string? enginePath = Environment.GetEnvironmentVariable("D3B_DIAGNOSTIC_ENGINE");
        if (string.IsNullOrWhiteSpace(enginePath))
        {
            Assert.Skip("Set D3B_DIAGNOSTIC_ENGINE to the Rust diagnostic binary to run this supervised process test.");
        }

        string directory = Path.Combine(Path.GetTempPath(), $"d3b-diagnostic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var options = new SidecarOptions
            {
                EnginePath = enginePath!,
                SocketDirectory = directory,
            };
            SidecarStartResult started = await new DiagnosticSidecarFactory().StartAsync(options, TestContext.Current.CancellationToken);
            Assert.NotNull(started.Sidecar);
            await using IDiagnosticSidecar sidecar = started.Sidecar;

            await sidecar.StartAsync(ReplayTestData.CreateSchema(), TestContext.Current.CancellationToken);
            DiagnosticSummary summary = await sidecar.CompleteAsync(TestContext.Current.CancellationToken);

            Assert.Equal(0, summary.Frames);
            Assert.Equal("complete", summary.Coverage);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
