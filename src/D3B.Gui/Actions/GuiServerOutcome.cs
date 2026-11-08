namespace D3B.Gui.Actions;

/// <summary>Neutral server result categories; D3B.Bot will map observed server events to these values.</summary>
public enum GuiServerOutcome
{
    /// <summary>The server confirmed movement to the expected cell.</summary>
    /// <remarks>D3B.Bot will map its observed movement confirmation to this value.</remarks>
    MovementConfirmed,

    /// <summary>The server reported the end of the expected element use.</summary>
    /// <remarks>D3B.Bot will map its observed element-use end event to this value.</remarks>
    ElementUseEnded
}
