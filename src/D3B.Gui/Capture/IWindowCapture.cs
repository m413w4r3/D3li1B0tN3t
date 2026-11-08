using D3B.Gui.Windowing;

namespace D3B.Gui.Capture;

/// <summary>Captures one explicitly identified X11 window.</summary>
public interface IWindowCapture
{
    /// <summary>Captures the requested window and returns a decoded frame with before/after observations.</summary>
    /// <param name="xid">The numeric X11 identifier selected by the caller.</param>
    /// <param name="expectation">The expected executable and process identity policy.</param>
    /// <param name="cancellationToken">Cancels the attempt and terminates any started capture process.</param>
    /// <returns>A frame that owns its decoded OpenCV image and must be disposed by the caller.</returns>
    Task<CapturedFrame> CaptureAsync(long xid, GameWindowExpectation expectation, CancellationToken cancellationToken);
}
