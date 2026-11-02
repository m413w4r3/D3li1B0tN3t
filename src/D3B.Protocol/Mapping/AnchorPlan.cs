using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>What the readable protocol says captures hold, which anchors are measured against.</summary>
/// <remarks>
/// Every frame is the root message. The client's frames are its <see cref="ClientField"/>, the server's its
/// <see cref="ServerField"/>, and the n-th exchange of a connection, the n-th frame of each side, is a member of a oneof
/// of theirs, named by <see cref="Exchanges"/>. A side's frame may instead carry its message in an <c>Any</c>, as the
/// game's do, whose type URL names the build's message: <see cref="Payloads"/> names some. Only the structure of the
/// frames is read, and type URLs, which name the schema's own types, never another value.
/// </remarks>
public sealed class AnchorPlan
{
    /// <summary>Gets the message every frame is.</summary>
    public MessageDescriptor Root { get; }

    /// <summary>Gets the field of the root the client's frames hold.</summary>
    public FieldDescriptor ClientField { get; }

    /// <summary>Gets the field of the root the server's frames hold.</summary>
    public FieldDescriptor ServerField { get; }

    /// <summary>Gets a connection's exchanges, in order.</summary>
    public IReadOnlyList<AnchorExchange> Exchanges { get; }

    /// <summary>Gets the messages some of a side's frames carry in an <c>Any</c>.</summary>
    public IReadOnlyList<AnchorPayload> Payloads { get; }

    /// <summary>Initializes a new instance of the <see cref="AnchorPlan"/> class.</summary>
    /// <param name="root">The message every frame is.</param>
    /// <param name="clientField">The field of the root the client's frames hold.</param>
    /// <param name="serverField">The field of the root the server's frames hold.</param>
    /// <param name="exchanges">A connection's exchanges, in order.</param>
    /// <param name="payloads">The messages some of a side's frames carry in an <c>Any</c>, or none.</param>
    /// <exception cref="ArgumentException">A field is not where the plan needs it, or not a message field.</exception>
    public AnchorPlan(MessageDescriptor root, FieldDescriptor clientField, FieldDescriptor serverField, IReadOnlyList<AnchorExchange> exchanges, IReadOnlyList<AnchorPayload>? payloads = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(clientField);
        ArgumentNullException.ThrowIfNull(serverField);
        ArgumentNullException.ThrowIfNull(exchanges);

        payloads ??= [];

        if (clientField.ContainingType != root || serverField.ContainingType != root || clientField.FieldType is not FieldType.Message || serverField.FieldType is not FieldType.Message)
            throw new ArgumentException("The client's and the server's fields must be message fields of the root.");

        foreach (var exchange in exchanges)
        {
            if (exchange.Request.ContainingType != clientField.MessageType || exchange.Response.ContainingType != serverField.MessageType)
                throw new ArgumentException($"The exchange {exchange.Request.Name} must be a field of the client's message and a field of the server's.");

            if (exchange.Request.FieldType is not FieldType.Message || exchange.Response.FieldType is not FieldType.Message)
                throw new ArgumentException($"The exchange {exchange.Request.Name} must be message fields.");
        }

        foreach (var payload in payloads)
        {
            if (payload.Content.ContainingType != clientField.MessageType && payload.Content.ContainingType != serverField.MessageType)
                throw new ArgumentException($"The payload {payload.Message.Name} must be carried by a field of the client's message or of the server's.");
        }

        Root = root;
        ClientField = clientField;
        ServerField = serverField;
        Exchanges = exchanges;
        Payloads = payloads;
    }
}
