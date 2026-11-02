using System.Text;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace D3B.Protocol.Mapping;

/// <summary>The pairs a plan's frames give, gathered frame by frame, with what contradicts them.</summary>
internal sealed class AnchorReading
{
    private readonly AnchorPlan _plan;
    private readonly MessageDescriptor _root;
    private readonly List<string> _problems;
    private readonly Dictionary<string, MessageDescriptor> _schemaMessages;
    private readonly Dictionary<MessageDescriptor, MessageDescriptor> _messages = [];
    private readonly Dictionary<MessageDescriptor, SortedDictionary<int, int>> _fields = [];
    private readonly HashSet<MessageDescriptor> _contradicted = [];
    private readonly Dictionary<MessageDescriptor, (int Client, int Server)> _payloads = [];

    public AnchorReading(AnchorPlan plan, FileDescriptor file, MessageDescriptor root, List<string> problems)
    {
        _plan = plan;
        _root = root;
        _problems = problems;
        _schemaMessages = Descriptors.Messages(file.MessageTypes).ToDictionary(message => message.FullName, StringComparer.Ordinal);
        Pair(plan.Root, root);
    }

    /// <summary>Reads the frames one side of a connection sent.</summary>
    /// <param name="frames">The frames, in the order they were sent.</param>
    /// <param name="sideField">The plan's field of the root for that side.</param>
    /// <param name="client">Whether the side is the client's, whose exchanges are the requests.</param>
    public void ReadSide(IReadOnlyList<ReadOnlyMemory<byte>> frames, FieldDescriptor sideField, bool client)
    {
        var side = client ? "client" : "server";
        int? sideNumber = null;
        var exchanges = 0;

        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index].Span;

            if (!WireReading.TryFields(frame, out var fields) || fields.Count is not 1 || fields[0].WireType is not 2)
            {
                _problems.Add($"Frame {index + 1} of a {side} side holds {fields.Count} fields at its root, where the plan expects one message.");

                continue;
            }

            var sideWire = fields[0];
            var obfuscatedSide = _root.FindFieldByNumber(sideWire.Number);

            if (obfuscatedSide?.FieldType is not FieldType.Message)
            {
                _problems.Add($"Frame {index + 1} of a {side} side holds field {sideWire.Number} of the root, which is no message field.");

                continue;
            }

            var value = frame.Slice(sideWire.Start, sideWire.Length);
            var payload = ReadPayload(obfuscatedSide.MessageType, value, $"Frame {index + 1} of a {side} side", client);

            // A side's first frame is its plan field: a connection opens on the identification. A later frame holding another
            // field of the root, such as an event the server sends unasked, is no exchange, and leaves their count alone.
            sideNumber ??= sideWire.Number;

            if (sideWire.Number != sideNumber)
                continue;

            PairField(_plan.Root, sideField.FieldNumber, sideWire.Number);
            Pair(sideField.MessageType, obfuscatedSide.MessageType);
            exchanges++;

            foreach (var planned in _plan.Payloads.Where(planned => planned.Content.ContainingType == sideField.MessageType && planned.Frame == exchanges))
            {
                if (payload is null)
                {
                    _problems.Add($"Frame {index + 1} of a {side} side carries no message in an Any, where the plan expects {planned.Message.Name}.");

                    continue;
                }

                PairField(sideField.MessageType, planned.Content.FieldNumber, payload.Value.Field.FieldNumber);
                Pair(planned.Message, payload.Value.Message);
            }

            if (exchanges > _plan.Exchanges.Count)
                continue;

            var exchange = client ? _plan.Exchanges[exchanges - 1].Request : _plan.Exchanges[exchanges - 1].Response;

            if (!WireReading.TryFields(value, out var inner))
                continue;

            var members = inner.Select(wire => obfuscatedSide.MessageType.FindFieldByNumber(wire.Number))
                .Where(field => field is not null && Descriptors.InRealOneof(field) && field.FieldType is FieldType.Message)
                .ToList();

            if (members.Count is not 1)
            {
                _problems.Add($"Frame {index + 1} of a {side} side holds {members.Count} message members of a oneof, where the plan expects {exchange.Name}.");

                continue;
            }

