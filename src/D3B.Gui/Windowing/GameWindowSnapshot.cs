namespace D3B.Gui.Windowing;

/// <summary>A point-in-time observation of one explicitly named X11 window.</summary>
/// <param name="Identity">Process identity from the X11 PID property and the matching <c>/proc/&lt;pid&gt;/exe</c> link; the title is descriptive only.</param>
/// <param name="Exists">Whether <c>xwininfo</c> confirmed that the requested XID exists.</param>
/// <param name="IsMapped">Whether the X11 map state is anything other than <c>IsUnMapped</c>.</param>
/// <param name="IsViewable">Whether <c>xwininfo</c> reported map state <c>IsViewable</c>.</param>
/// <param name="IsMinimized">Whether <c>WM_STATE</c> reports <c>Iconic</c> or <c>_NET_WM_STATE</c> contains <c>_NET_WM_STATE_HIDDEN</c>.</param>
/// <param name="IsFocused">Whether <c>xdotool getwindowfocus</c> returned this XID.</param>
/// <param name="IsActive">Whether <c>_NET_ACTIVE_WINDOW</c> names this XID, or the focus result when that property is unavailable.</param>
/// <param name="IsXTestAvailable">Whether <c>xdpyinfo</c> listed the XTEST extension.</param>
/// <param name="IsSessionX11">Whether the injected environment reader reported <c>XDG_SESSION_TYPE=x11</c>.</param>
/// <param name="HasValidDisplay">Whether <c>xdpyinfo</c> successfully queried the configured display.</param>
/// <param name="GeometryReliable">Whether the absolute geometry from <c>xwininfo</c> agrees with <c>xdotool</c> and has positive client dimensions.</param>
/// <param name="DesktopOrigin">The absolute upper-left position reported by <c>xwininfo</c>, cross-checked with <c>xdotool</c>.</param>
/// <param name="WindowWidth">The total horizontal window extent, calculated from the X11 client width plus both borders.</param>
/// <param name="WindowHeight">The total vertical window extent, calculated from the X11 client height plus both borders.</param>
/// <param name="BorderWidth">The X11 border width reported by <c>xwininfo</c>.</param>
/// <param name="ClientWidth">The drawable width reported by <c>xwininfo</c> and cross-checked with <c>xdotool</c>.</param>
/// <param name="ClientHeight">The drawable height reported by <c>xwininfo</c> and cross-checked with <c>xdotool</c>.</param>
/// <param name="Failure">A sanitized explanation when a probe failed or a required process identity was not verified.</param>
/// <param name="CapturedAt">The UTC time when this observation began.</param>
/// <remarks>
/// Titles are never identity evidence. A non-verified identity may be used for Preview inspection, but it must not authorize future actions.
/// Missing minimization properties are reported as not minimized; an X11 server without a window manager may expose neither property.
/// A false <see cref="Exists"/> value with a non-null <see cref="Failure"/> means the XID could not be inspected, not that it was proven absent.
/// </remarks>
public sealed record GameWindowSnapshot(
    GameWindowIdentity Identity,
    bool Exists,
    bool IsMapped,
    bool IsViewable,
    bool IsMinimized,
    bool IsFocused,
    bool IsActive,
    bool IsXTestAvailable,
    bool IsSessionX11,
    bool HasValidDisplay,
    bool GeometryReliable,
    DesktopPixels DesktopOrigin,
    int WindowWidth,
    int WindowHeight,
    int BorderWidth,
    int ClientWidth,
    int ClientHeight,
    string? Failure,
    DateTimeOffset CapturedAt);
