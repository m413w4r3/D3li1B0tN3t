using System.Buffers;
using System.IO.Pipelines;
using D3B.Network.Captures;
using D3B.Network.Framing;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Network.Tests.Captures;

public sealed class CaptureReplayTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private static async Task<byte[]> ReadToEndAsync(PipeReader reader)
    {
        using var bytes = new MemoryStream();

        while (true)
        {
            var result = await reader.ReadAsync(TestContext.Current.CancellationToken);

            foreach (var segment in result.Buffer)
                bytes.Write(segment.Span);

            reader.AdvanceTo(result.Buffer.End);

            if (result.IsCompleted)
                break;
        }

        await reader.CompleteAsync();

        return bytes.ToArray();
    }

    private static async Task<List<byte[]>> ReadFramesAsync(PipeReader reader)
    {
        var frames = new List<byte[]>();

        while (true)
        {
            var result = await reader.ReadAsync(TestContext.Current.CancellationToken);
            var buffer = result.Buffer;
            var consumed = Frames(buffer, frames);

            reader.AdvanceTo(consumed, buffer.End);

            if (result.IsCompleted)
            {
                Assert.True(buffer.Slice(consumed).IsEmpty, "The replay ended within a frame.");

                break;
            }
        }

        await reader.CompleteAsync();

        return frames;
    }

    private static SequencePosition Frames(ReadOnlySequence<byte> buffer, List<byte[]> frames)
    {
        var reader = new SequenceReader<byte>(buffer);

        while (FrameReader.TryReadFrame(ref reader, out var frame))
            frames.Add(frame.ToArray());

        return reader.Position;
    }

    [Fact]
    public async Task AReplayGivesEachSideItsBytesInOrder()
    {
        using var reader = new CaptureReader(new MemoryStream(CaptureTests.Session(new FakeTimeProvider())));
        var fromClient = new Pipe();
        var fromServer = new Pipe();

        var replay = CaptureReplay.PlayAsync(reader.ReadAll(), fromClient.Writer, fromServer.Writer, TestContext.Current.CancellationToken);
        var client = ReadToEndAsync(fromClient.Reader);
        var server = ReadToEndAsync(fromServer.Reader);

        await replay.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], await client);
        Assert.Equal([4, 5, 6, .. new byte[300]], await server);
    }

    [Fact]
    public async Task WhatD3BSentIsReplayedOnlyWhereItIsAskedFor()
    {
        var capture = new MemoryStream();
        var header = new CaptureHeader { StartedAt = DateTimeOffset.UnixEpoch, ClientVersion = "3.7.12.12", Listener = string.Empty, ServerHost = string.Empty, Server = string.Empty };

        using (var writer = new CaptureWriter(capture, header, new FakeTimeProvider()))
        {
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>([1, 2]));
            writer.Write(CaptureRecordKind.Injected, new ReadOnlySequence<byte>([1, 9]));
            writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>([1, 3]));
        }

        using var reader = new CaptureReader(new MemoryStream(capture.ToArray()));
        var records = reader.ReadAll().ToList();
        var fromClient = new Pipe();
        var fromServer = new Pipe();
        var injected = new Pipe();

        var replay = CaptureReplay.PlayAsync(records, fromClient.Writer, fromServer.Writer, injected.Writer, TestContext.Current.CancellationToken);
        var client = ReadToEndAsync(fromClient.Reader);
        var server = ReadToEndAsync(fromServer.Reader);
        var d3b = ReadToEndAsync(injected.Reader);

        await replay.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], await client);
        Assert.Equal([1, 3], await server);
        Assert.Equal([1, 9], await d3b);

        // Replayed through the relay, which sends its own, they are left out.
        var withoutInjected = new Pipe();

        await CaptureReplay.PlayAsync(records, withoutInjected.Writer, new Pipe().Writer, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], await ReadToEndAsync(withoutInjected.Reader));
    }

    [Fact]
    public async Task ASessionBuiltByATestReplaysAsTheFramesItWasMadeOf()
    {
        var random = new Random(59);
        var frames = Enumerable.Range(0, 40).Select(index => new byte[random.Next(index % 4 is 0 ? 2_000 : 200)]).ToList();

        foreach (var frame in frames)
            random.NextBytes(frame);

        var written = new ArrayBufferWriter<byte>();

        foreach (var frame in frames)
            FrameWriter.Write(written, frame);

        // The server's bytes cut at random, as reads of the network cut them, lengths and frames split anywhere.
        using var capture = new MemoryStream();
        var time = new FakeTimeProvider();

        using (var writer = new CaptureWriter(capture, new CaptureHeader { StartedAt = time.GetUtcNow(), ClientVersion = string.Empty, Listener = string.Empty, ServerHost = "connection.test", Server = string.Empty }, time))
        {
            var bytes = written.WrittenMemory;

            while (!bytes.IsEmpty)
            {
                var length = Math.Min(bytes.Length, random.Next(1, 700));

                writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(bytes[..length]));
                bytes = bytes[length..];
            }

            writer.WriteClosed(CaptureRecordKind.ServerClosed);
        }

        using var reader = new CaptureReader(new MemoryStream(capture.ToArray()));
        var fromClient = new Pipe();
        var fromServer = new Pipe();

        var replay = CaptureReplay.PlayAsync(reader.ReadAll(), fromClient.Writer, fromServer.Writer, TestContext.Current.CancellationToken);
        var read = ReadFramesAsync(fromServer.Reader);

        await fromClient.Reader.CompleteAsync();
        await replay.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(frames, await read);
    }
}
