using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using D3B.Protocol.Connection;
using D3B.Protocol.Game;
using D3B.Protocol.Mapping;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace D3B.Protocol.Translation;

/// <summary>Reads a client build's wire through D3B's readable classes, once the build's mapping holds (D11, D47).</summary>
/// <remarks>
/// <para>
/// The readable classes read the wire as it is, since the obfuscation keeps every field's and value's number (D42). So a
/// build is translated only when its mapping holds the contract (D44) and pairs every readable field and enum value with
/// its own number. A build renumbering one is refused: its mapping holds both numbers, so rewriting them on the wire can
/// be added for the messages that change, once one does.
/// </para>
/// <para>
/// A frame is decoded once, its unknown fields dropped, and is understood only when its message encodes back to the
/// frame's length. One that does not holds a field the readable protocol lacks, or was encoded otherwise: D3B passes it
/// on as it is, and never rewrites it. An enum value the readable enum lacks keeps its number and encodes back as it
/// came, so nothing of it is lost; the audit notes the values a build adds.
/// </para>
/// <para>
/// A game frame's envelope carries its message in an <c>Any</c>, whose type URL names the build's obfuscated message: the
/// mapping turns it into the readable one, which then reads the <c>Any</c>'s value the same way. A partial message, which
/// reads only some of its counterpart's fields on purpose, is read without being understood whole.
/// </para>
/// <para>
/// A request D3B sends the game server is encoded the other way: the readable message's bytes are the wire's, and the
/// <c>Any</c> carrying them names the build's message (D60).
/// </para>
/// </remarks>
public sealed class ProtocolTranslation
{
    /// <summary>The start of every type URL of the game's <c>Any</c>, before the build's name of the message (D56).</summary>
    public const string TypeUrlPrefix = "type.ankama.com/";

    /// <summary>The id every request of the first milestone carries, in every capture: the client counts only some others (D60).</summary>
    public const int UncountedRequestId = -1;

    private static readonly MessageParser<ConnectionMessage> _connection = ConnectionMessage.Parser.WithDiscardUnknownFields(true);
    private static readonly MessageParser<GameMessage> _game = GameMessage.Parser.WithDiscardUnknownFields(true);

    private readonly Dictionary<string, (MessageDescriptor Message, MessageParser Parser)> _readableOf;
    private readonly Dictionary<string, string> _obfuscatedOf;

    /// <summary>Gets the client build it translates, as its version file names it.</summary>
    public string ClientVersion { get; }

    /// <summary>Gets what auditing the build's mapping checked.</summary>
    public AuditReport Audit { get; }

    private ProtocolTranslation(string clientVersion, AuditReport audit, Dictionary<string, (MessageDescriptor Message, MessageParser Parser)> readableOf, Dictionary<string, string> obfuscatedOf)
    {
        ClientVersion = clientVersion;
        Audit = audit;
        _readableOf = readableOf;
        _obfuscatedOf = obfuscatedOf;
    }

    /// <summary>Makes the translation of a build from its schema and its mapping, once they hold.</summary>
    /// <param name="schema">The build's schema.</param>
    /// <param name="mapping">The build's mapping, matched against that schema.</param>
    /// <returns>The translation.</returns>
    /// <exception cref="ProtocolMismatchException">The mapping fails the audit, or renumbers a readable field or value.</exception>
    public static ProtocolTranslation Create(ProtocolSchema schema, ProtocolMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(mapping);

        var audit = ContractAudit.Audit(ReadableProtocol.Files, schema, mapping);
        var failures = audit.Failures.Concat(Renumbered(mapping)).ToList();

        if (failures.Count > 0)
            throw new ProtocolMismatchException(mapping.ClientVersion, failures);

        var readable = ReadableProtocol.Files.SelectMany(file => Descriptors.Messages(file.MessageTypes)).ToDictionary(message => message.FullName, StringComparer.Ordinal);
        // A map's entry is paired so that the audit checks its key and value, but it has no class and no frame carries it alone.
        var readableOf = mapping.Messages.Where(pair => !Descriptors.IsMapEntry(readable[pair.Readable])).ToDictionary(
            pair => pair.Obfuscated,
            pair => (readable[pair.Readable], readable[pair.Readable].Parser.WithDiscardUnknownFields(true)),
            StringComparer.Ordinal);

        var obfuscatedOf = mapping.Messages.ToDictionary(pair => pair.Readable, pair => pair.Obfuscated, StringComparer.Ordinal);

        return new ProtocolTranslation(mapping.ClientVersion, audit, readableOf, obfuscatedOf);
    }

    /// <summary>Lists the readable fields and enum values a mapping pairs with another number, which no translation reads.</summary>
    /// <param name="mapping">The mapping.</param>
    /// <returns>One line per field or value renumbered; none when the build keeps the wire's numbers.</returns>
    public static IReadOnlyList<string> Renumbered(ProtocolMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        var renumbered = new List<string>();

        foreach (var message in mapping.Messages)
        {
            foreach (var field in message.Fields.Where(field => field.Readable != field.Obfuscated))
                renumbered.Add($"{message.Readable} field {field.Readable} is field {field.Obfuscated} of .{message.Obfuscated}: the build renumbers it.");
        }

        foreach (var type in mapping.Enums)
        {
            foreach (var value in type.Values.Where(value => value.Readable != value.Obfuscated))
                renumbered.Add($"{type.Readable} value {value.Readable} is value {value.Obfuscated} of .{type.Obfuscated}: the build renumbers it.");
        }

        return renumbered;
    }

