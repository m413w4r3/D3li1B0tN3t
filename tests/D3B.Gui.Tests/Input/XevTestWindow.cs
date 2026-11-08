using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using D3B.Gui.Tests.Windowing;
using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Input;

internal sealed class XevTestWindow : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Channel<string> _output = Channel.CreateUnbounded<string>();
    private readonly Task _outputReader;

    public long WindowId { get; }

    private XevTestWindow(Process process, long windowId)
    {
        _process = process;
        WindowId = windowId;
        _outputReader = ReadOutputAsync();
        _ = _process.StandardError.ReadToEndAsync();
    }

    public static async Task<XevTestWindow> StartAsync(XvfbTestDisplay display)
    {
        string? xev = XvfbTestCommands.ResolveExecutable("xev");
        string? stdbuf = XvfbTestCommands.ResolveExecutable("stdbuf");

        if (xev is null)
        {
            Assert.Skip("XTest input tests require the installed 'xev' executable.");
        }

        if (stdbuf is null)
        {
            Assert.Skip("XTest input tests require the installed 'stdbuf' executable to flush xev events.");
        }

        string title = $"D3B-Xev-{Guid.NewGuid():N}";

        ProcessStartInfo startInfo = new()
        {
            FileName = stdbuf,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-oL");
        startInfo.ArgumentList.Add(xev);
        startInfo.ArgumentList.Add("-event");
        startInfo.ArgumentList.Add("button");
        startInfo.ArgumentList.Add("-geometry");
        startInfo.ArgumentList.Add("240x160+180+120");
        startInfo.ArgumentList.Add("-name");
        startInfo.ArgumentList.Add(title);
        startInfo.Environment["DISPLAY"] = display.DisplayName;
        Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("xev did not start.");
        XevTestWindow? window = null;

        try
        {
            long windowId = await FindWindowIdAsync(process, title, display, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            window = new XevTestWindow(process, windowId);
            string xprop = XvfbTestCommands.ResolveExecutable("xprop") ?? throw new InvalidOperationException("xprop is required by X11WindowLocator.");
            CommandResult propertyResult = await XvfbTestCommands.RunAsync(
                xprop,
                ["-id", window.WindowId.ToString(CultureInfo.InvariantCulture), "-f", "_NET_WM_PID", "32c", "-set", "_NET_WM_PID", process.Id.ToString(CultureInfo.InvariantCulture)],
                display).ConfigureAwait(false);
            XvfbTestCommands.AssertCommandSucceeded(propertyResult, "xprop _NET_WM_PID");

            string xdotool = XvfbTestCommands.ResolveExecutable("xdotool") ?? throw new InvalidOperationException("xdotool is required by XTest input tests.");
            CommandResult focusResult = await XvfbTestCommands.RunAsync(
                xdotool,
                ["windowfocus", "--sync", window.WindowId.ToString(CultureInfo.InvariantCulture)],
                display).ConfigureAwait(false);
            XvfbTestCommands.AssertCommandSucceeded(focusResult, "xdotool windowfocus");
            return window;
        }
        catch
        {
            if (window is not null)
            {
                await window.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
                process.Dispose();
            }

            throw;
        }
    }

    public async Task<bool> HasButtonPressAsync(TimeSpan timeout)
    {
        using CancellationTokenSource timeoutSource = new(timeout);

        try
        {
            while (true)
            {
                string line = await _output.Reader.ReadAsync(timeoutSource.Token).ConfigureAwait(false);

                if (line.StartsWith("ButtonPress event", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().ConfigureAwait(false);
        }

        _process.Dispose();
        try
        {
            await _outputReader.ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    private async Task ReadOutputAsync()
    {
        while (true)
        {
            string? line = await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false);

            if (line is null)
            {
                return;
            }

            await _output.Writer.WriteAsync(line).ConfigureAwait(false);
        }
    }

    private static async Task<long> FindWindowIdAsync(Process process, string title, XvfbTestDisplay display, TimeSpan timeout)
    {
        string xdotool = XvfbTestCommands.ResolveExecutable("xdotool") ?? throw new InvalidOperationException("xdotool is required by XTest input tests.");
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException("xev exited before creating its window.");
            }

            CommandResult result = await XvfbTestCommands.RunAsync(
                xdotool,
                ["search", "--onlyvisible", "--name", title],
                display).ConfigureAwait(false);

            if (result.Succeeded)
            {
                string firstLine = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;

                if (long.TryParse(firstLine, NumberStyles.None, CultureInfo.InvariantCulture, out long windowId))
                {
                    return windowId;
                }
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        throw new TimeoutException("xev did not create a visible X11 window within five seconds.");
    }
}
