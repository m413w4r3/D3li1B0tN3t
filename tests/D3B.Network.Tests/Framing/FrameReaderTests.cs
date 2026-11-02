using System.Buffers;
using D3B.Network.Framing;

namespace D3B.Network.Tests.Framing;

public sealed class FrameReaderTests
{
    private static readonly int[] _lengths = [0, 1, 127, 128, 300, 16_384, 70_000];

    private static byte[][] Payloads()
    {
        var random = new Random(3);

        return [.. _lengths.Select(length =>
        {
            var payload = new byte[length];

            random.NextBytes(payload);

            return payload;
        })];
    }

    private static byte[] Stream(IEnumerable<byte[]> payloads)
    {
        var writer = new ArrayBufferWriter<byte>();

        foreach (var payload in payloads)
            FrameWriter.Write(writer, payload);

        return writer.WrittenSpan.ToArray();
    }

    private static List<byte[]> ReadAll(ReadOnlySequence<byte> bytes, out long consumed)
    {
        var reader = new SequenceReader<byte>(bytes);
        var frames = new List<byte[]>();

        while (FrameReader.TryReadFrame(ref reader, out var frame))
            frames.Add(frame.ToArray());

        consumed = reader.Consumed;

        return frames;
    }

    [Fact]
    public void AFrameIsReadWithoutItsLength()
    {
        var frames = ReadAll(new ReadOnlySequence<byte>([3, 10, 20, 30]), out var consumed);

        Assert.Equal([10, 20, 30], Assert.Single(frames));
        Assert.Equal(4, consumed);
    }

    [Fact]
    public void FramesAreReadInOrderAndMayBeEmpty()
    {
        var payloads = Payloads();

        var frames = ReadAll(new ReadOnlySequence<byte>(Stream(payloads)), out _);

        Assert.Equal(payloads, frames);
    }

    [Fact]
    public void NothingIsConsumedUntilAFrameIsWhole()
    {
        var stream = Stream([new byte[300]]);

        // Every prefix: a length cut short, a length whole but its bytes not.
        for (var length = 0; length < stream.Length; length++)
        {
            var frames = ReadAll(new ReadOnlySequence<byte>(stream, 0, length), out var consumed);

            Assert.Empty(frames);
            Assert.Equal(0, consumed);
        }
    }

    [Fact]
    public void EverySplitOfAStreamGivesTheSameFrames()
    {
        var payloads = Payloads().Take(5).ToArray();
        var stream = Stream(payloads);

        for (var split = 0; split <= stream.Length; split++)
        {
            var frames = ReadAll(TestSequences.Split(stream, split), out var consumed);

            Assert.Equal(payloads, frames);
            Assert.Equal(stream.Length, consumed);
        }
    }

    [Fact]
    public void FramesReadTheSameWhenEveryByteIsItsOwnSegment()
    {
        var payloads = Payloads();
        var stream = Stream(payloads);

        Assert.Equal(payloads, ReadAll(TestSequences.Segments(stream, 1), out _));
    }

    [Fact]
    public void ALengthMayTakeFiveBytes()
    {
        // Three, written on five bytes: longer than needed, as protobuf readers accept it.
        var frames = ReadAll(new ReadOnlySequence<byte>([0x83, 0x80, 0x80, 0x80, 0x00, 1, 2, 3]), out _);

        Assert.Equal([1, 2, 3], Assert.Single(frames));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x1F })]
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x00 })]
    public void ALengthBeyondThirtyTwoBitsIsNoLength(byte[] bytes)
    {
        var exception = Assert.Throws<FrameFormatException>(() => ReadAll(new ReadOnlySequence<byte>(bytes), out _));

        Assert.Contains("32 bits", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFrameLongerThanAllowedIsRejectedBeforeItsBytesArrive()
    {
        var writer = new ArrayBufferWriter<byte>();

        FrameWriter.Write(writer, new byte[1_001]);

        var exception = Assert.Throws<FrameFormatException>(() =>
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(writer.WrittenMemory[..2]));

            FrameReader.TryReadFrame(ref reader, out _, 1_000);
        });

        Assert.Contains("is longer than the", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(127, 1)]
    [InlineData(128, 2)]
    [InlineData(16_383, 2)]
    [InlineData(16_384, 3)]
    [InlineData(FrameReader.DefaultMaxLength, 4)]
    [InlineData(int.MaxValue, 5)]
    public void ALengthTakesOneByteForEverySevenBits(int length, int size)
    {
        Assert.Equal(size, FrameWriter.LengthSize(length));
    }

    [Fact]
    public void FrameWriterUsesProtobufVarintWireOrder()
    {
        var payload = Enumerable.Range(0, 130).Select(value => (byte)value).ToArray();
        var writer = new ArrayBufferWriter<byte>();
        var expected = new byte[payload.Length + 2];

        expected[0] = 0x82;
        expected[1] = 0x01;
        payload.CopyTo(expected, 2);

        FrameWriter.Write(writer, payload);

        Assert.Equal(expected, writer.WrittenSpan.ToArray());
    }
}
