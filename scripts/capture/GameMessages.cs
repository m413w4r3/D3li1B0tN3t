using System.Buffers;
using System.Text;
using D3B.Network.Captures;
using D3B.Network.Framing;

namespace D3B.Scripts.Capture;

/// <summary>Lists the messages a game connection's frames carry, in the order they came, by their type URLs (D56, D58).</summary>
/// <remarks>
/// Each frame of the game is an envelope whose member carries an <c>Any</c>: its type URL names the build's message, the
/// only text read. A frame's time is the record that made it whole, so frames read together share one. The sides are
/// read apart, each a stream of frames, then merged by time, the client's first on a tie as the relay reads it first.
/// With a schema, the message's fields are typed there; with a mapping, the message is also named as the readable
/// protocol names it. Values stay out, as for the rest of this tool.
/// </remarks>
internal static class GameMessages
{
    private const string TypeUrlPrefix = "type.ankama.com/";

    /// <summary>Extracts complete frames and their message envelopes from capture records.</summary>
    /// <param name="records">The capture records paired with their zero-based index.</param>
    /// <param name="tolerateInvalidFrames">Whether to retain earlier frames if a side later has an invalid frame.</param>
    public static IReadOnlyList<GameMessage> Extract(IReadOnlyList<(CaptureRecord Record, long Index)> records, bool tolerateInvalidFrames = false)
    {
        var frames = new List<GameMessage>();

        foreach (var (kind, sideOrder, side) in new[]
        {
            (CaptureRecordKind.FromClient, 0, "client"),
            (CaptureRecordKind.FromServer, 1, "server"),
            (CaptureRecordKind.Injected, 2, "injected"),
        })
        {
            var pending = new List<byte>();
            var pendingOffset = 0L;
            var frameIndex = 0L;

            foreach (var (record, recordIndex) in records.Where(item => item.Record.Kind == kind))
            {
                pending.AddRange(record.Data.ToArray());

                var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(pending.ToArray()));

                try
                {
                    while (true)
                    {
                        var frameOffset = reader.Consumed;

                        if (!FrameReader.TryReadFrame(ref reader, out var frame))
                            break;

                        var content = Content(frame.ToArray());

                        frames.Add(new GameMessage
                        {
                            Member = content.Member,
                            TypeUrl = content.TypeUrl,
                            TypeName = content.TypeName,
                            Content = content.Content,
                            Offset = record.Offset,
                            FrameIndex = frameIndex,
                            RecordIndex = recordIndex,
                            ByteOffset = pendingOffset + frameOffset,
                            Side = side,
                            SideOrder = sideOrder,
                        });

                        frameIndex++;
                    }
                }
                catch (FrameFormatException) when (tolerateInvalidFrames)
                {
                    break;
                }

                pending.RemoveRange(0, (int)reader.Consumed);
                pendingOffset += reader.Consumed;
            }
        }

        return frames
            .OrderBy(frame => frame.Offset)
            .ThenBy(frame => frame.SideOrder)
            .Select((frame, index) => new GameMessage
            {
                Member = frame.Member,
                TypeUrl = frame.TypeUrl,
                TypeName = frame.TypeName,
                Content = frame.Content,
                Offset = frame.Offset,
                FrameIndex = frame.FrameIndex,
                RecordIndex = frame.RecordIndex,
                ByteOffset = frame.ByteOffset,
                Side = frame.Side,
                SideOrder = frame.SideOrder,
                MessageIndex = index,
            })
            .ToArray();
    }

    /// <summary>Writes a game capture's messages, one frame a line, with their fields when asked.</summary>
    /// <param name="output">Where to write.</param>
    /// <param name="records">The capture's records.</param>
    /// <param name="schema">The build's schema, which types each message's fields, or null.</param>
    /// <param name="readable">The readable name of each obfuscated message the mapping pairs, or null.</param>
    /// <param name="fields">Whether to write each message's fields.</param>
    public static void Write(TextWriter output, List<CaptureRecord> records, SchemaMatch? schema, IReadOnlyDictionary<string, string>? readable, bool fields)
    {
        var indexed = records.Select((record, index) => (Record: record, Index: (long)index)).ToArray();
        var messages = Extract(indexed);
        var byName = schema?.Messages.ToDictionary(message => message.FullName, StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var message in messages)
        {
            var label = message.TypeName is null ? "no type URL" : readable?.GetValueOrDefault(message.TypeName) is { } known ? $"{message.TypeName} {known}" : message.TypeName;

            counts[label] = counts.GetValueOrDefault(label) + 1;
            output.WriteLine($"    {message.Offset:hh\\:mm\\:ss\\.fff} {TextSideName(message.SideOrder)} {message.Member} {label}, {message.Content.Length} bytes");

            if (!fields || message.TypeName is null)
                continue;

            var schemaMessage = byName?.GetValueOrDefault(message.TypeName);

            if (schemaMessage is null && byName is not null)
                output.WriteLine("      not in the schema");

            ProtobufShape.Write(output, message.Content, 3, schemaMessage);
        }

        output.WriteLine($"  {messages.Count} frames, {counts.Count} messages: "
            + string.Join(", ", counts.OrderByDescending(count => count.Value).ThenBy(count => count.Key, StringComparer.Ordinal).Select(count => $"{count.Key} {count.Value}")));
    }

    private static (string Member, string? TypeUrl, string? TypeName, byte[] Content) Content(byte[] frame)
    {
        if (!ProtobufShape.TryFields(frame, out var members))
            return ("not protobuf", null, null, []);

        foreach (var (number, wireType, start, length) in members)
        {
            var (member, anyNumber) = number switch
            {
                1 => ("response", 2),
                3 => ("event", 1),
                4 => ("request", 2),
                _ => (string.Empty, 0),
            };

            if (anyNumber is 0 || wireType is not 2)
                continue;

            var body = frame.AsSpan(start, length);

            if (!ProtobufShape.TryFields(body, out var bodyFields))
                return (member, null, null, []);

            foreach (var (fieldNumber, fieldWire, fieldStart, fieldLength) in bodyFields)
            {
                if (fieldNumber != anyNumber || fieldWire is not 2)
                    continue;

                return Any(member, body.Slice(fieldStart, fieldLength));
            }

            return (member, null, null, []);
        }

        return ("unknown member", null, null, []);
    }

    private static (string Member, string? TypeUrl, string? TypeName, byte[] Content) Any(string member, ReadOnlySpan<byte> any)
    {
        if (!ProtobufShape.TryFields(any, out var fields))
            return (member, null, null, []);

        string? typeUrl = null;
        var content = Array.Empty<byte>();

        foreach (var (number, wireType, start, length) in fields)
        {
            if (number is 1 && wireType is 2)
                typeUrl = Encoding.ASCII.GetString(any.Slice(start, length));
            else if (number is 2 && wireType is 2)
                content = any.Slice(start, length).ToArray();
        }

        var typeName = typeUrl is null
            ? null
            : typeUrl.StartsWith(TypeUrlPrefix, StringComparison.Ordinal) ? typeUrl[TypeUrlPrefix.Length..] : typeUrl;

        return (member, typeUrl, typeName, content);
    }

    private static string TextSideName(int sideOrder)
    {
        return sideOrder switch
        {
            0 => "client",
            1 => "server",
            _ => "D3B   ",
        };
    }
}
