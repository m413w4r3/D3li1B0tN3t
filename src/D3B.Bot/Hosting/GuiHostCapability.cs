using D3B.Gui.Safety;

namespace D3B.Bot.Hosting;

/// <summary>Reports the GUI features available to the host without activating GUI input or activity.</summary>
public sealed class GuiHostCapability
{
    /// <summary>Gets whether GUI capture and preview are enabled by configuration.</summary>
    public bool Enabled { get; }

    /// <summary>Gets the configured GUI operating mode.</summary>
    public GuiMode Mode { get; }

    /// <summary>Gets whether real GUI input is allowed by configuration.</summary>
    public bool AllowRealInput { get; }

    /// <summary>Gets whether automatic GUI activity is available.</summary>
    public bool AutomaticActivity { get; }

    /// <summary>Gets whether a supervised one-shot GUI action is available.</summary>
    public bool SupervisedOneShotAvailable { get; }

    /// <summary>Gets whether real GUI input is available.</summary>
    public bool RealInputAvailable { get; }

    /// <summary>Initializes a new disabled GUI capability with preview mode and no input or activity.</summary>
    public GuiHostCapability()
        : this(false, GuiMode.Preview, false)
    {
    }

    internal GuiHostCapability(bool enabled, GuiMode mode, bool allowRealInput)
    {
        Enabled = enabled;
        Mode = mode;
        AllowRealInput = allowRealInput;
        AutomaticActivity = false;
        SupervisedOneShotAvailable = false;
        RealInputAvailable = false;
    }
}
