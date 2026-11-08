using System.Diagnostics;
using System.Globalization;
using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Windowing;

/// <summary>Owns an isolated Xvfb server and one or more fake xterm windows for backend tests.</summary>
internal sealed class XvfbTestDisplay : IAsyncDisposable
{
    private static int _nextDisplayNumber = 100;

    private readonly List<Process> _windowProcesses = [];
    private readonly Process _serverProcess;
    private readonly string _displayName;
    private readonly string _title;

    /// <summary>The display number assigned to this isolated Xvfb server.</summary>
    public string DisplayName
    {
        get
        {
            return _displayName;
        }
    }

    /// <summary>The XID of the primary fake xterm window.</summary>
    public long WindowId { get; private set; }

    private XvfbTestDisplay(Process serverProcess, string displayName, string title)
    {
        _serverProcess = serverProcess;
        _displayName = displayName;
        _title = title;
    }

    /// <summary>Starts Xvfb and waits for a uniquely titled xterm window.</summary>
    public static async Task<XvfbTestDisplay> StartAsync()
    {
        string[] requiredCommands = ["Xvfb", "xterm", "xdpyinfo", "xwininfo", "xprop", "xdotool", "sleep"];
        List<string> missingCommands = [];

        foreach (string command in requiredCommands)
        {
            if (ResolveExecutable(command) is null)
            {
                missingCommands.Add(command);
            }
        }

        if (missingCommands.Count > 0)
        {
            Assert.Skip($"X11 tests require these installed commands: {string.Join(", ", missingCommands)}.");
        }

        string title = $"D3B-Gui-{Guid.NewGuid():N}";

        for (int attempt = 0; attempt < 100; attempt++)
        {
            int displayNumber = Interlocked.Increment(ref _nextDisplayNumber);
            string displayName = $":{displayNumber}";

            if (File.Exists($"/tmp/.X11-unix/X{displayNumber}") || File.Exists($"/tmp/.X{displayNumber}-lock"))
            {
                continue;
            }

            Process? server = StartLongRunningProcess(
                ResolveExecutable("Xvfb")!,
                [displayName, "-screen", "0", "1024x768x24", "-nolisten", "tcp", "-ac"],
                environment: null);

            if (server is null)
            {
                continue;
            }

            XvfbTestDisplay display = new(server, displayName, title);

            try
            {
                await display.WaitForServerAsync().ConfigureAwait(false);
                await display.StartWindowAsync(title).ConfigureAwait(false);
                return display;
            }
            catch
            {
                bool serverExited = server.HasExited;
                await display.DisposeAsync().ConfigureAwait(false);

                if (serverExited)
                {
                    continue;
                }

                throw;
            }
        }

        throw new InvalidOperationException("Could not start an Xvfb server on a free display number.");
    }

    /// <summary>Starts a second fake xterm window on this display.</summary>
    public async Task<long> StartAdditionalWindowAsync()
    {
        string title = $"{_title}-other-{_windowProcesses.Count}";
        return await StartWindowAsync(title).ConfigureAwait(false);
    }

