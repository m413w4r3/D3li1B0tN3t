using D3B.Gui.Input;
using D3B.Gui.Windowing;

namespace D3B.Bot.Tests.Gui;

internal sealed class GuiWalkToCellTestInput : IGuiInput
{
    public bool IsEnabled { get; set; } = true;

    public bool EmitsRealInput => false;

    public int ClickCount { get; private set; }

    public Func<DesktopPixels, GameWindowSnapshot, CancellationToken, Task<GuiInputResult>>? OnClick { get; set; }

    public Func<DesktopPixels, GameWindowSnapshot, GuiClickLease, CancellationToken, Task<GuiInputResult>>? OnLeaseAwareClick { get; set; }

    public async Task<GuiInputResult> ClickAsync(
        DesktopPixels point,
        GameWindowSnapshot expectedWindow,
        CancellationToken cancellationToken)
    {
        return await ClickCoreAsync(point, expectedWindow, null, cancellationToken);
    }

    public async Task<GuiInputResult> ClickAsync(
        DesktopPixels point,
        GameWindowSnapshot expectedWindow,
        GuiClickLease lease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return await ClickCoreAsync(point, expectedWindow, lease, cancellationToken);
    }

    private async Task<GuiInputResult> ClickCoreAsync(
        DesktopPixels point,
        GameWindowSnapshot expectedWindow,
        GuiClickLease? lease,
        CancellationToken cancellationToken)
    {
        ClickCount++;

        if (lease is not null && OnLeaseAwareClick is not null)
            return await OnLeaseAwareClick(point, expectedWindow, lease, cancellationToken);

        if (OnClick is null)
            return new GuiInputResult(GuiInputStatus.Sent);

        return await OnClick(point, expectedWindow, cancellationToken);
    }
}
