using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>A message a side's frame carries in an <c>Any</c>, as the readable protocol names it: the game's frames carry theirs so.</summary>
public sealed class AnchorPayload
{
    /// <summary>Gets the <c>Any</c> field of the side's message that carries the payload.</summary>
    public FieldDescriptor Content { get; }

    /// <summary>Gets which of the side's frames holding its field carries it, from 1.</summary>
    public int Frame { get; }

    /// <summary>Gets the readable message the frame carries.</summary>
    public MessageDescriptor Message { get; }

    /// <summary>Initializes a new instance of the <see cref="AnchorPayload"/> class.</summary>
    /// <param name="content">The <c>Any</c> field of the side's message that carries the payload.</param>
    /// <param name="frame">Which of the side's frames holding its field carries it, from 1.</param>
    /// <param name="message">The readable message the frame carries.</param>
    /// <exception cref="ArgumentException">The content is no <c>Any</c> field.</exception>
    public AnchorPayload(FieldDescriptor content, int frame, MessageDescriptor message)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfLessThan(frame, 1);

        if (content.FieldType is not FieldType.Message || content.MessageType.FullName != Google.Protobuf.WellKnownTypes.Any.Descriptor.FullName || content.IsRepeated)
            throw new ArgumentException($"The payload's content, {content.FullName}, must be a single google.protobuf.Any field.", nameof(content));

        Content = content;
        Frame = frame;
        Message = message;
    }
}
