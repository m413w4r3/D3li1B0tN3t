using System.Buffers;
using System.Net.Sockets;
using D3B.Network.Framing;
using D3B.Network.Relay;
using Microsoft.Extensions.Logging;

namespace D3B.Network.Tests.Relay;

public sealed class RelayInterceptorTests
{
    private static byte[] Payload(int length, int seed)
    {
        var bytes = new byte[length];

        new Random(seed).NextBytes(bytes);

        return bytes;
    }

    private static byte[] Framed(params byte[][] payloads)
    {
        var writer = new ArrayBufferWriter<byte>();

        foreach (var payload in payloads)
            FrameWriter.Write(writer, payload);

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>A frame as a test compares it: arrays are equal only to themselves, their hexadecimal to any copy.</summary>
    private static (RelayDirection Direction, string Frame) Seen(RelayDirection direction, byte[] frame)
    {
        return (direction, Convert.ToHexString(frame));
    }

    /// <summary>Sends bytes in pieces, each its own write after a pause, so that the relay reads frames cut where they are.</summary>
    private static async Task SendInPiecesAsync(Socket socket, byte[] bytes, params int[] cuts)
    {
        var start = 0;

        foreach (var cut in cuts.Append(bytes.Length))
        {
            await socket.SendAsync(bytes.AsMemory(start, cut - start), TestContext.Current.CancellationToken);
            await Task.Delay(30, TestContext.Current.CancellationToken);
            start = cut;
        }
    }

    [Fact]
    public async Task FramesAreObservedWholeWhereverTheNetworkCutsThem()
    {
        var interceptor = new RecordingInterceptor();
        var fromClient = new[] { Payload(10, 1), Payload(300, 2), Payload(5_000, 3) };
        var fromServer = new[] { Payload(455, 4) };
        var clientBytes = Framed(fromClient);
        var serverBytes = Framed(fromServer);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var serverReceives = TestRelay.ReadExactlyAsync(server, clientBytes.Length);

            // Cut within the first frame's length, within the second frame, and right after the second.
            await SendInPiecesAsync(client, clientBytes, 1, 100, 313);

            Assert.Equal(clientBytes, await serverReceives);

            var clientReceives = TestRelay.ReadExactlyAsync(client, serverBytes.Length);

            await SendInPiecesAsync(server, serverBytes, 200);

            Assert.Equal(serverBytes, await clientReceives);
        }

        Assert.Equal(
            [.. fromClient.Select(frame => Seen(RelayDirection.FromClient, frame)), .. fromServer.Select(frame => Seen(RelayDirection.FromServer, frame))],
            interceptor.Frames.Select(frame => Seen(frame.Direction, frame.Frame)));
    }

    [Fact]
    public async Task AFrameLargerThanTheTransportsReadBufferIsObservedWhole()
    {
        var interceptor = new RecordingInterceptor();

        // Above the 1 MiB of Kestrel's read buffers: the frame comes in many reads, whose bytes are forwarded as they come.
        var payload = Payload(3 * 1024 * 1024, 10);
        var bytes = Framed(payload);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var serverReceives = TestRelay.ReadExactlyAsync(server, bytes.Length);

            await client.SendAsync(bytes, TestContext.Current.CancellationToken);

            Assert.Equal(bytes, await serverReceives);

            var clientReceives = TestRelay.ReadExactlyAsync(client, bytes.Length);

            await server.SendAsync(bytes, TestContext.Current.CancellationToken);

            Assert.Equal(bytes, await clientReceives);
        }

