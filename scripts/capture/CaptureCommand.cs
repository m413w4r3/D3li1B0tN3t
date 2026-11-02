using System.Buffers;
using System.CommandLine;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Network.Relay;
using D3B.Protocol.Mapping;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Scripts.Capture;

/// <summary>The command that describes captures by their structure: reads, frames, sizes and shapes, never content.</summary>
internal static class CaptureCommand
{
    public static RootCommand Create()
    {
        var paths = new Argument<string[]>("paths")
        {
            Description = @"Capture files, or directories of them. Defaults to %LOCALAPPDATA%\D3B\captures, where the host records them.",
            Arity = ArgumentArity.ZeroOrMore,
        };
        var frames = new Option<bool>("--frames")
        {
            Description = "Also shows the shape of every frame: protobuf field numbers, wire types and lengths, never values.",
        };
        var schema = new Option<FileInfo?>("--schema")
        {
            Description = @"A schema scripts/schema.cs wrote, such as %LOCALAPPDATA%\D3B\data\<version>\schema-1.binpb: says which of its messages read every frame, and with --frames the type and name of every field there.",
        };
        var messages = new Option<bool>("--messages")
        {
            Description = "Lists the messages a game connection's frames carry, by their type URLs, in the order and at the time they came; with --frames and --schema, the fields of each as the schema types them.",
        };
        var mapping = new Option<FileInfo?>("--mapping")
        {
            Description = @"A build's mapping, such as %LOCALAPPDATA%\D3B\data\<version>\mapping-<format>.binpb: with --messages, also names each message the readable protocol pairs.",
        };
        var command = new RootCommand("Describes D3B's captures of relayed connections by their structure, never their content (D37).")
        {
            paths,
            frames,
            schema,
            messages,
            mapping,
        };

        command.SetAction(result => Run(result.GetValue(paths) ?? [], result.GetValue(frames), result.GetValue(schema)?.FullName, result.GetValue(messages), result.GetValue(mapping)?.FullName));

        return command;
    }

    private static int Run(string[] paths, bool frames, string? schemaPath, bool messages, string? mappingPath)
    {
        var files = (paths.Length is 0 ? [RelaySettings.DefaultCapturesDirectory] : paths)
            .SelectMany(path => Directory.Exists(path) ? Directory.GetFiles(path, "*" + CaptureWriter.FileExtension).Order(StringComparer.Ordinal).ToArray() : [path])
            .ToList();

        if (files.Count is 0)
        {
            Console.Error.WriteLine("No capture to describe. The host records them with --Network:Captures:Enabled true.");

            return 2;
        }

        SchemaMatch? schema = null;
        MessageDescriptor? root = null;

        if (schemaPath is not null)
        {
            try
            {
                schema = SchemaMatch.Load(schemaPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidProtocolBufferException or DescriptorValidationException)
            {
                Console.Error.WriteLine($"{schemaPath}: {exception.Message}");

                return 1;
            }

            if (!messages)
                root = Root(Console.Out, schema, schemaPath, files);
        }

        Dictionary<string, string>? readable = null;

        if (mappingPath is not null)
        {
            try
            {
                readable = MappingFile.Read(mappingPath).Messages.ToDictionary(pair => pair.Obfuscated, pair => pair.Readable.Replace("d3b.game.", string.Empty, StringComparison.Ordinal), StringComparer.Ordinal);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidProtocolBufferException or InvalidDataException)
            {
                Console.Error.WriteLine($"{mappingPath}: {exception.Message}");

                return 1;
            }
        }

        var exitCode = 0;

        foreach (var file in files)
        {
            try
            {
                if (messages)
                    DescribeMessages(Console.Out, file, frames, schema, readable);
                else
                    Describe(Console.Out, file, frames, schema, root);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"{file}: {exception.Message}");
                exitCode = 1;
            }
        }

        return exitCode;
    }

