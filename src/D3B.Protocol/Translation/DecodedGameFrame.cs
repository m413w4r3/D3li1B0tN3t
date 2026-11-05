using System.Diagnostics.CodeAnalysis;
using D3B.Protocol.Game;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Protocol.Translation;

/// <summary>A frame of the game connection decoded through a build's translation: its envelope, and the message its <c>Any</c> carries.</summary>
public sealed class DecodedGameFrame
{
    /// <summary>Gets the frame's length in bytes, its varint length left out.</summary>
    public int Length { get; }

    /// <summary>Gets the frame's envelope, or <see langword="null"/> when the frame is no game message.</summary>
    public GameMessage? Envelope { get; }

    /// <summary>Gets the build's name of the message the envelope carries, as its type URL names it, or <see langword="null"/> when it carries none.</summary>
    public string? CarriedType { get; }

    /// <summary>Gets the readable message the type URL names, or <see langword="null"/> when the readable protocol lacks it.</summary>
    public MessageDescriptor? ReadableType { get; }

    /// <summary>Gets the readable message the envelope carries, or <see langword="null"/> when it carries none the readable protocol reads.</summary>
    public IMessage? Content { get; }

    /// <summary>Gets why the frame was not understood whole, or <see langword="null"/> when it was.</summary>
    public string? Problem { get; }

    /// <summary>Gets the structured kind of <see cref="Problem"/>.</summary>
    public TranslationProblemKind ProblemKind { get; }

    /// <summary>Gets whether the carried payload is valid for its type: an unknown type's build schema, or a readable type the readable parser accepted.</summary>
    /// <remarks>
    /// For an unknown carried type, the build's schema is walked exactly. For a readable type, this is true when the
    /// readable parser accepted the payload, whole or partial; a partial readable frame is read and is not walked against
    /// the fields the readable protocol leaves out (D47). A frame with no content has nothing to validate. Malformed
    /// envelopes, length mismatches and unreadable readable payloads are false. This property alone never authorizes
    /// movement: the non-position policy also requires <see cref="TranslationProblemKind.UnknownCarriedType"/> and its
    /// build-scoped list.
    /// </remarks>
    public bool IsSchemaValid { get; }

    /// <summary>Gets whether the envelope's <c>Any</c> names its type with the game's type URL prefix.</summary>
    public bool IsGameTypeUrl { get; }

    /// <summary>Gets whether the frame was understood whole: its envelope and its content encode back to their lengths, so D3B may rewrite it.</summary>
    [MemberNotNullWhen(true, nameof(Envelope))]
    [MemberNotNullWhen(false, nameof(Problem))]
    public bool IsUnderstood => Problem is null;

    /// <summary>Gets whether the frame was read: its envelope understood whole, and its content a readable message, whose fields read.</summary>
    /// <remarks>
    /// A frame of a partial message holding a field the readable protocol leaves out on purpose is read, not understood
    /// whole: bot code reads what it declares, and D3B passes the frame on as it came (D47).
    /// </remarks>
    [MemberNotNullWhen(true, nameof(Envelope), nameof(Content))]
    public bool IsRead { get; }

    internal DecodedGameFrame(int length, GameMessage? envelope, string? carriedType, MessageDescriptor? readableType, IMessage? content, bool read, string? problem, TranslationProblemKind problemKind, bool schemaValid, bool gameTypeUrl)
    {
        Length = length;
        Envelope = envelope;
        CarriedType = carriedType;
        ReadableType = readableType;
        Content = content;
        IsRead = read && envelope is not null && content is not null;
        Problem = problem;
        ProblemKind = problemKind;
        IsSchemaValid = schemaValid;
        IsGameTypeUrl = gameTypeUrl;
    }
}
