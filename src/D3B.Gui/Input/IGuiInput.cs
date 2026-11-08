using D3B.Gui.Windowing;

namespace D3B.Gui.Input;

/// <summary>Emits a GUI click in global desktop pixel coordinates.</summary>
public interface IGuiInput
{
    /// <summary>Gets whether this implementation is enabled to accept a click request.</summary>
    bool IsEnabled { get; }

    /// <summary>Gets whether this implementation can emit real desktop input.</summary>
    /// <remarks>Real input implementations must return true so Preview policy can reject them.</remarks>
    bool EmitsRealInput { get; }

    /// <summary>Clicks a global desktop point only when the expected window is still active and unchanged.</summary>
    /// <param name="point">The global desktop position; local image coordinates are never accepted here.</param>
    /// <param name="expectedWindow">The window observation against which identity and geometry are checked.</param>
    /// <param name="cancellationToken">Cancels checks or an external input command.</param>
    /// <returns>The explicit outcome and, when needed, a bounded sanitized detail.</returns>
    Task<GuiInputResult> ClickAsync(DesktopPixels point, GameWindowSnapshot expectedWindow, CancellationToken cancellationToken);
}
