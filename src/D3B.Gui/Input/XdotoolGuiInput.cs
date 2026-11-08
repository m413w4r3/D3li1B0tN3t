using System.Globalization;
using D3B.Gui.Windowing;

namespace D3B.Gui.Input;

/// <summary>Emits global XTest clicks through <c>xdotool</c> after rechecking the active window.</summary>
public sealed class XdotoolGuiInput : IGuiInput
{
    private const int ClientAreaMarginPixels = 2;
    private const int MaximumDetailLength = 512;
    private static readonly TimeSpan _commandTimeout = TimeSpan.FromSeconds(2);

    private readonly IGameWindowLocator _locator;
    private readonly string _binaryPath;
    private readonly Func<string, string?> _environmentReader;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool EmitsRealInput => true;

    /// <summary>Creates XTest input using the default executable path and current process environment.</summary>
    public XdotoolGuiInput(IGameWindowLocator locator, string binaryPath = "/usr/bin/xdotool")
        : this(locator, binaryPath, Environment.GetEnvironmentVariable)
    {
    }

    internal XdotoolGuiInput(
        IGameWindowLocator locator,
        string binaryPath,
        Func<string, string?> environmentReader)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentException.ThrowIfNullOrWhiteSpace(binaryPath);
        ArgumentNullException.ThrowIfNull(environmentReader);

        _locator = locator;
        _binaryPath = binaryPath;
        _environmentReader = environmentReader;
    }

    /// <inheritdoc />
    public async Task<GuiInputResult> ClickAsync(DesktopPixels point, GameWindowSnapshot expectedWindow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedWindow);

        if (cancellationToken.IsCancellationRequested)
        {
            return new GuiInputResult(GuiInputStatus.Canceled);
        }

        if (!Path.IsPathFullyQualified(_binaryPath) || !File.Exists(_binaryPath))
        {
            return new GuiInputResult(GuiInputStatus.Failed, "The configured xdotool executable is unavailable.");
        }

        if (expectedWindow.Identity.Status != GameWindowIdentityStatus.Verified
            || string.IsNullOrWhiteSpace(expectedWindow.Identity.ExecutablePath))
        {
            return new GuiInputResult(GuiInputStatus.WindowChanged, "A verified expected process identity is required for input.");
        }

        string expectedExecutable = Path.GetFileName(expectedWindow.Identity.ExecutablePath);
        GameWindowExpectation expectation = new(expectedExecutable, requireVerifiedProcess: true);
        GameWindowSnapshot current;

        try
        {
            current = await _locator.LocateAsync(expectedWindow.Identity.Xid, expectation, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GuiInputResult(GuiInputStatus.Canceled);
        }
        catch (Exception exception)
        {
            return new GuiInputResult(GuiInputStatus.Failed, SanitizeDetail(exception.Message));
        }

        if (!IdentityMatches(expectedWindow.Identity, current.Identity)
            || !current.Exists
            || !current.IsMapped
            || !current.IsViewable
            || current.IsMinimized
            || !current.GeometryReliable
            || current.DesktopOrigin != expectedWindow.DesktopOrigin
            || current.ClientWidth != expectedWindow.ClientWidth
            || current.ClientHeight != expectedWindow.ClientHeight
            || current.BorderWidth != expectedWindow.BorderWidth
            || current.WindowWidth != expectedWindow.WindowWidth
            || current.WindowHeight != expectedWindow.WindowHeight)
        {
            return new GuiInputResult(GuiInputStatus.WindowChanged, "The expected window identity, mapped state, or geometry changed.");
        }

        if (!current.IsFocused || !current.IsActive)
        {
            return new GuiInputResult(GuiInputStatus.WindowNotActive, "The expected window is not both focused and active.");
        }

        if (!current.IsSessionX11 || !current.HasValidDisplay || !current.IsXTestAvailable)
        {
            return new GuiInputResult(GuiInputStatus.Failed, "The active X11 display or XTest extension is unavailable.");
        }

        if (!IsInsideSafeClientArea(point, current))
        {
            return new GuiInputResult(GuiInputStatus.OutsideWindow, "The desktop point is outside the safe client area.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new GuiInputResult(GuiInputStatus.Canceled);
        }

        IReadOnlyDictionary<string, string?> childEnvironment = CreateChildEnvironment();
        CommandResult moveResult;

        try
        {
            moveResult = await ProcessCommandRunner.RunAsync(
                _binaryPath,
                ["mousemove", "--sync", point.X.ToString(CultureInfo.InvariantCulture), point.Y.ToString(CultureInfo.InvariantCulture)],
                _commandTimeout,
                childEnvironment,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GuiInputResult(GuiInputStatus.Canceled);
        }

        GuiInputResult? moveFailure = GetCommandFailure(moveResult);

        if (moveFailure is not null)
        {
            return moveFailure;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new GuiInputResult(GuiInputStatus.Canceled);
        }

        CommandResult clickResult;

        try
        {
            clickResult = await ProcessCommandRunner.RunAsync(
                _binaryPath,
                ["click", "1"],
                _commandTimeout,
                childEnvironment,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new GuiInputResult(GuiInputStatus.Canceled);
        }

        GuiInputResult? clickFailure = GetCommandFailure(clickResult);
        return clickFailure ?? new GuiInputResult(GuiInputStatus.Sent);
    }

    private static bool IdentityMatches(GameWindowIdentity expected, GameWindowIdentity actual)
    {
        return actual.Xid == expected.Xid
            && actual.ProcessId == expected.ProcessId
            && string.Equals(actual.ExecutablePath, expected.ExecutablePath, StringComparison.Ordinal)
            && actual.Status == GameWindowIdentityStatus.Verified;
    }

    private static bool IsInsideSafeClientArea(DesktopPixels point, GameWindowSnapshot snapshot)
    {
        long left = (long)snapshot.DesktopOrigin.X + snapshot.BorderWidth + ClientAreaMarginPixels;
        long top = (long)snapshot.DesktopOrigin.Y + snapshot.BorderWidth + ClientAreaMarginPixels;
        long right = (long)snapshot.DesktopOrigin.X + snapshot.BorderWidth + snapshot.ClientWidth - ClientAreaMarginPixels;
        long bottom = (long)snapshot.DesktopOrigin.Y + snapshot.BorderWidth + snapshot.ClientHeight - ClientAreaMarginPixels;
        return point.X >= left && point.X < right && point.Y >= top && point.Y < bottom;
    }

    private Dictionary<string, string?> CreateChildEnvironment()
    {
        Dictionary<string, string?> environment = new(StringComparer.Ordinal);
        string? display = _environmentReader("DISPLAY");
        string? home = _environmentReader("HOME");

        if (display is not null)
        {
            environment.Add("DISPLAY", display);
        }

        if (home is not null)
        {
            environment.Add("HOME", home);
        }

        return environment;
    }

    private static GuiInputResult? GetCommandFailure(CommandResult result)
    {
        if (result.TimedOut)
        {
            return new GuiInputResult(GuiInputStatus.TimedOut, SanitizeDetail(result.Failure ?? result.StandardError));
        }

        if (!result.Succeeded)
        {
            return new GuiInputResult(GuiInputStatus.Failed, SanitizeDetail(result.Failure ?? result.StandardError));
        }

        return null;
    }

    private static string? SanitizeDetail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string detail = ProcessCommandRunner.Sanitize(value, environment: null);

        if (detail.Length > MaximumDetailLength)
        {
            detail = detail[..MaximumDetailLength];
        }

        return detail;
    }
}
