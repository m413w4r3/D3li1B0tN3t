namespace D3B.Gui.Safety;

/// <summary>Applies the P2N request-admission contract for a selected input mode.</summary>
public static class GuiInputModePolicy
{
    /// <summary>Determines whether a classified request is admitted by the selected mode.</summary>
    /// <remarks>
    /// In GUI client input mode, client-originated traffic passes through and D3B gameplay requests are refused.
    /// Protocol injection retains its existing admission behavior; this policy does not change a network path.
    /// </remarks>
    public static bool IsAllowed<TRequest>(
        GameInputMode mode,
        TRequest request,
        IGameInputRequestClassifier<TRequest> classifier)
    {
        ArgumentNullException.ThrowIfNull(classifier);

        if (mode is GameInputMode.ProtocolInjection)
            return true;

        if (mode is not GameInputMode.GuiClientInput)
            return false;

        var classification = classifier.Classify(request);

        return classification.Source switch
        {
            GameInputRequestSource.Client => true,
            GameInputRequestSource.D3B => !classification.IsGameplay,
            _ => false,
        };
    }
}
