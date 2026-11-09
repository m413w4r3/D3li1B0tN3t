using D3B.Gui.Input;
using D3B.Gui.Windowing;

namespace D3B.Bot.Tests.Gui;

internal sealed class GuiWalkToCellTestInput : IGuiInput
{
    public bool IsEnabled { get; set; } = true;

    public bool EmitsRealInput => false;

    public int ClickCount { get; private set; }

    public Func<DesktopPixels, GameWindowSnapshot, CancellationToken, Task<GuiInputResult>>? OnClick { get; set; }

    public async Task<GuiInputResult> ClickAsync(
        DesktopPixels point,
        GameWindowSnapshot expectedWindow,
        CancellationToken cancellationToken)
    {
        ClickCount++;
        if (OnClick is null)
            return new GuiInputResult(GuiInputStatus.Sent);

        return await OnClick(point, expectedWindow, cancellationToken);
    }
}