    /// <summary>Decodes a frame of the connection phase, its varint length already read (D38).</summary>
    /// <param name="frame">The frame's bytes.</param>
    /// <returns>The frame's message, and whether it was understood whole.</returns>
    [SuppressMessage("Performance", "CA1822", Justification = "Only a build whose mapping holds has a translation: reading the wire through one is what the version gate guards.")]
    public DecodedFrame<ConnectionMessage> DecodeConnection(ReadOnlySequence<byte> frame)
    {
        ConnectionMessage message;

        try
        {
            message = _connection.ParseFrom(frame);
        }
        catch (InvalidProtocolBufferException exception)
        {
            return new DecodedFrame<ConnectionMessage>(null, $"it is no connection message: {exception.Message}");
        }

        var length = message.CalculateSize();

        return length == frame.Length
            ? new DecodedFrame<ConnectionMessage>(message, null)
            : new DecodedFrame<ConnectionMessage>(message, $"its message encodes back to {length} of its {frame.Length} bytes: it holds what the readable protocol lacks, or is encoded otherwise.");
    }

    /// <summary>Encodes a request of the game for the build's server: a frame, without its length, as the client sends one (D60).</summary>
    /// <remarks>
    /// The envelope carries the request with <see cref="UncountedRequestId"/>, as every request of the first milestone in
    /// every capture, and an <c>Any</c> whose type URL names the build's message. Only a message the frames carry may be
    /// sent, and only one read whole: a partial message would leave out what the server expects (D55).
    /// </remarks>
    /// <param name="request">The readable request.</param>
    /// <returns>The frame's bytes.</returns>
    /// <exception cref="ArgumentException">The message is no game message the frames carry, the mapping lacks it, or it is partial.</exception>
    public byte[] EncodeGameRequest(IMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var descriptor = request.Descriptor;

        if (descriptor.File.Package != ReadableProtocol.GamePackage || descriptor.ContainingType is not null || descriptor.File == GameMessage.Descriptor.File)
            throw new ArgumentException($"{descriptor.FullName} is no message the game's frames carry.", nameof(request));

        if (Descriptors.IsPartial(descriptor))
            throw new ArgumentException($"{descriptor.FullName} is partial: it leaves out fields its counterpart has, which a request must hold (D55).", nameof(request));

        if (!_obfuscatedOf.TryGetValue(descriptor.FullName, out var obfuscated))
            throw new ArgumentException($"The mapping of {ClientVersion} has no counterpart for {descriptor.FullName}.", nameof(request));

        var envelope = new GameMessage
        {
            Request = new GameRequest
            {
                Id = UncountedRequestId,
                Content = new Any { TypeUrl = TypeUrlPrefix + obfuscated, Value = request.ToByteString() },
            },
        };

        return envelope.ToByteArray();
    }

    /// <summary>Decodes a frame of the game connection, its varint length already read (D38), and the message its envelope carries.</summary>
    /// <param name="frame">The frame's bytes.</param>
    /// <returns>The frame's envelope and content, and whether both were understood whole.</returns>
    public DecodedGameFrame DecodeGame(ReadOnlySequence<byte> frame)
    {
        GameMessage envelope;

        try
        {
            envelope = _game.ParseFrom(frame);
        }
        catch (InvalidProtocolBufferException exception)
        {
            return new DecodedGameFrame((int)frame.Length, null, null, null, null, false, $"it is no game message: {exception.Message}");
        }

        var length = envelope.CalculateSize();

        if (length != frame.Length)
            return new DecodedGameFrame((int)frame.Length, envelope, null, null, null, false, $"its envelope encodes back to {length} of its {frame.Length} bytes: it holds what the readable protocol lacks, or is encoded otherwise.");

        var any = envelope.ContentCase switch
        {
            GameMessage.ContentOneofCase.Request => envelope.Request.Content,
            GameMessage.ContentOneofCase.Response => envelope.Response.Content,
            GameMessage.ContentOneofCase.Event => envelope.Event.Content,
            _ => null,
        };

        if (any is null)
            return new DecodedGameFrame((int)frame.Length, envelope, null, null, null, false, null);

        var carried = Any.GetTypeName(any.TypeUrl);

        if (!_readableOf.TryGetValue(carried, out var readable))
            return new DecodedGameFrame((int)frame.Length, envelope, carried, null, null, false, $"it carries .{carried}, which the readable protocol lacks.");

        IMessage content;

        try
        {
            content = readable.Parser.ParseFrom(any.Value);
        }
        catch (InvalidProtocolBufferException exception)
        {
            return new DecodedGameFrame((int)frame.Length, envelope, carried, readable.Message, null, false, $"it carries no {readable.Message.FullName}: {exception.Message}");
        }

        var contentLength = content.CalculateSize();

        return contentLength == any.Value.Length
            ? new DecodedGameFrame((int)frame.Length, envelope, carried, readable.Message, content, true, null)
            : new DecodedGameFrame((int)frame.Length, envelope, carried, readable.Message, content, true, $"its {readable.Message.FullName} encodes back to {contentLength} of its {any.Value.Length} bytes: it holds what the readable protocol lacks, or is encoded otherwise.");
    }
}
