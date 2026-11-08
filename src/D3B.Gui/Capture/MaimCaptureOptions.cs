namespace D3B.Gui.Capture;

/// <summary>Bounds an external <c>maim</c> capture.</summary>
public sealed class MaimCaptureOptions
{
    /// <summary>Maximum stdout size accepted for a single PNG.</summary>
    public const int AbsoluteMaximumCaptureBytes = 16 * 1024 * 1024;

    /// <summary>Absolute path to the <c>maim</c> executable.</summary>
    public string BinaryPath { get; }

    /// <summary>Maximum time allowed for a capture process.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Maximum PNG bytes read from stdout.</summary>
    public int MaxCaptureBytes { get; }

    /// <summary>Maximum accepted decoded image width.</summary>
    public int MaxWidth { get; }

    /// <summary>Maximum accepted decoded image height.</summary>
    public int MaxHeight { get; }

    /// <summary>Creates capture limits with the P2N defaults.</summary>
    public MaimCaptureOptions(
        string binaryPath = "/usr/bin/maim",
        TimeSpan? timeout = null,
        int maxCaptureBytes = AbsoluteMaximumCaptureBytes,
        int maxWidth = 3840,
        int maxHeight = 2160)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binaryPath);

        if (!Path.IsPathFullyQualified(binaryPath))
        {
            throw new ArgumentException("The maim executable path must be absolute.", nameof(binaryPath));
        }

        TimeSpan actualTimeout = timeout ?? TimeSpan.FromSeconds(2);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(actualTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxCaptureBytes, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxCaptureBytes, AbsoluteMaximumCaptureBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxWidth, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxWidth, 3840);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxHeight, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxHeight, 2160);

        BinaryPath = binaryPath;
        Timeout = actualTimeout;
        MaxCaptureBytes = maxCaptureBytes;
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
    }
}
