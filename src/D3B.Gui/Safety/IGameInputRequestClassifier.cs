namespace D3B.Gui.Safety;

/// <summary>Classifies outgoing requests without performing network operations.</summary>
/// <typeparam name="TRequest">The request representation used by an admission point.</typeparam>
public interface IGameInputRequestClassifier<in TRequest>
{
    /// <summary>Gets the origin and gameplay classification for a request.</summary>
    /// <param name="request">The request to classify.</param>
    /// <returns>The request classification.</returns>
    GameInputRequestClassification Classify(TRequest request);
}