        Assert.Equal([Seen(RelayDirection.FromClient, payload), Seen(RelayDirection.FromServer, payload)], interceptor.Frames.Select(frame => Seen(frame.Direction, frame.Frame)));
    }

    [Fact]
    public async Task BytesThatAreNoFramesAreRelayedUnwatched()
    {
        var interceptor = new RecordingInterceptor();

        // A length of five bytes above 32 bits: no frame can start so.
        byte[] bytes = [0xFF, 0xFF, 0xFF, 0xFF, 0x7F, .. Payload(1_000, 5)];

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var serverReceives = TestRelay.ReadExactlyAsync(server, bytes.Length);

            await SendInPiecesAsync(client, bytes, 3, 500);

            Assert.Equal(bytes, await serverReceives);
        }

        Assert.Empty(interceptor.Frames);
        await relay.WaitForLogAsync(record => record.Level is LogLevel.Warning && record.Message.Contains("the client's bytes are no varint frames", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFrameCutShortByItsSideClosingIsStillRelayed()
    {
        var interceptor = new RecordingInterceptor();
        var whole = Payload(20, 6);
        var cut = Framed(Payload(100, 7))[..41];
        byte[] bytes = [.. Framed(whole), .. cut];

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (server)
        {
            var serverReceives = TestRelay.ReadToEndAsync(server);

            using (client)
                await SendInPiecesAsync(client, bytes, 30);

            Assert.Equal(bytes, await serverReceives);
        }

        Assert.Equal([Seen(RelayDirection.FromClient, whole)], interceptor.Frames.Select(frame => Seen(frame.Direction, frame.Frame)));
    }

    [Fact]
    public async Task TheStartOfAFrameGoesOnWithoutWaitingForTheRest()
    {
        var interceptor = new RecordingInterceptor();

        // As a stream that is no frames would read: a length of 100, then 40 bytes, and the side waits for an answer.
        var cut = Framed(Payload(100, 11))[..41];

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            await client.SendAsync(cut, TestContext.Current.CancellationToken);

            Assert.Equal(cut, await TestRelay.ReadExactlyAsync(server, cut.Length));
        }

        Assert.Empty(interceptor.Frames);
    }

    [Fact]
    public async Task AFailingInterceptorLeavesTheConnectionRelayed()
    {
        var bytes = Framed(Payload(50, 8), Payload(60, 9));

        await using var relay = await TestRelay.StartAsync(interceptor: new RecordingInterceptor { Fails = true });
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var serverReceives = TestRelay.ReadExactlyAsync(server, bytes.Length);

            await SendInPiecesAsync(client, bytes, 20);

            Assert.Equal(bytes, await serverReceives);
        }

        await relay.WaitForLogAsync(record => record.Level is LogLevel.Error && record.Message.Contains("the client's frames are relayed unwatched", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFrameHeldGoesOnOnceWholeAsTheInterceptorRewroteIt()
    {
        var before = Payload(30, 12);
        var held = Payload(83, 13);
        var rewritten = Payload(200, 14);
        var interceptor = new RecordingInterceptor { Rewrite = frame => frame.AsSpan().SequenceEqual(held) ? rewritten : null };
        var serverBytes = Framed(before, held);
        var expected = Framed(before, rewritten);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var clientReceives = TestRelay.ReadExactlyAsync(client, expected.Length);

            interceptor.Holding = RelayDirection.FromServer;

            // Cut within the held frame's length, then within the frame.
            await SendInPiecesAsync(server, serverBytes, 32, 60);

            Assert.Equal(expected, await clientReceives);
        }

        Assert.Equal([Seen(RelayDirection.FromServer, before), Seen(RelayDirection.FromServer, held)], interceptor.Frames.Select(frame => Seen(frame.Direction, frame.Frame)));
        Assert.Equal([true, true], interceptor.Held);
    }

    [Fact]
    public async Task AFrameHeldThatDoesNotComeWholeGoesOnAsItCameOnceTheHoldTimesOut()
    {
        var interceptor = new RecordingInterceptor { Holding = RelayDirection.FromServer, Rewrite = _ => [1, 2, 3] };
        var bytes = Framed(Payload(100, 15));

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            // As a stream that is no frames would read: a length of 100, then 40 bytes, and the side waits for an answer.
            await server.SendAsync(bytes.AsMemory(0, 41), TestContext.Current.CancellationToken);

            Assert.Equal(bytes[..41], await TestRelay.ReadExactlyAsync(client, 41));

            await server.SendAsync(bytes.AsMemory(41), TestContext.Current.CancellationToken);

            Assert.Equal(bytes[41..], await TestRelay.ReadExactlyAsync(client, bytes.Length - 41));
        }

        Assert.StartsWith("it was not whole after 0.5 s", Assert.Single(interceptor.Abandoned), StringComparison.Ordinal);

        // Once the way out is taken, the side holds nothing more: the frame was already on its way.
        Assert.Equal([false], interceptor.Held);
    }

    [Fact]
    public async Task AFrameHeldFromTheReadEndingTheFrameBeforeIsGivenItsOwnTime()
    {
        var first = Payload(60, 18);
        var second = Payload(60, 19);
        var interceptor = new RecordingInterceptor { Holding = RelayDirection.FromServer, Rewrite = frame => [.. frame, 0] };
        var bytes = Framed(first, second);
        var expected = Framed([.. first, 0], [.. second, 0]);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor, holdTimeout: TimeSpan.FromSeconds(1));
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var clientReceives = TestRelay.ReadExactlyAsync(client, expected.Length);

            // The second frame starts 0.7 s after the first, and is whole 0.5 s later: past the first's time, within its own.
            await server.SendAsync(bytes.AsMemory(0, 30), TestContext.Current.CancellationToken);
            await Task.Delay(700, TestContext.Current.CancellationToken);
            await server.SendAsync(bytes.AsMemory(30, 61), TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);
            await server.SendAsync(bytes.AsMemory(91), TestContext.Current.CancellationToken);

            Assert.Equal(expected, await clientReceives);
        }

        Assert.Empty(interceptor.Abandoned);
        Assert.Equal([true, true], interceptor.Held);
    }

    [Fact]
    public async Task AFrameLongerThanTheHeldLengthGoesOnAsItComes()
    {
        var interceptor = new RecordingInterceptor { Holding = RelayDirection.FromServer, Rewrite = _ => [1, 2, 3] };
        var bytes = Framed(Payload(70_000, 16));

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            await server.SendAsync(bytes.AsMemory(0, 1_000), TestContext.Current.CancellationToken);

            Assert.Equal(bytes[..1_000], await TestRelay.ReadExactlyAsync(client, 1_000));

            await server.SendAsync(bytes.AsMemory(1_000), TestContext.Current.CancellationToken);

            Assert.Equal(bytes[1_000..], await TestRelay.ReadExactlyAsync(client, bytes.Length - 1_000));
        }

        Assert.Equal([false], interceptor.Held);
        Assert.Empty(interceptor.Abandoned);
    }

    [Fact]
    public async Task AFrameHeldThatItsSideClosesOnGoesOnAsItCame()
    {
        var interceptor = new RecordingInterceptor { Holding = RelayDirection.FromServer, Rewrite = _ => [1, 2, 3] };
        var cut = Framed(Payload(100, 17))[..41];

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        {
            var clientReceives = TestRelay.ReadToEndAsync(client);

            using (server)
                await server.SendAsync(cut, TestContext.Current.CancellationToken);

            Assert.Equal(cut, await clientReceives);
        }

        Assert.StartsWith("its side closed", Assert.Single(interceptor.Abandoned), StringComparison.Ordinal);
        Assert.Empty(interceptor.Frames);
    }
}
