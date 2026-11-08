using D3B.Gui.Safety;

namespace D3B.Bot.Hosting;

/// <summary>The validated input mode the host applies to new game sessions.</summary>
internal sealed class GuiHostInputMode
{
    public GameInputMode Mode { get; }

    public GuiHostInputMode(GameInputMode mode)
    {
        Mode = mode;
    }
}
