using System.Globalization;
using System.Text.RegularExpressions;

namespace D3B.Gui.Windowing;

/// <summary>Reads identity, state, and geometry for one caller-supplied X11 window identifier.</summary>
/// <remarks>The locator never searches windows by title and never sends input to a window.</remarks>
public sealed class X11WindowLocator : IGameWindowLocator
{
    private const int CommandTimeoutMilliseconds = 2000;
    private static readonly Regex _processIdPattern = new("_NET_WM_PID\\(CARDINAL\\)\\s*=\\s*(\\d+)", RegexOptions.CultureInvariant);

    private readonly Func<string, string?> _environmentReader;
    private readonly Func<
        string,
        IReadOnlyList<string>,
        TimeSpan,
        IReadOnlyDictionary<string, string?>?,
        CancellationToken,
        Task<CommandResult>> _commandRunner;

    /// <summary>Creates a locator using the current process environment and the bounded process runner.</summary>
    public X11WindowLocator()
        : this(Environment.GetEnvironmentVariable, ProcessCommandRunner.RunAsync)
    {
    }

    internal X11WindowLocator(
        Func<string, string?> environmentReader,
        Func<
            string,
            IReadOnlyList<string>,
            TimeSpan,
            IReadOnlyDictionary<string, string?>?,
            CancellationToken,
            Task<CommandResult>> commandRunner)
    {
        ArgumentNullException.ThrowIfNull(environmentReader);
        ArgumentNullException.ThrowIfNull(commandRunner);

        _environmentReader = environmentReader;
        _commandRunner = commandRunner;
    }

    /// <inheritdoc />
    public async Task<GameWindowSnapshot> LocateAsync(
        long xid,
        GameWindowExpectation expectation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectation);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
        bool isSessionX11 = string.Equals(_environmentReader("XDG_SESSION_TYPE"), "x11", StringComparison.OrdinalIgnoreCase);

        if (!isSessionX11)
        {
            return CreateFailureSnapshot(
                xid,
                capturedAt,
                isSessionX11,
                hasValidDisplay: false,
                isXTestAvailable: false,
                "XDG_SESSION_TYPE must be x11.");
        }

        string? display = _environmentReader("DISPLAY");

        if (string.IsNullOrWhiteSpace(display))
        {
            return CreateFailureSnapshot(
                xid,
                capturedAt,
                isSessionX11,
                hasValidDisplay: false,
                isXTestAvailable: false,
                "DISPLAY is empty.");
        }

        IReadOnlyDictionary<string, string?> childEnvironment = new Dictionary<string, string?>
        {
            ["DISPLAY"] = display
        };

        CommandResult displayResult = await RunCommandAsync(
            "xdpyinfo",
            ["-queryExtensions"],
            childEnvironment,
            cancellationToken).ConfigureAwait(false);

        if (!displayResult.Succeeded)
        {
            return CreateFailureSnapshot(
                xid,
                capturedAt,
                isSessionX11,
                hasValidDisplay: false,
                isXTestAvailable: false,
                DescribeFailure("xdpyinfo", displayResult));
        }

        bool isXTestAvailable = displayResult.StandardOutput.Contains("XTEST", StringComparison.Ordinal);

        if (xid <= 0)
        {
            return CreateNotFoundSnapshot(
                xid,
                capturedAt,
                isXTestAvailable,
                isSessionX11,
                "The XID must be a positive number.");
        }

        string xidArgument = xid.ToString(CultureInfo.InvariantCulture);
        List<string> failures = [];

        CommandResult windowInfoResult = await RunCommandAsync(
            "xwininfo",
            ["-id", xidArgument],
            childEnvironment,
            cancellationToken).ConfigureAwait(false);

        if (!windowInfoResult.Succeeded)
        {
            if (windowInfoResult.Failure is not null || windowInfoResult.TimedOut || windowInfoResult.ExitCode is null)
            {
                return CreateFailureSnapshot(
                    xid,
                    capturedAt,
                    isSessionX11,
                    hasValidDisplay: true,
                    isXTestAvailable,
                    DescribeFailure("xwininfo", windowInfoResult));
            }

            return CreateNotFoundSnapshot(
                xid,
                capturedAt,
                isXTestAvailable,
                isSessionX11,
                $"xwininfo could not find XID {xidArgument}.");
        }

        bool hasWindowInfo = TryReadWindowInfo(
            windowInfoResult.StandardOutput,
            out (int X, int Y, int Width, int Height, int BorderWidth, string MapState) windowInfo);

        if (!hasWindowInfo)
        {
            failures.Add("xwininfo returned incomplete geometry or map state.");
        }

