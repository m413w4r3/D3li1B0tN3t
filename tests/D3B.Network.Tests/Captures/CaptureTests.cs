using System.Buffers;
using D3B.Data.Tests;
using D3B.Network.Captures;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Network.Tests.Captures;

public sealed class CaptureTests
{
    private static readonly CaptureHeader _header = new()
    {
        StartedAt = new DateTimeOffset(2026, 10, 27, 20, 0, 0, 123, TimeSpan.Zero),
        ClientVersion = "3.7.12.12",
        Listener = "127.0.13.59:5555",
        ServerHost = "connection.test",
        Server = "192.0.2.1:5555",
    };

    /// <summary>A capture of a short session: the client speaks, the server answers in two reads, then both close.</summary>
    public static byte[] Session(FakeTimeProvider time)
    {
        using var stream = new MemoryStream();

        using (var writer = new CaptureWriter(stream, _header, time))
        {
            time.Advance(TimeSpan.FromMilliseconds(1));
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>([1, 2, 3]));
            time.Advance(TimeSpan.FromMicroseconds(250));
            writer.Write(CaptureRecordKind.FromServer, TestSequences.Of([[4, 5], [6]]));
            writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(new byte[300]));
            time.Advance(TimeSpan.FromSeconds(2));
            writer.WriteClosed(CaptureRecordKind.ServerClosed);
            writer.WriteClosed(CaptureRecordKind.ClientClosed);
        }

        return stream.ToArray();
    }

    private static (List<CaptureRecord> Records, bool Truncated) ReadAll(byte[] capture)
    {
        using var reader = new CaptureReader(new MemoryStream(capture));
        var records = reader.ReadAll().ToList();

        return (records, reader.IsTruncated);
    }

    private static int HeaderLength()
    {
        var stream = new MemoryStream();

        using (new CaptureWriter(stream, _header, new FakeTimeProvider()))
        {
        }

        return stream.ToArray().Length;
    }

    [Fact]
    public void ACaptureReadsBackAsItWasWritten()
    {
        var capture = Session(new FakeTimeProvider());

        using var reader = new CaptureReader(new MemoryStream(capture));
        var records = reader.ReadAll().ToList();

        Assert.Equal(_header, reader.Header);
        Assert.False(reader.IsTruncated);
        Assert.Equal(
            [
                (CaptureRecordKind.FromClient, TimeSpan.FromMicroseconds(1_000), 3),
                (CaptureRecordKind.FromServer, TimeSpan.FromMicroseconds(1_250), 3),
                (CaptureRecordKind.FromServer, TimeSpan.FromMicroseconds(1_250), 300),
                (CaptureRecordKind.ServerClosed, TimeSpan.FromMicroseconds(2_001_250), 0),
                (CaptureRecordKind.ClientClosed, TimeSpan.FromMicroseconds(2_001_250), 0),
            ],
            records.Select(record => (record.Kind, record.Offset, record.Data.Length)));
        Assert.Equal([4, 5, 6], records[1].Data.ToArray());
    }

    [Fact]
    public void ACaptureCutShortReadsUpToItsLastWholeRecord()
    {
        var time = new FakeTimeProvider();
        var ends = new List<long>();
        byte[] capture;

        // Each record is flushed as it is written, so the stream's length after each is where it ends.
        using (var stream = new MemoryStream())
        {
            using (var writer = new CaptureWriter(stream, _header, time))
            {
                ends.Add(stream.Length);
                writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>([1, 2, 3]));
                ends.Add(stream.Length);
                time.Advance(TimeSpan.FromSeconds(1));
                writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(new byte[300]));
                ends.Add(stream.Length);
                writer.WriteClosed(CaptureRecordKind.ServerClosed);
                ends.Add(stream.Length);
            }

            capture = stream.ToArray();
        }

        for (var length = (int)ends[0]; length <= capture.Length; length++)
        {
            var (records, truncated) = ReadAll(capture[..length]);

            Assert.Equal(ends.Count(end => end <= length) - 1, records.Count);
            Assert.Equal(!ends.Contains(length), truncated);
        }
    }

    [Fact]
    public void AFileThatIsNoCaptureIsRejected()
    {
        var exception = Assert.Throws<CaptureFormatException>(() => new CaptureReader(new MemoryStream("D3B-PACK and more"u8.ToArray())));

        Assert.Contains("not a D3B capture", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACaptureOfAnotherFormatIsRejected()
    {
        var capture = Session(new FakeTimeProvider());

        capture[8] = 3;

        var exception = Assert.Throws<CaptureFormatException>(() => new CaptureReader(new MemoryStream(capture)));

        Assert.Contains("format 3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACaptureOfFormat1StillReads()
    {
        var capture = Session(new FakeTimeProvider());

        capture[8] = 1;

        using var reader = new CaptureReader(new MemoryStream(capture));

        Assert.Equal(1, reader.FormatVersion);
        Assert.Equal(5, reader.ReadAll().Count());
    }

    [Fact]
    public void WhatD3BSentReadsBackAsItWasWritten()
    {
        using var stream = new MemoryStream();

        using (var writer = new CaptureWriter(stream, _header, new FakeTimeProvider()))
        {
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>([2, 9, 9]));
            writer.Write(CaptureRecordKind.Injected, new ReadOnlySequence<byte>([1, 7]));
        }

        using var reader = new CaptureReader(new MemoryStream(stream.ToArray()));
        var records = reader.ReadAll().ToList();

        Assert.Equal(2, reader.FormatVersion);
        Assert.Equal([CaptureRecordKind.FromClient, CaptureRecordKind.Injected], records.Select(record => record.Kind));
        Assert.Equal([1, 7], records[1].Data.ToArray());
    }

    [Fact]
    public void WhatD3BSentIsNoRecordOfFormat1()
    {
        var capture = Session(new FakeTimeProvider());

        capture[8] = 1;
        capture[HeaderLength()] = (byte)CaptureRecordKind.Injected;

        using var reader = new CaptureReader(new MemoryStream(capture));

        Assert.Throws<CaptureFormatException>(() => reader.ReadAll().ToList());
    }

    [Fact]
    public void AHeaderCutShortIsRejected()
    {
        var capture = Session(new FakeTimeProvider());

        Assert.Throws<CaptureFormatException>(() => new CaptureReader(new MemoryStream(capture[..20])));
    }

    [Fact]
    public void ARecordOfNoKnownKindIsRejected()
    {
        var capture = Session(new FakeTimeProvider());

        capture[HeaderLength()] = 9;

        using var reader = new CaptureReader(new MemoryStream(capture));

        Assert.Throws<CaptureFormatException>(() => reader.ReadAll().ToList());
    }

    [Fact]
    public void AnOffsetBeyondAnyTimeIsADamagedRecord()
    {
        // A kind, then the largest offset a varint holds, far beyond what a TimeSpan counts in microseconds.
        byte[] capture = [.. new byte[HeaderLength()], 1, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0];

        Session(new FakeTimeProvider()).AsSpan(0, HeaderLength()).CopyTo(capture);

        using var reader = new CaptureReader(new MemoryStream(capture));

        Assert.Throws<CaptureFormatException>(() => reader.ReadAll().ToList());
    }

    [Fact]
    public void AWriteThatFailsEndsTheCaptureWithoutThrowing()
    {
        using var stream = new FailingStream(capacity: 120);
        using var writer = new CaptureWriter(stream, _header, new FakeTimeProvider());

        writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(new byte[200]));
        writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>([1]));
        writer.WriteClosed(CaptureRecordKind.ClientClosed);

        Assert.NotNull(writer.Failure);
    }

    [Fact]
    public void ACaptureFileIsCreatedInItsDirectory()
    {
        using var directory = new TemporaryDirectory();

        var (writer, path) = CaptureWriter.Create(directory.Combine("captures"), "session", _header, new FakeTimeProvider());

        using (writer)
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>([7]));

        using var reader = CaptureReader.Open(path);

        Assert.Equal(directory.Combine("captures", "session.d3bcap"), path);
        Assert.Equal([7], Assert.Single(reader.ReadAll()).Data.ToArray());
    }
}
