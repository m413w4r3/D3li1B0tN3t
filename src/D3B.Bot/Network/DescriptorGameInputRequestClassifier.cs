using System.Collections.Frozen;
using D3B.Gui.Safety;
using Google.Protobuf;

namespace D3B.Bot.Network;

/// <summary>Classifies D3B requests by a deliberately explicit non-gameplay descriptor allowlist.</summary>
internal sealed class DescriptorGameInputRequestClassifier : IGameInputRequestClassifier<IMessage>
{
    private readonly FrozenSet<string> _nonGameplayDescriptors;

    public DescriptorGameInputRequestClassifier(IEnumerable<string> nonGameplayDescriptors)
    {
        ArgumentNullException.ThrowIfNull(nonGameplayDescriptors);

        _nonGameplayDescriptors = nonGameplayDescriptors.ToFrozenSet(StringComparer.Ordinal);
    }

    public GameInputRequestClassification Classify(IMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var gameplay = !_nonGameplayDescriptors.Contains(request.Descriptor.FullName);

        return new GameInputRequestClassification(GameInputRequestSource.D3B, gameplay);
    }
}
