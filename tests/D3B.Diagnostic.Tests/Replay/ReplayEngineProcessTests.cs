using D3B.Diagnostic.Replay;
using D3B.Diagnostic.Schema;
using D3B.Diagnostic.Tests.Schema;

namespace D3B.Diagnostic.Tests.Replay;

public sealed class ReplayEngineProcessTests
{
    [Fact]
    public async Task RunsTheConfiguredRustEngineWhenAvailable()
    {
        var executable = Environment.GetEnvironmentVariable("D3B_DIAGNOSTIC_ENGINE");
        if (string.IsNullOrWhiteSpace(executable))
        {
            Assert.Skip("D3B_DIAGNOSTIC_ENGINE is not set.");
        }

        if (!File.Exists(executable))
        {
            Assert.Skip("D3B_DIAGNOSTIC_ENGINE does not point to an executable.");
        }

        if (OperatingSystem.IsLinux())
        {
            var permissions = File.GetUnixFileMode(executable);
            var executableBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if ((permissions & executableBits) == 0)
            {
                Assert.Skip("D3B_DIAGNOSTIC_ENGINE does not point to an executable.");
            }
        }

        var schema = DiagnosticSchemaExporter.Export("synthetic", DiagnosticTestSchema.CreateProtocolSchema());
        var frame = ReplayTestData.CreateFrame();
        var result = await ReplayEngineProcess.AnalyzeAsync(
            executable,
            ["replay"],
            schema,
            [frame],
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Summary.Frames);
        Assert.Equal(frame.Index, Assert.Single(result.Analyses).Index);
    }
}
