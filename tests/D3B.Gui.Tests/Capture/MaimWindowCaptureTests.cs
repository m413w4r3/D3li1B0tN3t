using System.Diagnostics;
using System.Globalization;
using D3B.Gui.Capture;
using D3B.Gui.Tests.Windowing;
using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Capture;

public sealed class MaimWindowCaptureTests
{
    [Fact]
    public async Task CapturesAndDecodesARealXtermWindowWithMeasuredBorderDimensions()
    {
        string maim = RequireExecutable("maim");
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        string xdotool = RequireExecutable("xdotool");
        XvfbTestCommands.AssertCommandSucceeded(
            await XvfbTestCommands.RunAsync(xdotool, ["windowfocus", "--sync", display.WindowId.ToString(CultureInfo.InvariantCulture)], display, TestContext.Current.CancellationToken),
            "xdotool windowfocus");
        XvfbTestCommands.AssertCommandSucceeded(
            await XvfbTestCommands.RunAsync(xdotool, ["type", "D3B capture content fixture"], display, TestContext.Current.CancellationToken),
            "xdotool type");

        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        MaimWindowCapture capture = new(locator, new MaimCaptureOptions(maim), display.CreateEnvironmentReader());

        using CapturedFrame frame = await capture.CaptureAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.Equal(CaptureStatus.Captured, frame.Status);
        Assert.True(frame.ContentUsable);
        Assert.True(frame.PixelWidth > 0);
        Assert.True(frame.PixelHeight > 0);
        Assert.Equal(frame.WindowSnapshotBefore.WindowWidth, frame.PixelWidth);
        Assert.Equal(frame.WindowSnapshotBefore.WindowHeight, frame.PixelHeight);
        Assert.Equal(0, frame.WidthDifferenceFromWindow);
        Assert.Equal(0, frame.HeightDifferenceFromWindow);
        Assert.Equal(frame.WindowSnapshotBefore.Identity, frame.WindowIdentity);
        Assert.Equal(frame.WindowSnapshotBefore.Identity, frame.WindowSnapshotAfter.Identity);
    }

    [Fact]
    public async Task RecaptureReflectsAWindowMoveAndResize()
    {
        string maim = RequireExecutable("maim");
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        MaimWindowCapture capture = new(locator, new MaimCaptureOptions(maim), display.CreateEnvironmentReader());

        using CapturedFrame first = await capture.CaptureAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);
        Assert.Equal(CaptureStatus.Captured, first.Status);

        await display.MoveAndResizeAsync(220, 180, 360, 240);

