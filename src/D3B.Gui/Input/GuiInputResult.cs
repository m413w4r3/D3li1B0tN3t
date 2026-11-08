namespace D3B.Gui.Input;

/// <summary>Reports the result of a GUI input request.</summary>
/// <param name="Status">The explicit input outcome.</param>
/// <param name="Detail">An optional bounded and sanitized explanation.</param>
/// <param name="ClickMayHaveBeenEmitted">Whether an uncertain result followed a launched click command.</param>
public sealed record GuiInputResult(
    GuiInputStatus Status,
    string? Detail = null,
    bool ClickMayHaveBeenEmitted = false);
