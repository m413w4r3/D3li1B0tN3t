using D3B.Gui.Safety;

namespace D3B.Gui.Actions;

/// <summary>The final status and evidence for one consumed action intention.</summary>
public sealed record GuiActionOutcome
{
    /// <summary>Gets the terminal result.</summary>
    public GuiActionOutcomeStatus Status { get; }

    /// <summary>Gets the safety cause when the action was blocked.</summary>
    public GuiSafetyReason? BlockedReason { get; }

    /// <summary>Gets whether the input seam reported that one click was emitted.</summary>
    public bool ClickEmitted { get; }

    /// <summary>Gets an optional bounded explanation.</summary>
    public string? Detail { get; }

    /// <summary>Creates an outcome while preserving the rule that a click can never itself mean success.</summary>
    internal GuiActionOutcome(GuiActionOutcomeStatus status, GuiSafetyReason? blockedReason, bool clickEmitted, string? detail)
    {
        if (status == GuiActionOutcomeStatus.Pass && !clickEmitted)
            throw new ArgumentException("A passing action must include one emitted click and a confirmation.", nameof(clickEmitted));

        if (status == GuiActionOutcomeStatus.Pass && blockedReason is not null)
            throw new ArgumentException("A passing action cannot have a blocking reason.", nameof(blockedReason));

        if (status != GuiActionOutcomeStatus.Blocked && blockedReason is not null)
            throw new ArgumentException("Only a blocked action can carry a blocking reason.", nameof(blockedReason));

        Status = status;
        BlockedReason = blockedReason;
        ClickEmitted = clickEmitted;
        Detail = detail;
    }
}
