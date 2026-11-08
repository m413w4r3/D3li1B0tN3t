using System.Diagnostics;
using System.Globalization;
using System.Text;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Capture;

/// <summary>Captures an explicitly selected X11 window through bounded binary stdout from <c>maim</c>.</summary>
public sealed class MaimWindowCapture : IWindowCapture
{
    private const int MaximumStandardErrorBytes = 64 * 1024;
    private const int MaximumFailureLength = 512;

    private readonly IGameWindowLocator _locator;
    private readonly MaimCaptureOptions _options;
    private readonly Func<string, string?> _environmentReader;

    /// <summary>Creates a capture backend with the default <c>maim</c> path and current process environment.</summary>
    public MaimWindowCapture(IGameWindowLocator locator, MaimCaptureOptions? options = null)
        : this(locator, options ?? new MaimCaptureOptions(), Environment.GetEnvironmentVariable)
    {
    }

    internal MaimWindowCapture(
        IGameWindowLocator locator,
        MaimCaptureOptions options,
        Func<string, string?> environmentReader)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environmentReader);

        _locator = locator;
        _options = options;
        _environmentReader = environmentReader;
    }

    /// <inheritdoc />
    public async Task<CapturedFrame> CaptureAsync(long xid, GameWindowExpectation expectation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectation);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        GameWindowSnapshot before;

        try
        {
            before = await _locator.LocateAsync(xid, expectation, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            before = CreateUnavailableSnapshot(xid, exception.Message);
            return CreateFailureFrame(startedAt, before, before, CaptureStatus.Failed, SanitizeFailure(exception.Message));
        }

        CaptureStatus? preflightStatus = GetPreflightFailure(xid, expectation, before);

        if (preflightStatus is not null)
        {
            return CreateFailureFrame(startedAt, before, before, preflightStatus.Value, DescribeSnapshotFailure(before));
        }

        if (before.WindowWidth > _options.MaxWidth || before.WindowHeight > _options.MaxHeight)
        {
            return CreateFailureFrame(
                startedAt,
                before,
                before,
                CaptureStatus.InvalidDimensions,
                $"The measured X11 window extent is {before.WindowWidth}x{before.WindowHeight}, above the configured limit.");
        }

        BinaryProcessResult processResult = await RunMaimAsync(xid, cancellationToken).ConfigureAwait(false);
        GameWindowSnapshot after = await LocateAfterAsync(xid, expectation, before, cancellationToken).ConfigureAwait(false);

        if (!SnapshotsMatch(before, after))
        {
            return CreateFailureFrame(startedAt, before, after, CaptureStatus.WindowChanged, "The window identity, geometry, or mapped state changed during capture.");
        }

        if (processResult.Status is not null)
        {
            return CreateFailureFrame(startedAt, before, after, processResult.Status.Value, processResult.Failure);
        }

        if (processResult.StandardOutput is not { Length: > 0 } png)
        {
            return CreateFailureFrame(startedAt, before, after, CaptureStatus.DecodeFailed, "The maim process returned no PNG bytes.");
        }

        Mat? image = null;

        try
        {
            image = Cv2.ImDecode(png, ImreadModes.Color);

            if (image.Empty())
            {
                image.Dispose();
                image = null;
                return CreateFailureFrame(startedAt, before, after, CaptureStatus.DecodeFailed, "OpenCV could not decode the PNG output.");
            }

            int width = image.Cols;
            int height = image.Rows;
            int widthDifference = width - before.WindowWidth;
            int heightDifference = height - before.WindowHeight;

            if (width <= 0 || height <= 0 || width > _options.MaxWidth || height > _options.MaxHeight)
            {
                image.Dispose();
                image = null;
                return CreateFailureFrame(
                    startedAt,
                    before,
                    after,
                    CaptureStatus.InvalidDimensions,
                    $"Decoded image {width}x{height} exceeds the allowed dimensions or is empty.",
                    width,
                    height,
                    widthDifference,
                    heightDifference);
            }

            if (widthDifference != 0 || heightDifference != 0)
            {
                image.Dispose();
                image = null;
                return CreateFailureFrame(
                    startedAt,
                    before,
                    after,
                    CaptureStatus.InvalidDimensions,
                    $"Decoded image {width}x{height} differs from measured X11 extent {before.WindowWidth}x{before.WindowHeight} including borders by {widthDifference:+#;-#;0}x{heightDifference:+#;-#;0} pixels.",
                    width,
                    height,
                    widthDifference,
                    heightDifference);
            }

            if (HasEmptyContent(image))
            {
                image.Dispose();
                image = null;
                return CreateFailureFrame(
                    startedAt,
                    before,
                    after,
                    CaptureStatus.EmptyContent,
                    "The decoded image is constant or at least 99.5% near-black.",
                    width,
                    height,
                    widthDifference,
                    heightDifference);
            }

            CapturedFrame frame = new(
                Guid.NewGuid(),
                before.Identity,
                startedAt,
                DateTimeOffset.UtcNow,
                before,
                after,
                width,
                height,
                image,
                CaptureStatus.Captured,
                null,
                contentUsable: true,
                widthDifference,
                heightDifference);
            image = null;
            return frame;
        }
        catch (Exception exception) when (exception is OpenCVException or ArgumentException or TypeInitializationException or DllNotFoundException or BadImageFormatException)
        {
            image?.Dispose();
            return CreateFailureFrame(startedAt, before, after, CaptureStatus.DecodeFailed, SanitizeFailure(exception.Message));
        }
        finally
        {
            image?.Dispose();
        }
    }

    private async Task<BinaryProcessResult> RunMaimAsync(long xid, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(_options.BinaryPath) || !File.Exists(_options.BinaryPath))
        {
            return new BinaryProcessResult(CaptureStatus.BinaryUnavailable, null, "The configured maim executable is unavailable.");
        }

        Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = _options.BinaryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("-u");
        process.StartInfo.ArgumentList.Add("-f");
        process.StartInfo.ArgumentList.Add("png");
        process.StartInfo.ArgumentList.Add("-i");
        process.StartInfo.ArgumentList.Add(xid.ToString(CultureInfo.InvariantCulture));

        IReadOnlyDictionary<string, string?> childEnvironment = CreateChildEnvironment();

        foreach (KeyValuePair<string, string?> value in childEnvironment)
        {
            process.StartInfo.Environment[value.Key] = value.Value;
        }

        try
        {
            if (!process.Start())
            {
                return new BinaryProcessResult(CaptureStatus.BinaryUnavailable, null, "The maim process did not start.");
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return new BinaryProcessResult(CaptureStatus.BinaryUnavailable, null, SanitizeFailure(exception.Message, childEnvironment));
        }

        using (process)
        using (CancellationTokenSource timeoutSource = new(_options.Timeout))
        using (CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
        {
            Task<BinaryReadResult> outputTask = ReadBinaryOutputAsync(
                process.StandardOutput.BaseStream,
                _options.MaxCaptureBytes,
                process,
                linkedSource.Token);
            Task<string> errorTask = ReadStandardErrorAsync(process.StandardError.BaseStream, childEnvironment, linkedSource.Token);
            Task exitTask = process.WaitForExitAsync(linkedSource.Token);
            Task completionTask = Task.WhenAll(outputTask, errorTask, exitTask);
            ObserveFailure(completionTask);
            Task cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, linkedSource.Token);

            try
            {
                Task completedTask = await Task.WhenAny(completionTask, cancellationTask).ConfigureAwait(false);

                if (completedTask != completionTask)
                {
                    TerminateAndReleasePipes(process);

                    cancellationToken.ThrowIfCancellationRequested();

                    return new BinaryProcessResult(CaptureStatus.TimedOut, null, "The maim process exceeded its time limit.");
                }

                await completionTask.ConfigureAwait(false);

                if (linkedSource.IsCancellationRequested)
                {
                    TerminateAndReleasePipes(process);

                    cancellationToken.ThrowIfCancellationRequested();

                    return new BinaryProcessResult(CaptureStatus.TimedOut, null, "The maim process exceeded its time limit.");
                }

                BinaryReadResult output = await outputTask.ConfigureAwait(false);
                string error = await errorTask.ConfigureAwait(false);

                if (output.TooLarge)
                {
                    return new BinaryProcessResult(CaptureStatus.OutputTooLarge, null, "The maim PNG exceeded the configured byte limit.");
                }

                if (process.ExitCode != 0)
                {
                    string failure = string.IsNullOrWhiteSpace(error) ? "The maim process returned a non-zero exit code." : error;
                    return new BinaryProcessResult(CaptureStatus.Failed, null, SanitizeFailure(failure, childEnvironment));
                }

                return new BinaryProcessResult(null, output.Bytes, error);
            }
            catch (OperationCanceledException)
            {
                TerminateAndReleasePipes(process);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                return new BinaryProcessResult(CaptureStatus.TimedOut, null, "The maim process exceeded its time limit.");
            }
            catch (Exception exception)
            {
                TerminateAndReleasePipes(process);
                return new BinaryProcessResult(CaptureStatus.Failed, null, SanitizeFailure(exception.Message, childEnvironment));
            }
        }
    }

    private async Task<GameWindowSnapshot> LocateAfterAsync(
        long xid,
        GameWindowExpectation expectation,
        GameWindowSnapshot before,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _locator.LocateAsync(xid, expectation, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CreateUnavailableSnapshot(xid, exception.Message, before.CapturedAt);
        }
    }

    private static CaptureStatus? GetPreflightFailure(long xid, GameWindowExpectation expectation, GameWindowSnapshot snapshot)
    {
        if (!snapshot.Exists || snapshot.Identity.Status == GameWindowIdentityStatus.NotFound)
        {
            return CaptureStatus.WindowNotFound;
        }

        if (snapshot.Identity.Xid != xid || snapshot.Identity.Status == GameWindowIdentityStatus.UnverifiedExecutableMismatch)
        {
            return CaptureStatus.WindowChanged;
        }

        if (!snapshot.IsMapped || !snapshot.IsViewable || snapshot.IsMinimized)
        {
            return CaptureStatus.WindowHidden;
        }

        if (expectation.RequireVerifiedProcess && snapshot.Identity.Status != GameWindowIdentityStatus.Verified)
        {
            return CaptureStatus.Failed;
        }

        if (!snapshot.IsSessionX11 || !snapshot.HasValidDisplay)
        {
            return CaptureStatus.Failed;
        }

        if (!snapshot.GeometryReliable
            || snapshot.ClientWidth <= 0
            || snapshot.ClientHeight <= 0
            || snapshot.BorderWidth < 0
            || snapshot.WindowWidth <= 0
            || snapshot.WindowHeight <= 0)
        {
            return CaptureStatus.InvalidDimensions;
        }

        return null;
    }

    private static bool SnapshotsMatch(GameWindowSnapshot before, GameWindowSnapshot after)
    {
        return after.Exists
            && after.Identity.Xid == before.Identity.Xid
            && after.Identity.ProcessId == before.Identity.ProcessId
            && string.Equals(after.Identity.ExecutablePath, before.Identity.ExecutablePath, StringComparison.Ordinal)
            && after.Identity.Status == before.Identity.Status
            && after.IsMapped == before.IsMapped
            && after.IsViewable == before.IsViewable
            && after.IsMinimized == before.IsMinimized
            && after.DesktopOrigin == before.DesktopOrigin
            && after.WindowWidth == before.WindowWidth
            && after.WindowHeight == before.WindowHeight
            && after.BorderWidth == before.BorderWidth
            && after.ClientWidth == before.ClientWidth
            && after.ClientHeight == before.ClientHeight
            && after.GeometryReliable;
    }

    private static bool HasEmptyContent(Mat image)
    {
        using Mat gray = new();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.MinMaxLoc(gray, out double minimum, out double maximum);

        if (minimum == maximum)
        {
            return true;
        }

        using Mat nearBlack = new();
        Cv2.Threshold(gray, nearBlack, 8, 255, ThresholdTypes.BinaryInv);
        int nearBlackPixels = Cv2.CountNonZero(nearBlack);
        int columns = gray.Cols;
        int rows = gray.Rows;
        long totalPixels = (long)columns * rows;
        return totalPixels > 0 && nearBlackPixels / (double)totalPixels >= 0.995;
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

    private static async Task<BinaryReadResult> ReadBinaryOutputAsync(
        Stream stream,
        int maxBytes,
        Process process,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];
        using MemoryStream output = new(Math.Min(maxBytes, 16 * 1024));

        while (true)
        {
            int count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (count == 0)
            {
                return new BinaryReadResult(output.ToArray(), false);
            }

            int remaining = maxBytes - (int)output.Length;

            if (count > remaining)
            {
                TryKill(process);
                return new BinaryReadResult([], true);
            }

            output.Write(buffer, 0, count);
        }
    }

    private static async Task<string> ReadStandardErrorAsync(
        Stream stream,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        using MemoryStream output = new();

        while (true)
        {
            int count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (count == 0)
            {
                break;
            }

            int remaining = MaximumStandardErrorBytes - (int)output.Length;

            if (remaining > 0)
            {
                output.Write(buffer, 0, Math.Min(count, remaining));
            }
        }

        string text = Encoding.UTF8.GetString(output.ToArray());
        return ProcessCommandRunner.Sanitize(text, environment);
    }

    private static CapturedFrame CreateFailureFrame(
        DateTimeOffset startedAt,
        GameWindowSnapshot before,
        GameWindowSnapshot after,
        CaptureStatus status,
        string? failure,
        int pixelWidth = 0,
        int pixelHeight = 0,
        int? widthDifference = null,
        int? heightDifference = null)
    {
        return new CapturedFrame(
            Guid.NewGuid(),
            before.Identity,
            startedAt,
            DateTimeOffset.UtcNow,
            before,
            after,
            pixelWidth,
            pixelHeight,
            new Mat(),
            status,
            failure is null ? null : SanitizeFailure(failure),
            contentUsable: false,
            widthDifference,
            heightDifference);
    }

    private static GameWindowSnapshot CreateUnavailableSnapshot(long xid, string failure, DateTimeOffset? capturedAt = null)
    {
        return new GameWindowSnapshot(
            new GameWindowIdentity(xid, null, null, null, GameWindowIdentityStatus.NotFound),
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
            SanitizeFailure(failure),
            capturedAt ?? DateTimeOffset.UtcNow);
    }

    private static string DescribeSnapshotFailure(GameWindowSnapshot snapshot)
    {
        return snapshot.Failure ?? "The requested window did not pass capture preflight checks.";
    }

    private static string SanitizeFailure(string value, IReadOnlyDictionary<string, string?>? environment = null)
    {
        string sanitized = ProcessCommandRunner.Sanitize(value, environment);

        if (sanitized.Length > MaximumFailureLength)
        {
            sanitized = sanitized[..MaximumFailureLength];
        }

        return sanitized;
    }

    private static void TryKill(Process process)
    {
        // A descendant that has already been reparented may survive tree termination; closing our pipes still bounds this call.
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static void TerminateAndReleasePipes(Process process)
    {
        TryKill(process);
        DisposePipe(process.StandardOutput);
        DisposePipe(process.StandardError);
    }

    private static void DisposePipe(StreamReader reader)
    {
        try
        {
            reader.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void ObserveFailure(Task task)
    {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private sealed record BinaryProcessResult(CaptureStatus? Status, byte[]? StandardOutput, string? Failure);

    private sealed record BinaryReadResult(byte[] Bytes, bool TooLarge);
}
