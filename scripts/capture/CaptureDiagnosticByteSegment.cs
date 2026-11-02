using System.Buffers;

namespace D3B.Scripts.Capture;

internal sealed class CaptureDiagnosticByteSegment : ReadOnlySequenceSegment<byte>
{
    public CaptureDiagnosticByteSegment(ReadOnlyMemory<byte> memory)
    {
        Memory = memory;
    }

    public CaptureDiagnosticByteSegment Append(ReadOnlyMemory<byte> memory)
    {
        var next = new CaptureDiagnosticByteSegment(memory)
        {
            RunningIndex = RunningIndex + Memory.Length,
        };
        Next = next;

        return next;
    }
}
