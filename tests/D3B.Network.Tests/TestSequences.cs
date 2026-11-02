using System.Buffers;

namespace D3B.Network.Tests;

/// <summary>Byte sequences cut into segments, as pipes hand out what the network cut.</summary>
internal static class TestSequences
{
    /// <summary>Two segments, cut at <paramref name="split"/>; either may be empty.</summary>
    public static ReadOnlySequence<byte> Split(byte[] bytes, int split)
    {
        return Of([bytes[..split], bytes[split..]]);
    }

    /// <summary>Segments of <paramref name="size"/> bytes, the last shorter.</summary>
    public static ReadOnlySequence<byte> Segments(byte[] bytes, int size)
    {
        return Of([.. bytes.Chunk(size)]);
    }

    public static ReadOnlySequence<byte> Of(IReadOnlyList<byte[]> parts)
    {
        if (parts.Count is 0)
            return ReadOnlySequence<byte>.Empty;

        var first = new Segment(parts[0], 0);
        var last = first;

        foreach (var part in parts.Skip(1))
            last = last.Append(part);

        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory, long runningIndex)
        {
            Memory = memory;
            RunningIndex = runningIndex;
        }

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory, RunningIndex + Memory.Length);

            Next = next;

            return next;
        }
    }
}
