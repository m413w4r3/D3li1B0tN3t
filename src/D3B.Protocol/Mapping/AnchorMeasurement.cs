using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>What captures say of a build's schema: its root, and the anchors their frames give.</summary>
public sealed class AnchorMeasurement
{
    /// <summary>Gets the number of frames read, both sides.</summary>
    public int Frames { get; }

    /// <summary>Gets the obfuscated message that reads every frame and types the most of their fields, when one does alone.</summary>
    public MessageDescriptor? Root { get; }

    /// <summary>Gets the fields the root types in all the frames, at every depth.</summary>
    public int RootTyped { get; }

    /// <summary>Gets the fields the next best reader types, or −1 when no other message reads every frame.</summary>
    public int NextTyped { get; }

    /// <summary>Gets the anchors, one per readable message the frames paired.</summary>
    public IReadOnlyList<Anchor> Anchors { get; }

    /// <summary>Gets the messages the frames carry in an <c>Any</c>, by the type URLs naming them, in the schema's order.</summary>
    public IReadOnlyList<CapturedPayload> Payloads { get; }

    /// <summary>Gets what contradicts the plan the captures were read with; an anchor it touches is left out.</summary>
    public IReadOnlyList<string> Problems { get; }

    internal AnchorMeasurement(int frames, MessageDescriptor? root, int rootTyped, int nextTyped, IReadOnlyList<Anchor> anchors, IReadOnlyList<CapturedPayload> payloads, IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(payloads);
        ArgumentNullException.ThrowIfNull(problems);

        Frames = frames;
        Root = root;
        RootTyped = rootTyped;
        NextTyped = nextTyped;
        Anchors = anchors;
        Payloads = payloads;
        Problems = problems;
    }
}