        CommandResult propertiesResult = await RunCommandAsync(
            "xprop",
            ["-id", xidArgument, "_NET_WM_PID", "_NET_WM_NAME", "WM_NAME", "WM_STATE", "_NET_WM_STATE"],
            childEnvironment,
            cancellationToken).ConfigureAwait(false);

        string properties = propertiesResult.Succeeded ? propertiesResult.StandardOutput : string.Empty;

        if (!propertiesResult.Succeeded && (propertiesResult.Failure is not null || propertiesResult.TimedOut || propertiesResult.ExitCode is null))
        {
            failures.Add(DescribeFailure("xprop", propertiesResult));
        }

        (int? processId, string? executablePath, GameWindowIdentityStatus identityStatus) = ReadIdentity(
            properties,
            expectation.ExpectedExecutableName);

        GameWindowIdentity identity = new(xid, processId, executablePath, ReadTitle(properties), identityStatus);

        if (expectation.RequireVerifiedProcess && identityStatus != GameWindowIdentityStatus.Verified)
        {
            failures.Add("A verified process identity was required, but the process could not be verified.");
        }

        CommandResult geometryResult = await RunCommandAsync(
            "xdotool",
            ["getwindowgeometry", "--shell", xidArgument],
            childEnvironment,
            cancellationToken).ConfigureAwait(false);

        (int X, int Y, int Width, int Height) xdotoolGeometry = default;
        bool hasXdotoolGeometry = geometryResult.Succeeded && TryReadXdotoolGeometry(geometryResult.StandardOutput, out xdotoolGeometry);

        if (!hasXdotoolGeometry)
        {
            xdotoolGeometry = default;
            failures.Add(DescribeFailure("xdotool getwindowgeometry", geometryResult));
        }

        bool isFocused = false;
        CommandResult focusResult = await RunCommandAsync(
            "xdotool",
            ["getwindowfocus"],
            childEnvironment,
            cancellationToken).ConfigureAwait(false);

        if (focusResult.Succeeded && TryParseXid(focusResult.StandardOutput.Trim(), out long focusedXid))
        {
            isFocused = focusedXid == xid;
        }
        else
        {
            failures.Add(DescribeFailure("xdotool getwindowfocus", focusResult));
        }

        bool isActive;
        CommandResult activeResult = await RunCommandAsync(
            "xprop",
            ["-root", "_NET_ACTIVE_WINDOW"],
            childEnvironment,
            cancellationToken).ConfigureAwait(false);

        if (activeResult.Succeeded && TryReadActiveWindow(activeResult.StandardOutput, out long activeXid))
        {
            isActive = activeXid == xid;
        }
        else
        {
            isActive = isFocused;

            if (activeResult.Failure is not null || activeResult.TimedOut || activeResult.ExitCode is null)
            {
                failures.Add(DescribeFailure("xprop -root _NET_ACTIVE_WINDOW", activeResult));
            }
        }

        bool isMapped = hasWindowInfo && !string.Equals(windowInfo.MapState, "IsUnMapped", StringComparison.Ordinal);
        bool isViewable = hasWindowInfo && string.Equals(windowInfo.MapState, "IsViewable", StringComparison.Ordinal);
        bool isMinimized = IsMinimized(properties);
        int clientWidth = hasWindowInfo ? windowInfo.Width : 0;
        int clientHeight = hasWindowInfo ? windowInfo.Height : 0;
        int borderWidth = hasWindowInfo ? windowInfo.BorderWidth : 0;
        int windowWidth = CalculateWindowExtent(clientWidth, borderWidth, failures, "width");
        int windowHeight = CalculateWindowExtent(clientHeight, borderWidth, failures, "height");
        DesktopPixels desktopOrigin = hasWindowInfo ? new DesktopPixels(windowInfo.X, windowInfo.Y) : default;
        bool geometryReliable = hasWindowInfo
            && hasXdotoolGeometry
            && clientWidth > 0
            && clientHeight > 0
            && borderWidth >= 0
            && windowInfo.X == xdotoolGeometry.X
            && windowInfo.Y == xdotoolGeometry.Y
            && clientWidth == xdotoolGeometry.Width
            && clientHeight == xdotoolGeometry.Height
            && windowWidth > 0
            && windowHeight > 0;

        if (hasWindowInfo && hasXdotoolGeometry && !geometryReliable)
        {
            failures.Add("xwininfo and xdotool geometry did not agree or dimensions were empty.");
        }

