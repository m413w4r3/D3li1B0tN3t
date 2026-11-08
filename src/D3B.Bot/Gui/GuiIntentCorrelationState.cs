namespace D3B.Bot.Gui;

/// <summary>States in the one-intention GUI-to-client correlation model.</summary>
public enum GuiIntentCorrelationState
{
    /// <summary>No intention has been registered.</summary>
    Idle,

    /// <summary>The intention is registered before any input is permitted.</summary>
    Registered,

    /// <summary>The registration was acknowledged against its session, window, and snapshot.</summary>
    Acknowledged,

    /// <summary>One click was recorded for the intention.</summary>
    Clicked,

    /// <summary>A compatible client request was observed.</summary>
    ClientRequestSeen,

    /// <summary>The matching server outcome was observed.</summary>
    ServerConfirmed,

    /// <summary>The confirmed intention was consumed permanently.</summary>
    Consumed,

    /// <summary>The intention expired or was canceled before a click.</summary>
    Cancelled,

    /// <summary>An unexpected request, identity change, or ambiguous user action interrupted correlation.</summary>
    Interrupted,

    /// <summary>The click may have occurred but the expected confirmation was absent or contradictory.</summary>
    Unconfirmed
}
