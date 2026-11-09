using System.Diagnostics;
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
        Assert.True(result.ClickMayHaveBeenEmitted);
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
    public async Task CancellationAfterPointerMovementPreventsClickLaunch()
    {
        GameWindowSnapshot snapshot = CreateTestSnapshot();
        using CancellationTokenSource cancellation = new();
        CancelingGameWindowLocator locator = new(snapshot, cancellation, cancelOnLocate: 2);
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
            GuiInputResult result = await input.ClickAsync(GetClientCenter(snapshot), snapshot, cancellation.Token);

            Assert.Equal(GuiInputStatus.Canceled, result.Status);
            Assert.False(result.ClickMayHaveBeenEmitted);
            Assert.True(File.Exists(moveMarker));
            Assert.False(File.Exists(clickMarker));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LeaseRevocationDuringFinalWindowRevalidationPreventsClickLaunch()
    {
        GameWindowSnapshot snapshot = CreateTestSnapshot();
        TaskCompletionSource revalidationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseRevalidation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        BlockingGameWindowLocator locator = new(snapshot, revalidationStarted, releaseRevalidation);
        string directory = Path.Combine(Path.GetTempPath(), $"d3b-gui-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string moveMarker = Path.Combine(directory, "move");
        string clickMarker = Path.Combine(directory, "click");
        string fakeXdotool = Path.Combine(directory, "fake-xdotool");
        XvfbTestCommands.WriteExecutable(
            fakeXdotool,
            $"#!/bin/sh\nif [ \"$1\" = \"mousemove\" ]; then printf moved > '{moveMarker}'; exit 0; fi\nif [ \"$1\" = \"click\" ]; then printf clicked > '{clickMarker}'; exit 0; fi\nexit 1\n");
        IGuiInput input = new XdotoolGuiInput(locator, fakeXdotool, GetTestEnvironmentValue);
        GuiClickLease lease = new();

        try
        {
            Task<GuiInputResult> execution = input.ClickAsync(
                GetClientCenter(snapshot),
                snapshot,
                lease,
                CancellationToken.None);
            await revalidationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await Task.Run(lease.Cancel, TestContext.Current.CancellationToken);
            releaseRevalidation.TrySetResult();
            GuiInputResult result = await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal(GuiInputStatus.Canceled, result.Status);
            Assert.False(result.ClickMayHaveBeenEmitted);
            Assert.True(File.Exists(moveMarker));
            Assert.False(File.Exists(clickMarker));
            Assert.False(lease.LaunchStarted);
        }
        finally
        {
            releaseRevalidation.TrySetResult();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FailureAfterLeaseLaunchPointMayHaveEmittedClick()
    {
        GameWindowSnapshot snapshot = CreateTestSnapshot();
        SequenceGameWindowLocator locator = new(snapshot, snapshot);
        string directory = Path.Combine(Path.GetTempPath(), $"d3b-gui-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string clickMarker = Path.Combine(directory, "click");
        string fakeXdotool = Path.Combine(directory, "fake-xdotool");
        XvfbTestCommands.WriteExecutable(
            fakeXdotool,
            $"#!/bin/sh\nif [ \"$1\" = \"mousemove\" ]; then exit 0; fi\nif [ \"$1\" = \"click\" ]; then printf clicked > '{clickMarker}'; exit 7; fi\nexit 1\n");
        IGuiInput input = new XdotoolGuiInput(locator, fakeXdotool, GetTestEnvironmentValue);
        GuiClickLease lease = new();

        try
        {
            GuiInputResult result = await input.ClickAsync(
                GetClientCenter(snapshot),
                snapshot,
                lease,
                CancellationToken.None);

            Assert.Equal(GuiInputStatus.Failed, result.Status);
            Assert.True(result.ClickMayHaveBeenEmitted);
            Assert.True(lease.LaunchStarted);
            Assert.True(File.Exists(clickMarker));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FailedClickCommandAfterLaunchMayHaveEmittedInput()
    {
        GameWindowSnapshot snapshot = CreateTestSnapshot();
        SequenceGameWindowLocator locator = new(snapshot, snapshot);
        string directory = Path.Combine(Path.GetTempPath(), $"d3b-gui-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string clickMarker = Path.Combine(directory, "click");
        string fakeXdotool = Path.Combine(directory, "fake-xdotool");
        XvfbTestCommands.WriteExecutable(
            fakeXdotool,
            $"#!/bin/sh\nif [ \"$1\" = \"mousemove\" ]; then exit 0; fi\nif [ \"$1\" = \"click\" ]; then printf started > '{clickMarker}'; exit 1; fi\nexit 1\n");
        XdotoolGuiInput input = new(locator, fakeXdotool, GetTestEnvironmentValue);

        try
        {
            GuiInputResult result = await input.ClickAsync(GetClientCenter(snapshot), snapshot, CancellationToken.None);

            Assert.Equal(GuiInputStatus.Failed, result.Status);
            Assert.True(result.ClickMayHaveBeenEmitted);
            Assert.True(File.Exists(clickMarker));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ClickCommandTimeoutAfterLaunchMayHaveEmittedInput()
    {
        GameWindowSnapshot snapshot = CreateTestSnapshot();
        SequenceGameWindowLocator locator = new(snapshot, snapshot);
        string directory = Path.Combine(Path.GetTempPath(), $"d3b-gui-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string clickMarker = Path.Combine(directory, "click");
        string fakeXdotool = Path.Combine(directory, "fake-xdotool");
        XvfbTestCommands.WriteExecutable(
            fakeXdotool,
            $"#!/bin/sh\nif [ \"$1\" = \"mousemove\" ]; then exit 0; fi\nif [ \"$1\" = \"click\" ]; then printf started > '{clickMarker}'; exec sleep 5; fi\nexit 1\n");
        XdotoolGuiInput input = new(locator, fakeXdotool);

        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            GuiInputResult result = await input.ClickAsync(GetClientCenter(snapshot), snapshot, CancellationToken.None);
            stopwatch.Stop();

            Assert.Equal(GuiInputStatus.TimedOut, result.Status);
            Assert.NotEqual(GuiInputStatus.Sent, result.Status);
            Assert.True(result.ClickMayHaveBeenEmitted);
            Assert.True(File.Exists(clickMarker));
            Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CancellationAfterClickLaunchMayHaveEmittedInput()
    {
        GameWindowSnapshot snapshot = CreateTestSnapshot();
        SequenceGameWindowLocator locator = new(snapshot, snapshot);
        string directory = Path.Combine(Path.GetTempPath(), $"d3b-gui-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string clickMarker = Path.Combine(directory, "click");
        string fakeXdotool = Path.Combine(directory, "fake-xdotool");
        XvfbTestCommands.WriteExecutable(
            fakeXdotool,
            $"#!/bin/sh\nif [ \"$1\" = \"mousemove\" ]; then exit 0; fi\nif [ \"$1\" = \"click\" ]; then printf started > '{clickMarker}'; exec sleep 5; fi\nexit 1\n");
        XdotoolGuiInput input = new(locator, fakeXdotool, GetTestEnvironmentValue);
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(1));

        try
        {
            GuiInputResult result = await input.ClickAsync(GetClientCenter(snapshot), snapshot, cancellation.Token);

            Assert.Equal(GuiInputStatus.Canceled, result.Status);
            Assert.True(result.ClickMayHaveBeenEmitted);
            Assert.True(File.Exists(clickMarker));
        }
        finally
        {
            cancellation.Cancel();
            Directory.Delete(directory, recursive: true);
        }
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

    private sealed class CancelingGameWindowLocator : IGameWindowLocator
    {
        private readonly GameWindowSnapshot _snapshot;
        private readonly CancellationTokenSource _cancellation;
        private readonly int _cancelOnLocate;
        private int _locateCount;

        public CancelingGameWindowLocator(
            GameWindowSnapshot snapshot,
            CancellationTokenSource cancellation,
            int cancelOnLocate)
        {
            _snapshot = snapshot;
            _cancellation = cancellation;
            _cancelOnLocate = cancelOnLocate;
        }

        public Task<GameWindowSnapshot> LocateAsync(
            long xid,
            GameWindowExpectation expectation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Interlocked.Increment(ref _locateCount) == _cancelOnLocate)
                _cancellation.Cancel();

            return Task.FromResult(_snapshot);
        }
    }

    private sealed class BlockingGameWindowLocator : IGameWindowLocator
    {
        private readonly GameWindowSnapshot _snapshot;
        private readonly TaskCompletionSource _revalidationStarted;
        private readonly TaskCompletionSource _releaseRevalidation;
        private int _locateCount;

        public BlockingGameWindowLocator(
            GameWindowSnapshot snapshot,
            TaskCompletionSource revalidationStarted,
            TaskCompletionSource releaseRevalidation)
        {
            _snapshot = snapshot;
            _revalidationStarted = revalidationStarted;
            _releaseRevalidation = releaseRevalidation;
        }

        public async Task<GameWindowSnapshot> LocateAsync(
            long xid,
            GameWindowExpectation expectation,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _locateCount) == 2)
            {
                _revalidationStarted.TrySetResult();
                await _releaseRevalidation.Task;
            }

            return _snapshot;
        }
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
