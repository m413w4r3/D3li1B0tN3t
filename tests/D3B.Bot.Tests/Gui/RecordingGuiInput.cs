using D3B.Gui.Input;
using D3B.Gui.Windowing;

namespace D3B.Bot.Tests.Gui;

internal sealed class RecordingGuiInput : IGuiInput
{
    public bool IsEnabled { get; set; } = true;

    public bool EmitsRealInput { get; set; }

    public GuiInputResult Result { get; set; } = new(GuiInputStatus.Sent);

    public int ClickCount { get; private set; }

    public DesktopPixels? LastPoint { get; private set; }

    public GameWindowSnapshot? LastWindow { get; private set; }

    public Task<GuiInputResult> ClickAsync(
        DesktopPixels point,
        GameWindowSnapshot expectedWindow,
        CancellationToken cancellationToken)
    {
        ClickCount++;
        LastPoint = point;
        LastWindow = expectedWindow;
        return Task.FromResult(Result);
    }
}
