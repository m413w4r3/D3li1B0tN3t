using System.Buffers;
using BenchmarkDotNet.Attributes;
using D3B.Network.Framing;

namespace D3B.Network.Benchmarks.Framing;

/// <summary>Reads 10,000 frames, of the sizes the connection phase measured and longer, whole or cut as socket reads cut them.</summary>
[MemoryDiagnoser]
public class FrameReaderBenchmarks
{
    private const int FrameCount = 10_000;

    // The connection phase's frames (client.md), and one longer, as a map's would be.
    private static readonly int[] _sizes = [10, 83, 126, 455, 4_000];

    private ReadOnlySequence<byte> _whole;
    private ReadOnlySequence<byte> _cut;

    [Params(4_096, 512)]
    public int SegmentSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var writer = new ArrayBufferWriter<byte>();

        for (var index = 0; index < FrameCount; index++)
            FrameWriter.Write(writer, new byte[_sizes[index % _sizes.Length]]);

        var bytes = writer.WrittenMemory;
        var first = new BufferSegment(bytes[..SegmentSize], 0);
        var last = first;

        for (var start = SegmentSize; start < bytes.Length; start += SegmentSize)
            last = last.Append(bytes[start..Math.Min(bytes.Length, start + SegmentSize)]);

        _whole = new ReadOnlySequence<byte>(bytes);
        _cut = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    [Benchmark(Baseline = true)]
    public long OneSegment()
    {
        return Read(_whole);
    }

    [Benchmark]
    public long Segments()
    {
        return Read(_cut);
    }

    private static long Read(ReadOnlySequence<byte> bytes)
    {
        var reader = new SequenceReader<byte>(bytes);
        var length = 0L;

        while (FrameReader.TryReadFrame(ref reader, out var frame))
            length += frame.Length;

        return length;
    }
}
