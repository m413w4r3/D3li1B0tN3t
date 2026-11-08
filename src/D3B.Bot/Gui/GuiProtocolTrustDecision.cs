using D3B.Gui.Safety;

namespace D3B.Bot.Gui;

/// <summary>A fail-closed result that names the first missing or contradicted GUI trust proof.</summary>
public sealed record GuiProtocolTrustDecision
{
    /// <summary>Gets whether every required proof passed.</summary>
    public bool Allowed { get; }

    /// <summary>Gets the existing GUI safety reason for the result.</summary>
    public GuiSafetyReason Reason { get; }

    /// <summary>Gets a precise explanation for a blocked decision.</summary>
    public string? Detail { get; }

    /// <summary>Creates a decision that is either Ready or blocked by one safety reason.</summary>
    public GuiProtocolTrustDecision(bool allowed, GuiSafetyReason reason, string? detail)
    {
        if (allowed != (reason == GuiSafetyReason.Ready))
            throw new ArgumentException("An allowed decision must use Ready, and a blocked decision cannot use Ready.", nameof(reason));

        Allowed = allowed;
        Reason = reason;
        Detail = detail;
    }
}
