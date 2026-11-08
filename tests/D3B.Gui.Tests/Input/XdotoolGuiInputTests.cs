using D3B.Gui.Input;
using D3B.Gui.Tests.Capture;
using D3B.Gui.Tests.Windowing;
using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Input;

public sealed class XdotoolGuiInputTests
{
    [Fact]
    public async Task ClickUsesGlobalXTestInputAndEmitsButtonPressOnTheFakeWindow()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        await using XevTestWindow window = await XevTestWindow.StartAsync(display);
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        GameWindowSnapshot snapshot = await locator.LocateAsync(
            window.WindowId,
            new GameWindowExpectation("xev", requireVerifiedProcess: true),
            CancellationToken.None);
        XdotoolGuiInput input = new(locator, RequireExecutable("xdotool"), display.CreateEnvironmentReader());
        DesktopPixels point = GetClientCenter(snapshot);

        GuiInputResult result = await input.ClickAsync(point, snapshot, CancellationToken.None);
        bool observed = await window.HasButtonPressAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(GuiInputStatus.Sent, result.Status);
        Assert.True(observed, "xev did not observe the XTest ButtonPress event.");
    }

    [Fact]
    public async Task ClickIsRefusedWhenTheExpectedWindowIsNotFocused()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        await using XevTestWindow window = await XevTestWindow.StartAsync(display);
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        GameWindowSnapshot snapshot = await locator.LocateAsync(
            window.WindowId,
            new GameWindowExpectation("xev", requireVerifiedProcess: true),
            CancellationToken.None);
        await display.FocusOtherWindowAsync();
        XdotoolGuiInput input = new(locator, RequireExecutable("xdotool"), display.CreateEnvironmentReader());

        GuiInputResult result = await input.ClickAsync(GetClientCenter(snapshot), snapshot, CancellationToken.None);
        bool observed = await window.HasButtonPressAsync(TimeSpan.FromMilliseconds(300));

        Assert.Equal(GuiInputStatus.WindowNotActive, result.Status);
        Assert.False(observed);
    }

    [Fact]
    public async Task ClickIsRefusedOutsideTheSafeClientArea()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        await using XevTestWindow window = await XevTestWindow.StartAsync(display);
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        GameWindowSnapshot snapshot = await locator.LocateAsync(
            window.WindowId,
            new GameWindowExpectation("xev", requireVerifiedProcess: true),
            CancellationToken.None);
        DesktopPixels outside = new(snapshot.DesktopOrigin.X, snapshot.DesktopOrigin.Y);
        XdotoolGuiInput input = new(locator, RequireExecutable("xdotool"), display.CreateEnvironmentReader());

        GuiInputResult result = await input.ClickAsync(outside, snapshot, CancellationToken.None);
        bool observed = await window.HasButtonPressAsync(TimeSpan.FromMilliseconds(300));

        Assert.Equal(GuiInputStatus.OutsideWindow, result.Status);
        Assert.False(observed);
    }

    [Fact]
    public async Task ClickIsRefusedWhenTheExpectedIdentityChanged()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        await using XevTestWindow window = await XevTestWindow.StartAsync(display);
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        GameWindowSnapshot snapshot = await locator.LocateAsync(
            window.WindowId,
            new GameWindowExpectation("xev", requireVerifiedProcess: true),
            CancellationToken.None);
        GameWindowSnapshot stale = snapshot with
        {
            Identity = snapshot.Identity with { ProcessId = snapshot.Identity.ProcessId + 1 }
        };
        XdotoolGuiInput input = new(locator, RequireExecutable("xdotool"), display.CreateEnvironmentReader());

        GuiInputResult result = await input.ClickAsync(GetClientCenter(snapshot), stale, CancellationToken.None);
        bool observed = await window.HasButtonPressAsync(TimeSpan.FromMilliseconds(300));

        Assert.Equal(GuiInputStatus.WindowChanged, result.Status);
        Assert.False(observed);
    }

    [Fact]
    public async Task ClickIsRefusedWhenFocusChangesAfterPointerMovement()
    {
        await AssertClickIsRefusedAfterMoveAsync(
            snapshot => snapshot with { IsFocused = false, IsActive = false },
            GuiInputStatus.WindowNotActive);
    }

    [Fact]
    public async Task ClickIsRefusedWhenWindowMovesAfterPointerMovement()
    {
        await AssertClickIsRefusedAfterMoveAsync(
            snapshot => snapshot with
            {
                DesktopOrigin = new DesktopPixels(snapshot.DesktopOrigin.X + 1, snapshot.DesktopOrigin.Y)
            },
            GuiInputStatus.WindowChanged);
    }

    [Fact]
    public async Task ClickIsNotSentWhenFocusChangesAfterPointerMovementWithoutAnXServer()
    {
        GameWindowSnapshot snapshot = CreateTestSnapshot();
        SequenceGameWindowLocator locator = new(
            snapshot,
            snapshot with { IsFocused = false, IsActive = false });
        string directory = Path.Combine(Path.GetTempPath(), $"d3b-gui-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string moveMarker = Path.Combine(directory, "move");
        string clickMarker = Path.Combine(directory, "click");
        string fakeXdotool = Path.Combine(directory, "fake-xdotool");
        XvfbTestCommands.WriteExecutable(
            fakeXdotool,
            $"#!/bin/sh\nif [ \"$1\" = \"mousemove\" ]; then printf moved > '{moveMarker}'; exit 0; fi\nif [ \"$1\" = \"click\" ]; then printf clicked > '{clickMarker}'; exit 0; fi\nexit 1\n");
        XdotoolGuiInput input = new(locator, fakeXdotool, GetTestEnvironmentValue);

        try
        {
            GuiInputResult result = await input.ClickAsync(GetClientCenter(snapshot), snapshot, CancellationToken.None);

            Assert.Equal(GuiInputStatus.WindowNotActive, result.Status);
            Assert.True(File.Exists(moveMarker));
            Assert.False(File.Exists(clickMarker));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CancellationBeforeClickStartsNoInputProcessOrEvent()
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        await using XevTestWindow window = await XevTestWindow.StartAsync(display);
        X11WindowLocator locator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        GameWindowSnapshot snapshot = await locator.LocateAsync(
            window.WindowId,
            new GameWindowExpectation("xev", requireVerifiedProcess: true),
            CancellationToken.None);
        string directory = Path.Combine(Path.GetTempPath(), $"d3b-gui-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string markerPath = Path.Combine(directory, "started");
        string fakeXdotool = Path.Combine(directory, "fake-xdotool");
        XvfbTestCommands.WriteExecutable(fakeXdotool, $"#!/bin/sh\nprintf started > '{markerPath}'\n");
        XdotoolGuiInput input = new(locator, fakeXdotool, display.CreateEnvironmentReader());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        GuiInputResult result = await input.ClickAsync(GetClientCenter(snapshot), snapshot, cancellation.Token);
        bool observed = await window.HasButtonPressAsync(TimeSpan.FromMilliseconds(300));

        Assert.Equal(GuiInputStatus.Canceled, result.Status);
        Assert.False(observed);
        Assert.False(File.Exists(markerPath));
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task DisabledInputNeverRunsACommand()
    {
        DisabledGuiInput input = new();
        GameWindowSnapshot snapshot = new(
            new GameWindowIdentity(1, null, null, null, GameWindowIdentityStatus.NotFound),
            Exists: false,
            IsMapped: false,
            IsViewable: false,
            IsMinimized: false,
            IsFocused: false,
            IsActive: false,
            IsXTestAvailable: false,
            IsSessionX11: false,
            HasValidDisplay: false,
            GeometryReliable: false,
            DesktopOrigin: default,
            WindowWidth: 0,
            WindowHeight: 0,
            BorderWidth: 0,
            ClientWidth: 0,
            ClientHeight: 0,
            Failure: null,
            CapturedAt: DateTimeOffset.UtcNow);

        GuiInputResult result = await input.ClickAsync(default, snapshot, CancellationToken.None);

        Assert.Equal(GuiInputStatus.Disabled, result.Status);
    }

    private static DesktopPixels GetClientCenter(GameWindowSnapshot snapshot)
    {
        return new DesktopPixels(
            snapshot.DesktopOrigin.X + snapshot.BorderWidth + snapshot.ClientWidth / 2,
            snapshot.DesktopOrigin.Y + snapshot.BorderWidth + snapshot.ClientHeight / 2);
    }

    private static async Task AssertClickIsRefusedAfterMoveAsync(
        Func<GameWindowSnapshot, GameWindowSnapshot> changeAfterMove,
        GuiInputStatus expectedStatus)
    {
        await using XvfbTestDisplay display = await XvfbTestDisplay.StartAsync();
        await using XevTestWindow window = await XevTestWindow.StartAsync(display);
        X11WindowLocator systemLocator = new(display.CreateEnvironmentReader(), ProcessCommandRunner.RunAsync);
        GameWindowSnapshot snapshot = await systemLocator.LocateAsync(
            window.WindowId,
            new GameWindowExpectation("xev", requireVerifiedProcess: true),
            CancellationToken.None);
        GameWindowSnapshot changed = changeAfterMove(snapshot);
        SequenceGameWindowLocator locator = new(snapshot, changed);
        string directory = Path.Combine(Path.GetTempPath(), $"d3b-gui-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string moveMarker = Path.Combine(directory, "move");
        string clickMarker = Path.Combine(directory, "click");
        string fakeXdotool = Path.Combine(directory, "fake-xdotool");
        XvfbTestCommands.WriteExecutable(
            fakeXdotool,
            $"#!/bin/sh\nif [ \"$1\" = \"mousemove\" ]; then printf moved > '{moveMarker}'; exit 0; fi\nif [ \"$1\" = \"click\" ]; then printf clicked > '{clickMarker}'; exit 0; fi\nexit 1\n");
        XdotoolGuiInput input = new(locator, fakeXdotool, display.CreateEnvironmentReader());

        try
        {
            GuiInputResult result = await input.ClickAsync(GetClientCenter(snapshot), snapshot, CancellationToken.None);
            bool observed = await window.HasButtonPressAsync(TimeSpan.FromMilliseconds(300));

            Assert.Equal(expectedStatus, result.Status);
            Assert.True(File.Exists(moveMarker));
            Assert.False(File.Exists(clickMarker));
            Assert.False(observed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static GameWindowSnapshot CreateTestSnapshot()
    {
        return new GameWindowSnapshot(
            new GameWindowIdentity(42, 1234, "/usr/bin/xterm", "Synthetic input window", GameWindowIdentityStatus.Verified),
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

    private static string RequireExecutable(string command)
    {
        string? executable = XvfbTestCommands.ResolveExecutable(command);

        if (executable is null)
        {
            Assert.Skip($"XTest input tests require the installed '{command}' executable.");
        }

        return executable;
    }
}