    private static MessageDescriptor? Root(TextWriter output, SchemaMatch schema, string schemaPath, List<string> files)
    {
        var frames = new List<byte[]>();

        foreach (var file in files)
        {
            try
            {
                frames.AddRange(FramesOf(file));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FrameFormatException)
            {
                // Described with its error below; the frames of the other captures still say what reads them.
            }
        }

        var readers = new List<(MessageDescriptor Message, int Typed)>();

        foreach (var message in schema.Messages)
        {
            var typed = 0;

            foreach (var frame in frames)
            {
                if (SchemaMatch.Typed(message, frame) is not int count)
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

        var shown = frames.Sum(frame => ProtobufShape.CountFields(frame));
        var best = readers.Count > 0 ? readers[0] : default;
        var unique = readers.Count is 1 || (readers.Count > 1 && readers[1].Typed < best.Typed);

        output.Write($"{Path.GetFileName(schemaPath)}: {schema.Messages.Count} messages, {readers.Count} of which read all {frames.Count} frames of the captures, both sides");
        output.WriteLine(readers.Count is 0
            ? string.Empty
            : $"; .{best.Message.FullName} types the most fields, {best.Typed}, where their shapes show {shown}"
                + (readers.Count > 1 ? $"; the next, .{readers[1].Message.FullName}, types {readers[1].Typed}" : string.Empty));

        return unique ? best.Message : null;
    }

    private static IEnumerable<byte[]> FramesOf(string path)
    {
        using var reader = CaptureReader.Open(path);

        var records = reader.ReadAll().ToList();

        foreach (var kind in new[] { CaptureRecordKind.FromClient, CaptureRecordKind.FromServer, CaptureRecordKind.Injected })
        {
            foreach (var frame in Frames([.. records.Where(record => record.Kind == kind).SelectMany(record => record.Data.ToArray())]))
                yield return frame;
        }
    }

    private static void Describe(TextWriter output, string path, bool frames, SchemaMatch? schema, MessageDescriptor? root)
    {
        using var reader = CaptureReader.Open(path);

        var records = reader.ReadAll().ToList();
        var header = reader.Header;
        var last = records.Count > 0 ? records[^1].Offset : TimeSpan.Zero;

        output.WriteLine($"{Path.GetFileName(path)}: client {(header.ClientVersion.Length > 0 ? header.ClientVersion : "unknown")}, "
            + $"listener port {Port(header.Listener)}, server port {Port(header.Server)}, {records.Count} records over {last:hh\\:mm\\:ss\\.fff}{(reader.IsTruncated ? ", cut short" : string.Empty)}");
        output.WriteLine($"  order: {string.Join(", ", records.Take(16).Select(Name))}{(records.Count > 16 ? ", ..." : string.Empty)}");

        foreach (var (kind, side) in new[] { (CaptureRecordKind.FromClient, "client"), (CaptureRecordKind.FromServer, "server") })
            DescribeSide(output, records.Where(record => record.Kind == kind).ToList(), side, frames, schema, root);

        // Format 2 on: the frames D3B sent the server itself, each a record of its own (D61).
        var injected = records.Where(record => record.Kind is CaptureRecordKind.Injected).ToList();

        if (injected.Count > 0)
            DescribeSide(output, injected, "relay itself, to the server", frames, schema, root);
    }

    private static void DescribeMessages(TextWriter output, string path, bool frames, SchemaMatch? schema, IReadOnlyDictionary<string, string>? readable)
    {
        using var reader = CaptureReader.Open(path);

        var records = reader.ReadAll().ToList();
        var last = records.Count > 0 ? records[^1].Offset : TimeSpan.Zero;

        output.WriteLine($"{Path.GetFileName(path)}: client {(reader.Header.ClientVersion.Length > 0 ? reader.Header.ClientVersion : "unknown")}, {records.Count} records over {last:hh\\:mm\\:ss\\.fff}{(reader.IsTruncated ? ", cut short" : string.Empty)}");

        try
        {
            GameMessages.Write(output, records, schema, readable, frames);
        }
        catch (FrameFormatException exception)
        {
            output.WriteLine($"  no varint frames: {exception.Message}");
        }
    }

    private static void DescribeSide(TextWriter output, List<CaptureRecord> reads, string side, bool frames, SchemaMatch? schema, MessageDescriptor? root)
    {
        var bytes = reads.SelectMany(read => read.Data.ToArray()).ToArray();

        output.Write($"  from the {side}: {reads.Count} reads, {bytes.Length} bytes");

        if (bytes.Length is 0)
        {
            output.WriteLine();

            return;
        }

        // A TLS connection opens with a handshake record: type 22, then version 3.x.
        output.Write(bytes.Length > 2 && bytes[0] is 0x16 && bytes[1] is 0x03 ? ", TLS" : ", not TLS");

        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));
        var sizes = new List<int>();
        var ends = new HashSet<long>();
        using var shapes = new StringWriter();

        try
        {
            while (FrameReader.TryReadFrame(ref reader, out var frame))
            {
                sizes.Add((int)frame.Length);
                ends.Add(reader.Consumed);

                if (frames)
                {
                    var data = frame.ToArray();
                    var (frameReader, reading) = schema is null ? (null, string.Empty) : ReaderOf(schema, root, data);

                    shapes.WriteLine($"    frame {sizes.Count}, {frame.Length} bytes{reading}:");
                    ProtobufShape.Write(shapes, data, 3, frameReader);
                }
            }
        }
        catch (FrameFormatException exception)
        {
            output.WriteLine($"; no varint frame at byte {reader.Consumed}: {exception.Message}");

            return;
        }

        var offset = 0L;
        var aligned = reads.Count(read => ends.Contains(offset += read.Data.Length));
        var messages = sizes.Count is 0 ? 0 : Frames(bytes).Count(frame => ProtobufShape.IsMessage(frame));
        var read = root is null ? string.Empty : $", {Frames(bytes).Count(frame => SchemaMatch.Accepts(root, frame))} read as .{root.FullName}";

        output.WriteLine($"; {sizes.Count} varint frames{(sizes.Count > 0 ? $" of {sizes.Min()} to {sizes.Max()} bytes" : string.Empty)}, "
            + $"{bytes.Length - reader.Consumed} bytes left over, {messages} parse as protobuf{read}, {aligned} of {reads.Count} reads end on a frame");
        output.Write(shapes.ToString());
    }

