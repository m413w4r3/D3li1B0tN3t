using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Windowing;

public sealed class X11WindowLocatorTests
{
    [Fact]
    public async Task DisposeRemovesTheOwnedDisplayLockAndSocket()
    {
        string lockFilePath;
        string socketFilePath;

        await using (XvfbTestDisplay display = await XvfbTestDisplay.StartAsync())
        {
            lockFilePath = display.LockFilePath;
            socketFilePath = display.SocketFilePath;
        }

        Assert.False(File.Exists(lockFilePath));
        Assert.False(File.Exists(socketFilePath));
    }

    [Fact]
    public async Task LocateInspectsTheExplicitXidAndVerifiesTheXtermProcess()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        X11WindowLocator locator = CreateLocator(display);

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.True(snapshot.Exists);
        Assert.True(snapshot.IsMapped);
        Assert.True(snapshot.IsViewable);
        Assert.False(snapshot.IsMinimized);
        Assert.True(snapshot.IsXTestAvailable);
        Assert.True(snapshot.IsSessionX11);
        Assert.True(snapshot.HasValidDisplay);
        Assert.True(snapshot.GeometryReliable);
        Assert.True(snapshot.Identity.ProcessId > 0);
        Assert.NotNull(snapshot.Identity.ExecutablePath);
        Assert.Equal("xterm", Path.GetFileName(snapshot.Identity.ExecutablePath));
        Assert.Equal(GameWindowIdentityStatus.Verified, snapshot.Identity.Status);
        Assert.True(snapshot.ClientWidth > 0);
        Assert.True(snapshot.ClientHeight > 0);
        Assert.True(snapshot.WindowWidth > 0);
        Assert.True(snapshot.WindowHeight > 0);
    }

    [Fact]
    public async Task SnapshotReflectsWindowMovementAndResize()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        X11WindowLocator locator = CreateLocator(display);

        await display.MoveAndResizeAsync(210, 170, 333, 222);

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.True(snapshot.GeometryReliable);
        Assert.Equal(210, snapshot.DesktopOrigin.X);
        Assert.Equal(170, snapshot.DesktopOrigin.Y);
        Assert.Equal(333, snapshot.ClientWidth);
        Assert.Equal(222, snapshot.ClientHeight);
    }

    [Fact]
    public async Task UnmappedWindowIsReportedAsNotMapped()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        X11WindowLocator locator = CreateLocator(display);

        await display.UnmapWindowAsync();

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.True(snapshot.Exists);
        Assert.False(snapshot.IsMapped);
        Assert.False(snapshot.IsViewable);
    }

    [Fact]
    public async Task ReplacedWindowIsReportedAsNotFound()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        X11WindowLocator locator = CreateLocator(display);

        await display.KillPrimaryWindowAsync();

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.False(snapshot.Exists);
        Assert.Equal(GameWindowIdentityStatus.NotFound, snapshot.Identity.Status);
        Assert.NotNull(snapshot.Failure);
    }

    [Fact]
    public async Task MissingNetWmPidLeavesTheWindowExistingButUnverified()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        X11WindowLocator locator = CreateLocator(display);

        await display.RemoveProcessIdPropertyAsync();

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.True(snapshot.Exists);
        Assert.Equal(GameWindowIdentityStatus.UnverifiedProcessIdUnavailable, snapshot.Identity.Status);
    }

    [Fact]
    public async Task ExpectedExecutableMustMatchTheProcessBasename()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        X11WindowLocator locator = CreateLocator(display);

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            display.WindowId,
            new GameWindowExpectation("Dofus"),
            CancellationToken.None);

        Assert.True(snapshot.Exists);
        Assert.Equal(GameWindowIdentityStatus.UnverifiedExecutableMismatch, snapshot.Identity.Status);
        Assert.Equal("xterm", Path.GetFileName(snapshot.Identity.ExecutablePath));
    }

    [Fact]
    public async Task WindowWithoutFocusIsNotReportedAsFocused()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        X11WindowLocator locator = CreateLocator(display);

        await display.FocusOtherWindowAsync();

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            display.WindowId,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.False(snapshot.IsFocused);
    }

    [Fact]
    public async Task NonX11SessionIsReportedWithoutQueryingTheDisplay()
    {
        Func<string, string?> environmentReader = name =>
        {
            if (string.Equals(name, "XDG_SESSION_TYPE", StringComparison.Ordinal))
            {
                return "wayland";
            }

            if (string.Equals(name, "DISPLAY", StringComparison.Ordinal))
            {
                return ":65534";
            }

            return Environment.GetEnvironmentVariable(name);
        };

        Func<
            string,
            IReadOnlyList<string>,
            TimeSpan,
            IReadOnlyDictionary<string, string?>?,
            CancellationToken,
            Task<CommandResult>> commandRunner = (_, _, _, _, _) =>
                Task.FromResult(new CommandResult(0, false, "XTEST", string.Empty, null));
        X11WindowLocator locator = new(environmentReader, commandRunner);

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            1,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.False(snapshot.IsSessionX11);
        Assert.False(snapshot.HasValidDisplay);
        Assert.False(snapshot.Exists);
        Assert.NotNull(snapshot.Failure);
    }

    [Fact]
    public async Task InvalidDisplayIsReportedExplicitly()
    {
        Func<string, string?> environmentReader = name =>
        {
            if (string.Equals(name, "XDG_SESSION_TYPE", StringComparison.Ordinal))
            {
                return "x11";
            }

            if (string.Equals(name, "DISPLAY", StringComparison.Ordinal))
            {
                return ":65534";
            }

            return Environment.GetEnvironmentVariable(name);
        };
        X11WindowLocator locator = new(environmentReader, ProcessCommandRunner.RunAsync);

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            1,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.True(snapshot.IsSessionX11);
        Assert.False(snapshot.HasValidDisplay);
        Assert.False(snapshot.IsXTestAvailable);
        Assert.False(snapshot.Exists);
        Assert.NotNull(snapshot.Failure);
    }

    [Fact]
    public async Task AbsentExternalCommandIsReturnedInTheSnapshot()
    {
        Func<string, string?> environmentReader = CreateUnavailableDisplayEnvironment();
        Func<
            string,
            IReadOnlyList<string>,
            TimeSpan,
            IReadOnlyDictionary<string, string?>?,
            CancellationToken,
            Task<CommandResult>> commandRunner = (_, _, _, _, _) =>
                Task.FromResult(new CommandResult(null, false, "", "", "The executable is absent."));
        X11WindowLocator locator = new(environmentReader, commandRunner);

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            1,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.False(snapshot.HasValidDisplay);
        Assert.NotNull(snapshot.Failure);
        Assert.Contains("xdpyinfo", snapshot.Failure, StringComparison.Ordinal);
        Assert.Contains("absent", snapshot.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalCommandFailureIsReturnedInTheSnapshot()
    {
        Func<string, string?> environmentReader = CreateUnavailableDisplayEnvironment();
        Func<
            string,
            IReadOnlyList<string>,
            TimeSpan,
            IReadOnlyDictionary<string, string?>?,
            CancellationToken,
            Task<CommandResult>> commandRunner = (_, _, _, _, _) =>
                Task.FromResult(new CommandResult(1, false, "", "display query denied", null));
        X11WindowLocator locator = new(environmentReader, commandRunner);

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            1,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.False(snapshot.HasValidDisplay);
        Assert.NotNull(snapshot.Failure);
        Assert.Contains("display query denied", snapshot.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalCommandTimeoutIsReturnedInTheSnapshot()
    {
        Func<string, string?> environmentReader = CreateUnavailableDisplayEnvironment();
        Func<
            string,
            IReadOnlyList<string>,
            TimeSpan,
            IReadOnlyDictionary<string, string?>?,
            CancellationToken,
            Task<CommandResult>> commandRunner = (_, _, _, _, _) =>
                Task.FromResult(new CommandResult(null, true, "", "", "The process timed out."));
        X11WindowLocator locator = new(environmentReader, commandRunner);

        GameWindowSnapshot snapshot = await locator.LocateAsync(
            1,
            new GameWindowExpectation("xterm"),
            CancellationToken.None);

        Assert.False(snapshot.HasValidDisplay);
        Assert.NotNull(snapshot.Failure);
        Assert.Contains("timed out", snapshot.Failure, StringComparison.Ordinal);
    }

    private static X11WindowLocator CreateLocator(XvfbTestDisplay display)
    {
        return new X11WindowLocator(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
    }

    private static Func<string, string?> CreateUnavailableDisplayEnvironment()
    {
        return name =>
        {
            if (string.Equals(name, "XDG_SESSION_TYPE", StringComparison.Ordinal))
            {
                return "x11";
            }

            if (string.Equals(name, "DISPLAY", StringComparison.Ordinal))
            {
                return ":65534";
            }

            return Environment.GetEnvironmentVariable(name);
        };
    }
}
