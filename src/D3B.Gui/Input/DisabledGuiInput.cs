using D3B.Gui.Windowing;

namespace D3B.Gui.Input;

/// <summary>Provides the host-safe input implementation that never runs a process or emits input.</summary>
public sealed class DisabledGuiInput : IGuiInput
{
    /// <inheritdoc />
    public Task<GuiInputResult> ClickAsync(DesktopPixels point, GameWindowSnapshot expectedWindow, CancellationToken cancellationToken)
    {
        return Task.FromResult(new GuiInputResult(GuiInputStatus.Disabled));
    }
}
