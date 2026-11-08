namespace D3B.Bot.Gui;

/// <summary>The result of submitting an intention to the correlation inbox.</summary>
public sealed record GuiIntentRegistration
{
    /// <summary>Gets the registration decision.</summary>
    public GuiIntentRegistrationStatus Status { get; }

    /// <summary>Gets the submitted intention identifier.</summary>
    public Guid IntentId { get; }

    /// <summary>Gets an optional explanation.</summary>
    public string? Detail { get; }

    /// <summary>Creates a registration result.</summary>
    public GuiIntentRegistration(GuiIntentRegistrationStatus status, Guid intentId, string? detail)
    {
        Status = status;
        IntentId = intentId;
        Detail = detail;
    }
}