            PairField(sideField.MessageType, exchange.FieldNumber, members[0]!.FieldNumber);
            Pair(exchange.MessageType, members[0]!.MessageType);
        }
    }

    /// <summary>The anchors gathered, but those a frame contradicted.</summary>
    public IReadOnlyList<Anchor> Anchors()
    {
        return
        [
            .. _messages.Where(pair => !_contradicted.Contains(pair.Key)).Select(pair => new Anchor(
                pair.Key,
                pair.Value,
                _fields.TryGetValue(pair.Key, out var fields) ? [.. fields.Select(field => (field.Key, field.Value))] : [])),
        ];
    }

    /// <summary>The messages the frames carried in an <c>Any</c>, in the schema's order.</summary>
    public IReadOnlyList<CapturedPayload> Payloads()
    {
        var order = _schemaMessages.Values.Select((message, index) => (message, index)).ToDictionary(pair => pair.message, pair => pair.index);

        return [.. _payloads.OrderBy(pair => order[pair.Key]).Select(pair => new CapturedPayload(pair.Key, pair.Value.Client, pair.Value.Server))];
    }

    /// <summary>Reads the message a side's message carries in its one <c>Any</c>, as its type URL names it.</summary>
    /// <returns>The <c>Any</c> field and the message, or <see langword="null"/> when the side's message carries none.</returns>
    private (FieldDescriptor Field, MessageDescriptor Message)? ReadPayload(MessageDescriptor sideMessage, ReadOnlySpan<byte> value, string frame, bool client)
    {
        if (!WireReading.TryFields(value, out var fields))
            return null;

        var anys = fields.Select(wire => (Wire: wire, Field: sideMessage.FindFieldByNumber(wire.Number)))
            .Where(pair => pair.Field is { FieldType: FieldType.Message } field && field.MessageType.FullName == Any.Descriptor.FullName)
            .ToList();

        if (anys.Count is 0)
            return null;

        if (anys.Count > 1)
        {
            _problems.Add($"{frame} carries {anys.Count} messages in an Any, where one is read.");

            return null;
        }

        var (wire, field) = anys[0];
        var any = value.Slice(wire.Start, wire.Length);

        if (!WireReading.TryFields(any, out var anyFields))
            return null;

        var url = anyFields.LastOrDefault(anyField => anyField.Number is Any.TypeUrlFieldNumber && anyField.WireType is 2);
        var content = anyFields.LastOrDefault(anyField => anyField.Number is Any.ValueFieldNumber && anyField.WireType is 2);

        if (url.Number is 0)
        {
            _problems.Add($"{frame} carries an Any without a type URL.");

            return null;
        }

        var typeUrl = Encoding.UTF8.GetString(any.Slice(url.Start, url.Length));
        var name = Any.GetTypeName(typeUrl);

        if (!_schemaMessages.TryGetValue(name, out var message))
        {
            _problems.Add($"{frame} carries a type URL naming {name}, which is no message of the schema's file.");

            return null;
        }

        if (WireReading.Typed(message, any.Slice(content.Start, content.Length)) < 0)
        {
            _problems.Add($"{frame} carries a value that does not read as {name}, which its type URL names.");

            return null;
        }

        var (fromClient, fromServer) = _payloads.GetValueOrDefault(message);

        _payloads[message] = client ? (fromClient + 1, fromServer) : (fromClient, fromServer + 1);

        return (field!, message);
    }

    private void Pair(MessageDescriptor readable, MessageDescriptor obfuscated)
    {
        if (!_messages.TryGetValue(readable, out var known))
        {
            _messages[readable] = obfuscated;

            return;
        }

        if (known != obfuscated && _contradicted.Add(readable))
            _problems.Add($"The frames read {readable.FullName} as both {known.FullName} and {obfuscated.FullName}.");
    }

    private void PairField(MessageDescriptor readable, int readableNumber, int obfuscatedNumber)
    {
        if (!_fields.TryGetValue(readable, out var fields))
            _fields[readable] = fields = [];

        if (!fields.TryGetValue(readableNumber, out var known))
        {
            fields[readableNumber] = obfuscatedNumber;

            return;
        }

        if (known != obfuscatedNumber && _contradicted.Add(readable))
            _problems.Add($"The frames read field {readableNumber} of {readable.FullName} as both field {known} and field {obfuscatedNumber}.");
    }
}
