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

    /// <summary>Writes a game capture's messages, one frame a line, with their fields when asked.</summary>
    /// <param name="output">Where to write.</param>
    /// <param name="records">The capture's records.</param>
    /// <param name="schema">The build's schema, which types each message's fields, or null.</param>
    /// <param name="readable">The readable name of each obfuscated message the mapping pairs, or null.</param>
    /// <param name="fields">Whether to write each message's fields.</param>
    public static void Write(TextWriter output, List<CaptureRecord> records, SchemaMatch? schema, IReadOnlyDictionary<string, string>? readable, bool fields)
    {
        var frames = new List<(TimeSpan Offset, int Side, byte[] Frame)>();

        foreach (var (kind, side) in new[] { (CaptureRecordKind.FromClient, 0), (CaptureRecordKind.FromServer, 1), (CaptureRecordKind.Injected, 2) })
            frames.AddRange(FramesOf(records, kind).Select(frame => (frame.Offset, side, frame.Frame)));

        var byName = schema?.Messages.ToDictionary(message => message.FullName, StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var index = 0;

        foreach (var (offset, side, frame) in frames.OrderBy(frame => frame.Offset).ThenBy(frame => frame.Side))
        {
            index++;

            var (member, name, content) = Content(frame);
            var label = name is null ? "no type URL" : readable?.GetValueOrDefault(name) is { } known ? $"{name} {known}" : name;

            counts[label] = counts.GetValueOrDefault(label) + 1;
            output.WriteLine($"    {offset:hh\\:mm\\:ss\\.fff} {SideName(side)} {member} {label}, {content.Length} bytes");

            if (!fields || name is null)
                continue;

            var message = byName?.GetValueOrDefault(name);

            if (message is null && byName is not null)
                output.WriteLine("      not in the schema");

            ProtobufShape.Write(output, content, 3, message);
        }

        output.WriteLine($"  {index} frames, {counts.Count} messages: "
            + string.Join(", ", counts.OrderByDescending(count => count.Value).ThenBy(count => count.Key, StringComparer.Ordinal).Select(count => $"{count.Key} {count.Value}")));
    }

    private static IEnumerable<(TimeSpan Offset, byte[] Frame)> FramesOf(List<CaptureRecord> records, CaptureRecordKind kind)
    {
        var pending = new List<byte>();

        foreach (var record in records.Where(record => record.Kind == kind))
        {
            pending.AddRange(record.Data.ToArray());

            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(pending.ToArray()));
            var whole = new List<byte[]>();

            while (FrameReader.TryReadFrame(ref reader, out var frame))
                whole.Add(frame.ToArray());

            pending.RemoveRange(0, (int)reader.Consumed);

            foreach (var frame in whole)
                yield return (record.Offset, frame);
        }
    }

    // The envelope's members, by the numbers mapping 1 measured (D56): a response, an event or a request, each holding
    // its Any at its own number.
    private static (string Member, string? Name, byte[] Content) Content(byte[] frame)
    {
        if (!ProtobufShape.TryFields(frame, out var members))
            return ("not protobuf", null, []);

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
                return (member, null, []);

            foreach (var (fieldNumber, fieldWire, fieldStart, fieldLength) in bodyFields)
            {
                if (fieldNumber != anyNumber || fieldWire is not 2)
                    continue;

                return Any(member, body.Slice(fieldStart, fieldLength));
            }

            return (member, null, []);
        }

        return ("unknown member", null, []);
    }

    private static (string Member, string? Name, byte[] Content) Any(string member, ReadOnlySpan<byte> any)
    {
        if (!ProtobufShape.TryFields(any, out var fields))
            return (member, null, []);

        string? name = null;
        var content = Array.Empty<byte>();

        foreach (var (number, wireType, start, length) in fields)
        {
            if (number is 1 && wireType is 2)
            {
                var url = Encoding.ASCII.GetString(any.Slice(start, length));

                name = url.StartsWith(TypeUrlPrefix, StringComparison.Ordinal) ? url[TypeUrlPrefix.Length..] : url;
            }
            else if (number is 2 && wireType is 2)
            {
                content = any.Slice(start, length).ToArray();
            }
        }

        return (member, name, content);
    }

    private static string SideName(int side)
    {
        return side switch
        {
            0 => "client",
            1 => "server",
            _ => "D3B   ",
        };
    }
}