    private static (MessageDescriptor? Reader, string Reading) ReaderOf(SchemaMatch schema, MessageDescriptor? root, byte[] frame)
    {
        if (root is not null && SchemaMatch.Accepts(root, frame))
            return (root, $", read as .{root.FullName}");

        var readers = schema.Accepting(frame);

        return readers.Count is 1
            ? (readers[0], $", read as .{readers[0].FullName}, the only message of the schema that reads it")
            : (null, $", read by {readers.Count} messages of the schema");
    }

    private static List<byte[]> Frames(byte[] bytes)
    {
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));
        var frames = new List<byte[]>();

        while (FrameReader.TryReadFrame(ref reader, out var frame))
            frames.Add(frame.ToArray());

        return frames;
    }

    private static string Name(CaptureRecord record)
    {
        return record.Kind switch
        {
            CaptureRecordKind.FromClient => $"client {record.Data.Length}",
            CaptureRecordKind.FromServer => $"server {record.Data.Length}",
            CaptureRecordKind.Injected => $"D3B {record.Data.Length}",
            CaptureRecordKind.ClientClosed => "client closed",
            _ => "server closed",
        };
    }

    private static string Port(string endPoint)
    {
        var separator = endPoint.LastIndexOf(':');

        return separator < 0 ? "unknown" : endPoint[(separator + 1)..];
    }
}