        using CapturedFrame second = await capture.CaptureAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.Equal(CaptureStatus.Captured, second.Status);
        Assert.Equal(220, second.WindowSnapshotBefore.DesktopOrigin.X);
        Assert.Equal(180, second.WindowSnapshotBefore.DesktopOrigin.Y);
        Assert.Equal(360, second.WindowSnapshotBefore.ClientWidth);
        Assert.Equal(240, second.WindowSnapshotBefore.ClientHeight);
        Assert.Equal(second.WindowSnapshotBefore.WindowWidth, second.PixelWidth);
        Assert.Equal(second.WindowSnapshotBefore.WindowHeight, second.PixelHeight);
    }

    [Fact]
    public async Task UnmappedWindowIsRejectedExplicitly()
    {
        string maim = RequireExecutable("maim");
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        await display.UnmapWindowAsync();
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        MaimWindowCapture capture = new(locator, new MaimCaptureOptions(maim), display.CreateEnvironmentReader());

        using CapturedFrame frame = await capture.CaptureAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.Equal(CaptureStatus.WindowHidden, frame.Status);
        Assert.False(frame.ContentUsable);
    }

    [Fact]
    public async Task WindowGeometryChangedDuringCaptureReturnsWindowChanged()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        X11WindowLocator systemLocator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        GameWindowSnapshot before = await systemLocator.LocateAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            TestContext.Current.CancellationToken);
        GameWindowSnapshot after = before with
        {
            DesktopOrigin = new DesktopPixels(before.DesktopOrigin.X + 1, before.DesktopOrigin.Y)
        };
        SequenceGameWindowLocator locator = new(before, after);
        string directory = CreateTemporaryDirectory();
        string fakeMaim = Path.Combine(directory, "fake-maim");
        XvfbTestCommands.WriteExecutable(fakeMaim, "#!/bin/sh\nexit 0\n");
        MaimWindowCapture capture = new(locator, new MaimCaptureOptions(fakeMaim), display.CreateEnvironmentReader());

        using CapturedFrame frame = await capture.CaptureAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.Equal(CaptureStatus.WindowChanged, frame.Status);
        Assert.False(frame.ContentUsable);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task WindowChangeTakesPrecedenceOverMaimFailure()
    {
        GameWindowSnapshot before = CreateTestSnapshot();
        GameWindowSnapshot after = before with
        {
            DesktopOrigin = new DesktopPixels(before.DesktopOrigin.X + 1, before.DesktopOrigin.Y)
        };
        SequenceGameWindowLocator locator = new(before, after);
        string directory = CreateTemporaryDirectory();
        string fakeMaim = Path.Combine(directory, "failed-maim");
        XvfbTestCommands.WriteExecutable(fakeMaim, "#!/bin/sh\nexit 1\n");
        MaimWindowCapture capture = new(locator, new MaimCaptureOptions(fakeMaim), GetTestEnvironmentValue);

        try
        {
            using CapturedFrame frame = await capture.CaptureAsync(
                before.Identity.Xid,
                new GameWindowExpectation("xterm"),
                CancellationToken.None);

            Assert.Equal(CaptureStatus.WindowChanged, frame.Status);
            Assert.False(frame.ContentUsable);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MissingMaimExecutableReturnsBinaryUnavailable()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        MaimWindowCapture capture = new(
            locator,
            new MaimCaptureOptions(Path.Combine(Path.GetTempPath(), $"missing-maim-{Guid.NewGuid():N}")),
            display.CreateEnvironmentReader());

        using CapturedFrame frame = await capture.CaptureAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.Equal(CaptureStatus.BinaryUnavailable, frame.Status);
        Assert.False(frame.ContentUsable);
        Assert.NotNull(frame.WindowSnapshotAfter);
    }

    [Fact]
    public async Task CaptureTimeoutKillsTheMaimProcessTree()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        string directory = CreateTemporaryDirectory();
        string pidFile = Path.Combine(directory, "child.pid");
        string fakeMaim = Path.Combine(directory, "slow-maim");
        XvfbTestCommands.WriteExecutable(fakeMaim, $"#!/bin/sh\nsleep 30 &\necho $! > '{pidFile}'\nwait\n");
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        MaimWindowCapture capture = new(
            locator,
            new MaimCaptureOptions(fakeMaim, TimeSpan.FromMilliseconds(300)),
            display.CreateEnvironmentReader());

        using CapturedFrame frame = await capture.CaptureAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.Equal(CaptureStatus.TimedOut, frame.Status);
        Assert.True(File.Exists(pidFile));
        int childPid = int.Parse(await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
        Assert.True(await XvfbTestCommands.IsProcessStoppedAsync(childPid, TimeSpan.FromSeconds(3)));
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task CaptureTimeoutBoundsDrainWhenExitedMaimDescendantKeepsPipesOpen()
    {
        GameWindowSnapshot snapshot = CreateTestSnapshot();
        string directory = CreateTemporaryDirectory();
        string pidFile = Path.Combine(directory, "child.pid");
        string fakeMaim = Path.Combine(directory, "maim-with-inherited-pipes");
        string sleep = RequireExecutable("sleep");
        XvfbTestCommands.WriteExecutable(
            fakeMaim,
            $"#!/bin/sh\n'{sleep}' 30 &\nprintf '%s' \"$!\" > '{pidFile}'\nexit 0\n");
        SequenceGameWindowLocator locator = new(snapshot);
        MaimWindowCapture capture = new(
            locator,
            new MaimCaptureOptions(fakeMaim, TimeSpan.FromMilliseconds(300)),
            GetTestEnvironmentValue);
        int? childPid = null;

        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            using CapturedFrame frame = await capture.CaptureAsync(
                snapshot.Identity.Xid,
                new GameWindowExpectation("xterm"),
                CancellationToken.None);
            stopwatch.Stop();

            Assert.True(File.Exists(pidFile));
            childPid = int.Parse(await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
            Assert.Equal(CaptureStatus.TimedOut, frame.Status);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Capture returned after {stopwatch.Elapsed}.");
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

    [Fact]
    public async Task CaptureByteLimitKillsTheMaimProcessTree()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        string directory = CreateTemporaryDirectory();
        string pidFile = Path.Combine(directory, "process.pid");
        string fakeMaim = Path.Combine(directory, "large-maim");
        XvfbTestCommands.WriteExecutable(fakeMaim, $"#!/bin/sh\necho $$ > '{pidFile}'\nhead -c 65536 /dev/zero\nwhile :; do sleep 1; done\n");
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        MaimWindowCapture capture = new(
            locator,
            new MaimCaptureOptions(fakeMaim, maxCaptureBytes: 512),
            display.CreateEnvironmentReader());

        using CapturedFrame frame = await capture.CaptureAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.Equal(CaptureStatus.OutputTooLarge, frame.Status);
        Assert.True(File.Exists(pidFile));
        int processId = int.Parse(await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
        Assert.True(await XvfbTestCommands.IsProcessStoppedAsync(processId, TimeSpan.FromSeconds(3)));
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task InvalidPngReturnsDecodeFailed()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        string directory = CreateTemporaryDirectory();
        string fakeMaim = Path.Combine(directory, "invalid-png-maim");
        XvfbTestCommands.WriteExecutable(fakeMaim, "#!/bin/sh\nprintf 'not a PNG image'\n");
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        MaimWindowCapture capture = new(locator, new MaimCaptureOptions(fakeMaim), display.CreateEnvironmentReader());

        using CapturedFrame frame = await capture.CaptureAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.Equal(CaptureStatus.DecodeFailed, frame.Status);
        Assert.False(frame.ContentUsable);
        Directory.Delete(directory, recursive: true);
    }

    private static string RequireExecutable(string command)
    {
        string? executable = XvfbTestCommands.ResolveExecutable(command);

        if (executable is null)
        {
            Assert.Skip($"This X11 test requires the installed '{command}' executable.");
        }

        return executable;
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"d3b-gui-capture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static GameWindowSnapshot CreateTestSnapshot()
    {
        return new GameWindowSnapshot(
            new GameWindowIdentity(42, 1234, "/usr/bin/xterm", "Synthetic capture window", GameWindowIdentityStatus.Verified),
            Exists: true,
            IsMapped: true,
            IsViewable: true,
            IsMinimized: false,
            IsFocused: true,
            IsActive: true,
            IsXTestAvailable: true,
            IsSessionX11: true,
            HasValidDisplay: true,
            GeometryReliable: true,
            DesktopOrigin: new DesktopPixels(20, 30),
            WindowWidth: 640,
            WindowHeight: 480,
            BorderWidth: 0,
            ClientWidth: 640,
            ClientHeight: 480,
            Failure: null,
            CapturedAt: DateTimeOffset.UtcNow);
    }

    private static string? GetTestEnvironmentValue(string name)
    {
        return name switch
        {
            "DISPLAY" => ":100",
            "HOME" => Environment.GetEnvironmentVariable("HOME"),
            _ => null
        };
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