    /// <summary>Moves and resizes the primary window using xdotool against this Xvfb display.</summary>
    public async Task MoveAndResizeAsync(int x, int y, int width, int height)
    {
        await RunToolAsync("xdotool", ["windowmove", WindowId.ToString(CultureInfo.InvariantCulture), x.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture)]).ConfigureAwait(false);
        await RunToolAsync("xdotool", ["windowsize", WindowId.ToString(CultureInfo.InvariantCulture), width.ToString(CultureInfo.InvariantCulture), height.ToString(CultureInfo.InvariantCulture)]).ConfigureAwait(false);
    }

    /// <summary>Unmaps the primary fake window.</summary>
    public async Task UnmapWindowAsync()
    {
        await RunToolAsync("xdotool", ["windowunmap", WindowId.ToString(CultureInfo.InvariantCulture)]).ConfigureAwait(false);
    }

    /// <summary>Removes the PID property from the primary fake window.</summary>
    public async Task RemoveProcessIdPropertyAsync()
    {
        await RunToolAsync("xprop", ["-id", WindowId.ToString(CultureInfo.InvariantCulture), "-remove", "_NET_WM_PID"]).ConfigureAwait(false);
    }

    /// <summary>Sets keyboard focus to another fake window on this test display.</summary>
    public async Task FocusOtherWindowAsync()
    {
        long otherWindowId = await StartAdditionalWindowAsync().ConfigureAwait(false);
        await RunToolAsync("xdotool", ["windowfocus", "--sync", otherWindowId.ToString(CultureInfo.InvariantCulture)]).ConfigureAwait(false);
    }

    /// <summary>Terminates the process that owns the primary window.</summary>
    public async Task KillPrimaryWindowAsync()
    {
        Process process = _windowProcesses[0];

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Creates a locator environment reader that points only at this Xvfb display.</summary>
    public Func<string, string?> CreateEnvironmentReader(string? sessionType = "x11", string? path = null)
    {
        return name =>
        {
            if (string.Equals(name, "XDG_SESSION_TYPE", StringComparison.Ordinal))
            {
                return sessionType;
            }

            if (string.Equals(name, "DISPLAY", StringComparison.Ordinal))
            {
                return _displayName;
            }

            if (string.Equals(name, "PATH", StringComparison.Ordinal) && path is not null)
            {
                return path;
            }

            return Environment.GetEnvironmentVariable(name);
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (Process process in _windowProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        try
        {
            if (!_serverProcess.HasExited)
            {
                _serverProcess.Kill(entireProcessTree: true);
                await _serverProcess.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        finally
        {
            _serverProcess.Dispose();
        }
    }

    private async Task WaitForServerAsync()
    {
        string executable = ResolveExecutable("xdpyinfo")!;
        IReadOnlyDictionary<string, string?> environment = new Dictionary<string, string?> { ["DISPLAY"] = _displayName };
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(8);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_serverProcess.HasExited)
            {
                throw new InvalidOperationException("Xvfb exited before its display became available.");
            }

            CommandResult result = await ProcessCommandRunner.RunAsync(
                executable,
                ["-queryExtensions"],
                TimeSpan.FromMilliseconds(500),
                environment,
                CancellationToken.None).ConfigureAwait(false);

            if (result.Succeeded)
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException($"Xvfb display {_displayName} did not answer xdpyinfo within eight seconds.");
    }

    private async Task<long> StartWindowAsync(string title)
    {
        Process process = StartLongRunningProcess(
            ResolveExecutable("xterm")!,
            ["-T", title, "-geometry", "80x24+50+60", "-e", ResolveExecutable("sleep")!, "300"],
            new Dictionary<string, string?> { ["DISPLAY"] = _displayName })
            ?? throw new InvalidOperationException("xterm did not start.");

        _windowProcesses.Add(process);
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(8);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException("xterm exited before its X11 window appeared.");
            }

            CommandResult result = await RunToolAsync("xdotool", ["search", "--onlyvisible", "--name", title], allowNonZeroExit: true).ConfigureAwait(false);

            if (result.Succeeded)
            {
                string firstId = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;

                if (long.TryParse(firstId, NumberStyles.None, CultureInfo.InvariantCulture, out long windowId))
                {
                    if (_windowProcesses.Count == 1)
                    {
                        WindowId = windowId;
                    }

                    return windowId;
                }
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException("xterm did not create a visible window within eight seconds.");
    }

    private async Task<CommandResult> RunToolAsync(string command, IReadOnlyList<string> arguments, bool allowNonZeroExit = false)
    {
        string executable = ResolveExecutable(command) ?? throw new InvalidOperationException($"The test dependency '{command}' is not installed.");
        IReadOnlyDictionary<string, string?> environment = new Dictionary<string, string?> { ["DISPLAY"] = _displayName };
        CommandResult result = await ProcessCommandRunner.RunAsync(
            executable,
            arguments,
            TimeSpan.FromSeconds(2),
            environment,
            CancellationToken.None).ConfigureAwait(false);

        if (!allowNonZeroExit && !result.Succeeded)
        {
            throw new InvalidOperationException($"The X11 test command '{command}' failed: {result.Failure ?? result.StandardError}");
        }

        return result;
    }

    private static Process? StartLongRunningProcess(
        string executablePath,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (KeyValuePair<string, string?> entry in environment)
            {
                startInfo.Environment[entry.Key] = entry.Value;
            }
        }

        try
        {
            Process? process = Process.Start(startInfo);

            if (process is not null)
            {
                _ = process.StandardOutput.ReadToEndAsync();
                _ = process.StandardError.ReadToEndAsync();
            }

            return process;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string? ResolveExecutable(string command)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string executable = Path.GetFullPath(Path.Combine(directory, command));

            if (File.Exists(executable))
            {
                return executable;
            }
        }

        return null;
    }
}
