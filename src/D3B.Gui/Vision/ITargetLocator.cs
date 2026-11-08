using D3B.Gui.Calibration;
using D3B.Gui.Capture;

namespace D3B.Gui.Vision;

/// <summary>Locates a visual candidate around a calibrated logical cell without performing input or I/O.</summary>
public interface ITargetLocator
{
    /// <summary>Locates a target in one already captured frame.</summary>
    /// <param name="frame">The caller-owned captured frame.</param>
    /// <param name="calibration">The calibration associated with that frame's window and dimensions.</param>
    /// <param name="expectation">The logical target and caller-owned template.</param>
    /// <returns>A verified, ambiguous, or unverified candidate result.</returns>
    TargetRegion Locate(CapturedFrame frame, CalibrationProfile calibration, TargetExpectation expectation);
}
