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

    /// <summary>Creates a blocked result for a coordinator guard that runs before the executor.</summary>
    public static GuiActionOutcome CreateBlocked(GuiSafetyReason reason, string? detail = null)
    {
        if (!Enum.IsDefined(reason) || reason == GuiSafetyReason.Ready)
            throw new ArgumentOutOfRangeException(nameof(reason));

        return new GuiActionOutcome(GuiActionOutcomeStatus.Blocked, reason, false, detail);
    }

    /// <summary>Creates an interrupted result while preserving whether one click may have been emitted.</summary>
    public static GuiActionOutcome CreateInterrupted(bool clickEmitted, string? detail = null)
    {
        return new GuiActionOutcome(GuiActionOutcomeStatus.Interrupted, null, clickEmitted, detail);
    }

    /// <summary>Creates a failed result while preserving whether one click was reported as emitted.</summary>
    public static GuiActionOutcome CreateFailed(bool clickEmitted, string? detail = null)
    {
        return new GuiActionOutcome(GuiActionOutcomeStatus.Failed, null, clickEmitted, detail);
    }

    /// <summary>Creates an unconfirmed result after an attempted click could not be matched to its protocol outcome.</summary>
    public static GuiActionOutcome CreateUnconfirmed(bool clickEmitted, string? detail = null)
    {
        return new GuiActionOutcome(GuiActionOutcomeStatus.Unconfirmed, null, clickEmitted, detail);
    }
}
