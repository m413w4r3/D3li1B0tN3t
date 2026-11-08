namespace D3B.Gui.Actions;

/// <summary>Neutral client request categories; D3B.Bot will map them to observed client requests.</summary>
public enum GuiClientAction
{
    /// <summary>A client movement request for one map cell.</summary>
    /// <remarks>D3B.Bot will match this to the client's movement request without a D3B.Protocol dependency here.</remarks>
    Walk,

    /// <summary>A client request to use one interactive element.</summary>
    /// <remarks>D3B.Bot will match this to the client's interactive-use request without a D3B.Protocol dependency here.</remarks>
    UseElement
}
