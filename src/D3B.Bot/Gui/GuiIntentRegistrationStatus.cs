namespace D3B.Bot.Gui;

/// <summary>Describes whether an intention entered the correlation inbox.</summary>
public enum GuiIntentRegistrationStatus
{
    /// <summary>The intention was accepted as the only active correlation.</summary>
    Registered,

    /// <summary>Another intention is active; this intention was refused permanently.</summary>
    Busy,

    /// <summary>This intention identifier was already submitted.</summary>
    Duplicate,

    /// <summary>The intention was already expired when submitted.</summary>
    Expired,

    /// <summary>The actor refused the intention because its current state was unsafe or had changed.</summary>
    Rejected
}
