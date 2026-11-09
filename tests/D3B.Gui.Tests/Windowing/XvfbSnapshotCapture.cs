using D3B.Gui.Capture;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Tests.Windowing;

internal sealed class XvfbSnapshotCapture : IWindowCapture
{
    private readonly IGameWindowLocator _locator;

    public int CaptureCount { get; private set; }

    public XvfbSnapshotCapture(IGameWindowLocator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        _locator = locator;
    }

    public async Task<CapturedFrame> CaptureAsync(
        long xid,
        GameWindowExpectation expectation,
        CancellationToken cancellationToken)
    {
        CaptureCount++;
        GameWindowSnapshot before = await _locator.LocateAsync(xid, expectation, cancellationToken).ConfigureAwait(false);
        GameWindowSnapshot after = await _locator.LocateAsync(xid, expectation, cancellationToken).ConfigureAwait(false);
        Mat image = new(after.WindowHeight, after.WindowWidth, MatType.CV_8UC3, Scalar.All(25));
        return new CapturedFrame(
            Guid.NewGuid(),
            after.Identity,
            before.CapturedAt,
            after.CapturedAt,
            before,
            after,
            image.Cols,
            image.Rows,
            image,
            CaptureStatus.Captured,
            null,
            true,
            image.Cols - after.WindowWidth,
            image.Rows - after.WindowHeight);
    }
}
