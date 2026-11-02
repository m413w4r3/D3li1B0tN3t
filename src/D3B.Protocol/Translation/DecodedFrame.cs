using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;

namespace D3B.Protocol.Translation;

/// <summary>A frame decoded through a build's translation: its message, and whether the message holds all of it.</summary>
/// <typeparam name="T">The readable message the frame was decoded as.</typeparam>
public sealed class DecodedFrame<T>
    where T : class, IMessage<T>
{
    /// <summary>Gets the frame's message, or <see langword="null"/> when the frame is no message of its type.</summary>
    public T? Message { get; }

    /// <summary>Gets why the frame was not understood whole, or <see langword="null"/> when it was.</summary>
    public string? Problem { get; }

    /// <summary>Gets whether the frame was understood whole: its message encodes back to its length, so D3B may rewrite it.</summary>
    [MemberNotNullWhen(true, nameof(Message))]
    [MemberNotNullWhen(false, nameof(Problem))]
    public bool IsUnderstood => Problem is null;

    internal DecodedFrame(T? message, string? problem)
    {
        Message = message;
        Problem = problem;
    }
}
