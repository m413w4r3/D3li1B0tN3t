using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>Measures anchors in captures of a build, by the structure of their frames only (D43).</summary>
/// <remarks>
/// The schema's root is the one message of its file that reads every frame and types the most of their fields. Each
/// frame holds one field of the root: the field a side's first frame holds pairs the plan's field for that side, and
/// frames holding another, such as events, are passed over. In the n-th frame holding it, one member of that message's
/// oneofs pairs the plan's n-th exchange, and the message its <c>Any</c> carries, which its type URL names, pairs the
/// plan's payload of that frame. Every pair is checked against every frame: one contradicting another is reported, and
/// left out. Every <c>Any</c> of every frame must name a message of the file that reads its value whole.
/// </remarks>
public static class CaptureAnchors
{
    /// <summary>Reads the frames of captured connections with a file of a build's schema and a plan.</summary>
    /// <param name="connections">The captured connections, of the build the schema describes.</param>
    /// <param name="file">The schema's file the connection's messages are in.</param>
    /// <param name="plan">What the readable protocol says the frames hold.</param>
    public static AnchorMeasurement Measure(IReadOnlyList<CapturedConnection> connections, FileDescriptor file, AnchorPlan plan)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(plan);

        var frames = connections.SelectMany(connection => connection.ClientFrames.Concat(connection.ServerFrames)).ToList();
        var problems = new List<string>();

        if (frames.Count is 0)
            return new AnchorMeasurement(0, null, 0, -1, [], [], ["No frame to read."]);

        var readers = new List<(MessageDescriptor Message, int Typed)>();

        foreach (var message in Descriptors.Messages(file.MessageTypes).Where(message => !Descriptors.IsMapEntry(message)))
        {
            var typed = 0;

            foreach (var frame in frames)
            {
                var count = WireReading.Typed(message, frame.Span);

                if (count < 0)
                {
                    typed = -1;

                    break;
                }

                typed += count;
            }

            if (typed >= 0)
                readers.Add((message, typed));
        }

        readers.Sort((left, right) => right.Typed.CompareTo(left.Typed));

        var next = readers.Count > 1 ? readers[1].Typed : -1;

        if (readers.Count is 0 || next == readers[0].Typed)
        {
            problems.Add(readers.Count is 0
                ? $"No message of {file.Name} reads every frame."
                : $"{readers.Count(reader => reader.Typed == readers[0].Typed)} messages of {file.Name} read every frame and type as many of their fields: none is the root alone.");

            return new AnchorMeasurement(frames.Count, null, readers.Count is 0 ? 0 : readers[0].Typed, next, [], [], problems);
        }

        var root = readers[0].Message;
        var reading = new AnchorReading(plan, file, root, problems);

        foreach (var connection in connections)
        {
            reading.ReadSide(connection.ClientFrames, plan.ClientField, true);
            reading.ReadSide(connection.ServerFrames, plan.ServerField, false);
        }

        return new AnchorMeasurement(frames.Count, root, readers[0].Typed, next, reading.Anchors(), reading.Payloads(), problems);
    }
}
