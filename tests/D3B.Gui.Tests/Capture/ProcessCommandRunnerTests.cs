using System.Diagnostics;
using System.Globalization;
using D3B.Gui.Tests.Windowing;
using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Capture;

public sealed class ProcessCommandRunnerTests
{
    [Fact]
    public async Task TimeoutBoundsDrainWhenExitedProcessDescendantKeepsPipesOpen()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("This process pipe inheritance test requires Linux.");
        }

        string? sleep = XvfbTestCommands.ResolveExecutable("sleep");

        if (sleep is null)
        {
            Assert.Skip("This process pipe inheritance test requires the installed 'sleep' executable.");
        }

        string directory = Path.Combine(Path.GetTempPath(), $"d3b-gui-process-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string pidFile = Path.Combine(directory, "child.pid");
        string fakeCommand = Path.Combine(directory, "command-with-inherited-pipes");
        XvfbTestCommands.WriteExecutable(
            fakeCommand,
            $"#!/bin/sh\n'{sleep}' 30 &\nprintf '%s' \"$!\" > '{pidFile}'\nexit 0\n");
        int? childPid = null;

        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            CommandResult result = await ProcessCommandRunner.RunAsync(
                fakeCommand,
                [],
                TimeSpan.FromMilliseconds(300),
                environment: null,
                CancellationToken.None);
            stopwatch.Stop();

            Assert.True(File.Exists(pidFile));
            childPid = int.Parse(await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
            Assert.True(result.TimedOut);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Command returned after {stopwatch.Elapsed}.");
        }
        finally
        {
            if (childPid is int processId)
            {
                TryKillProcess(processId);
            }

            Directory.Delete(directory, recursive: true);
        }
    }

    private static void TryKillProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }
}
