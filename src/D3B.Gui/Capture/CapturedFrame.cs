using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Capture;

/// <summary>Owns one decoded capture image and the window observations surrounding the capture.</summary>
/// <remarks><see cref="Status"/> being <see cref="CaptureStatus.Captured"/> does not prove that a game scene was recognized.</remarks>
public sealed class CapturedFrame : IDisposable
{
    private int _disposed;

    /// <summary>The unique identifier for this capture attempt.</summary>
    public Guid CaptureId { get; }

    /// <summary>The X11 window identity observed before capture.</summary>
    public GameWindowIdentity WindowIdentity { get; }

    /// <summary>The UTC time at which capture preparation began.</summary>
    public DateTimeOffset CaptureStartedAt { get; }

    /// <summary>The UTC time at which the capture attempt completed.</summary>
    public DateTimeOffset CaptureCompletedAt { get; }

    /// <summary>The window state measured before capture.</summary>
    public GameWindowSnapshot WindowSnapshotBefore { get; }

    /// <summary>The window state measured after capture, or the initial observation when no capture process ran.</summary>
    public GameWindowSnapshot WindowSnapshotAfter { get; }

    /// <summary>The decoded image width, or zero when no image was decoded.</summary>
    public int PixelWidth { get; }

    /// <summary>The decoded image height, or zero when no image was decoded.</summary>
    public int PixelHeight { get; }

    /// <summary>The difference between the decoded width and measured X11 window extent, including both borders.</summary>
    public int? WidthDifferenceFromWindow { get; }

    /// <summary>The difference between the decoded height and measured X11 window extent, including both borders.</summary>
    public int? HeightDifferenceFromWindow { get; }

    /// <summary>The decoded BGR image. The caller owns this frame and must dispose it.</summary>
    public Mat Image { get; }

    /// <summary>The capture outcome.</summary>
    public CaptureStatus Status { get; }

    /// <summary>A bounded, sanitized explanation when the capture did not succeed.</summary>
    public string? Failure { get; }

    /// <summary>Whether the image passed the basic dimensions and content checks.</summary>
    public bool ContentUsable { get; }

    /// <summary>Creates an immutable result for one capture attempt.</summary>
    public CapturedFrame(
        Guid captureId,
        GameWindowIdentity windowIdentity,
        DateTimeOffset captureStartedAt,
        DateTimeOffset captureCompletedAt,
        GameWindowSnapshot windowSnapshotBefore,
        GameWindowSnapshot windowSnapshotAfter,
        int pixelWidth,
        int pixelHeight,
        Mat image,
        CaptureStatus status,
        string? failure,
        bool contentUsable,
        int? widthDifferenceFromWindow = null,
        int? heightDifferenceFromWindow = null)
    {
        ArgumentNullException.ThrowIfNull(windowIdentity);
        ArgumentNullException.ThrowIfNull(windowSnapshotBefore);
        ArgumentNullException.ThrowIfNull(windowSnapshotAfter);
        ArgumentNullException.ThrowIfNull(image);

        CaptureId = captureId;
        WindowIdentity = windowIdentity;
        CaptureStartedAt = captureStartedAt;
        CaptureCompletedAt = captureCompletedAt;
        WindowSnapshotBefore = windowSnapshotBefore;
        WindowSnapshotAfter = windowSnapshotAfter;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Image = image;
        Status = status;
        Failure = failure;
        ContentUsable = contentUsable;
        WidthDifferenceFromWindow = widthDifferenceFromWindow;
        HeightDifferenceFromWindow = heightDifferenceFromWindow;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Image.Dispose();
        }
    }
}
