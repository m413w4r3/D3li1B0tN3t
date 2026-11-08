namespace D3B.Gui.Safety;

/// <summary>Selects whether outgoing gameplay requests may come from protocol injection or GUI input.</summary>
public enum GameInputMode
{
    /// <summary>Keep the existing protocol injection behavior.</summary>
    ProtocolInjection = 0,

    /// <summary>Accept client traffic while refusing D3B gameplay requests.</summary>
    GuiClientInput = 1,
}
