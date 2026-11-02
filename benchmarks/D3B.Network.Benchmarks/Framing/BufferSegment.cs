using System.Buffers;

namespace D3B.Network.Benchmarks.Framing;

/// <summary>A segment of a byte sequence, as a pipe chains the buffers a socket filled.</summary>
internal sealed class BufferSegment : ReadOnlySequenceSegment<byte>
{
    public BufferSegment(ReadOnlyMemory<byte> memory, long runningIndex)
    {
        Memory = memory;
        RunningIndex = runningIndex;
    }

    public BufferSegment Append(ReadOnlyMemory<byte> memory)
    {
        var next = new BufferSegment(memory, RunningIndex + Memory.Length);

        Next = next;

        return next;
    }
}