        return new GameWindowSnapshot(
            identity,
            Exists: true,
            isMapped,
            isViewable,
            isMinimized,
            isFocused,
            isActive,
            isXTestAvailable,
            isSessionX11,
            HasValidDisplay: true,
            geometryReliable,
            desktopOrigin,
            windowWidth,
            windowHeight,
            borderWidth,
            clientWidth,
            clientHeight,
            failures.Count == 0 ? null : string.Join(" ", failures),
            capturedAt);
    }

    private async Task<CommandResult> RunCommandAsync(
        string command,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken)
    {
        string? executablePath = ResolveExecutable(command);

        if (executablePath is null)
        {
            return new CommandResult(null, false, string.Empty, string.Empty, $"Required command '{command}' was not found on PATH.");
        }

        try
        {
            return await _commandRunner(
                executablePath,
                arguments,
                TimeSpan.FromMilliseconds(CommandTimeoutMilliseconds),
                environment,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new CommandResult(
                null,
                false,
                string.Empty,
                string.Empty,
                ProcessCommandRunner.Sanitize(exception.Message, environment));
        }
    }

    private string? ResolveExecutable(string command)
    {
        string? path = _environmentReader("PATH");

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string executablePath = Path.GetFullPath(Path.Combine(directory, command));

                if (File.Exists(executablePath))
                {
                    return executablePath;
                }
            }
            catch (ArgumentException)
            {
            }
            catch (IOException)
            {
            }
        }

        return null;
    }

    private static GameWindowSnapshot CreateFailureSnapshot(
        long xid,
        DateTimeOffset capturedAt,
        bool isSessionX11,
        bool hasValidDisplay,
        bool isXTestAvailable,
        string failure)
    {
        return new GameWindowSnapshot(
            new GameWindowIdentity(xid, null, null, null, GameWindowIdentityStatus.UnverifiedProcessIdUnavailable),
            Exists: false,
            IsMapped: false,
            IsViewable: false,
            IsMinimized: false,
            IsFocused: false,
            IsActive: false,
            isXTestAvailable,
            isSessionX11,
            hasValidDisplay,
            GeometryReliable: false,
            DesktopOrigin: default,
            WindowWidth: 0,
            WindowHeight: 0,
            BorderWidth: 0,
            ClientWidth: 0,
            ClientHeight: 0,
            failure,
            capturedAt);
    }

    private static GameWindowSnapshot CreateNotFoundSnapshot(
        long xid,
        DateTimeOffset capturedAt,
        bool isXTestAvailable,
        bool isSessionX11,
        string failure)
    {
        return new GameWindowSnapshot(
            new GameWindowIdentity(xid, null, null, null, GameWindowIdentityStatus.NotFound),
            Exists: false,
            IsMapped: false,
            IsViewable: false,
            IsMinimized: false,
            IsFocused: false,
            IsActive: false,
            isXTestAvailable,
            isSessionX11,
            HasValidDisplay: true,
            GeometryReliable: false,
            DesktopOrigin: default,
            WindowWidth: 0,
            WindowHeight: 0,
            BorderWidth: 0,
            ClientWidth: 0,
            ClientHeight: 0,
            failure,
            capturedAt);
    }

    private static (int? ProcessId, string? ExecutablePath, GameWindowIdentityStatus Status) ReadIdentity(
        string properties,
        string expectedExecutableName)
    {
        Match match = _processIdPattern.Match(properties);

        if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int processId) || processId <= 0)
        {
            return (null, null, GameWindowIdentityStatus.UnverifiedProcessIdUnavailable);
        }

        string procExecutable = $"/proc/{processId}/exe";

        try
        {
            FileSystemInfo? target = new FileInfo(procExecutable).ResolveLinkTarget(returnFinalTarget: true);

            if (target is null || string.IsNullOrWhiteSpace(target.FullName))
            {
                return (processId, null, GameWindowIdentityStatus.UnverifiedExecutableUnreadable);
            }

            string executablePath = target.FullName;
            string executableName = Path.GetFileName(executablePath);

            if (!string.Equals(executableName, expectedExecutableName, StringComparison.Ordinal))
            {
                return (processId, executablePath, GameWindowIdentityStatus.UnverifiedExecutableMismatch);
            }

            return (processId, executablePath, GameWindowIdentityStatus.Verified);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return (processId, null, GameWindowIdentityStatus.UnverifiedExecutableUnreadable);
        }
    }

    private static string? ReadTitle(string properties)
    {
        foreach (string line in properties.Split('\n'))
        {
            string trimmed = line.TrimStart();

            if (!trimmed.StartsWith("_NET_WM_NAME(", StringComparison.Ordinal)
                && !trimmed.StartsWith("WM_NAME(", StringComparison.Ordinal))
            {
                continue;
            }

            int firstQuote = trimmed.IndexOf('"');
            int lastQuote = trimmed.LastIndexOf('"');

            if (firstQuote >= 0 && lastQuote > firstQuote)
            {
                return trimmed[(firstQuote + 1)..lastQuote]
                    .Replace("\\\"", "\"", StringComparison.Ordinal)
                    .Replace("\\\\", "\\", StringComparison.Ordinal);
            }
        }

        return null;
    }

    private static bool IsMinimized(string properties)
    {
        foreach (string line in properties.Split('\n'))
        {
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("WM_STATE(", StringComparison.Ordinal)
                && trimmed.Contains("Iconic", StringComparison.Ordinal))
            {
                return true;
            }

            if (trimmed.StartsWith("_NET_WM_STATE(", StringComparison.Ordinal)
                && trimmed.Contains("_NET_WM_STATE_HIDDEN", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadWindowInfo(
        string output,
        out (int X, int Y, int Width, int Height, int BorderWidth, string MapState) windowInfo)
    {
        bool hasX = TryReadIntegerField(output, "Absolute upper-left X", out int x);
        bool hasY = TryReadIntegerField(output, "Absolute upper-left Y", out int y);
        bool hasWidth = TryReadIntegerField(output, "Width", out int width);
        bool hasHeight = TryReadIntegerField(output, "Height", out int height);
        bool hasBorder = TryReadIntegerField(output, "Border width", out int borderWidth);
        bool hasMapState = TryReadTextField(output, "Map State", out string mapState);

        windowInfo = (x, y, width, height, borderWidth, mapState);

        return hasX && hasY && hasWidth && hasHeight && hasBorder && hasMapState;
    }

    private static bool TryReadXdotoolGeometry(string output, out (int X, int Y, int Width, int Height) geometry)
    {
        bool hasX = TryReadShellInteger(output, "X", out int x);
        bool hasY = TryReadShellInteger(output, "Y", out int y);
        bool hasWidth = TryReadShellInteger(output, "WIDTH", out int width);
        bool hasHeight = TryReadShellInteger(output, "HEIGHT", out int height);

        geometry = (x, y, width, height);

        return hasX && hasY && hasWidth && hasHeight;
    }

    private static bool TryReadIntegerField(string output, string name, out int value)
    {
        if (TryReadTextField(output, name, out string text)
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryReadTextField(string output, string name, out string value)
    {
        foreach (string line in output.Split('\n'))
        {
            string trimmed = line.Trim();
            string prefix = name + ":";

            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                value = trimmed[prefix.Length..].Trim();
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadShellInteger(string output, string name, out int value)
    {
        foreach (string line in output.Split('\n'))
        {
            string trimmed = line.Trim();
            string prefix = name + "=";

            if (trimmed.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(trimmed[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryReadActiveWindow(string output, out long xid)
    {
        foreach (string line in output.Split('\n'))
        {
            if (line.TrimStart().StartsWith("_NET_ACTIVE_WINDOW(", StringComparison.Ordinal))
            {
                int marker = line.LastIndexOf('#');
                string value = marker >= 0 ? line[(marker + 1)..].Trim() : line[(line.IndexOf('=') + 1)..].Trim();
                return TryParseXid(value, out xid);
            }
        }

        xid = 0;
        return false;
    }

    private static bool TryParseXid(string value, out long xid)
    {
        string trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out xid))
        {
            return true;
        }

        return long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out xid);
    }

    private static int CalculateWindowExtent(int clientExtent, int borderWidth, List<string> failures, string dimension)
    {
        if (clientExtent <= 0 || borderWidth < 0)
        {
            return 0;
        }

        try
        {
            return checked(clientExtent + (borderWidth * 2));
        }
        catch (OverflowException)
        {
            failures.Add($"The measured window {dimension} exceeded the supported integer range.");
            return 0;
        }
    }

    private static string DescribeFailure(string command, CommandResult result)
    {
        string detail;

        if (result.TimedOut)
        {
            detail = "timed out";
        }
        else if (result.Failure is not null)
        {
            detail = ProcessCommandRunner.Sanitize(result.Failure, environment: null);
        }
        else if (result.ExitCode is int exitCode)
        {
            detail = $"exited with code {exitCode}";
        }
        else
        {
            detail = "did not complete";
        }

        string standardError = ProcessCommandRunner.Sanitize(result.StandardError, environment: null);

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            detail = $"{detail}: {standardError}";
        }

        return $"{command} {detail}.";
    }
}
