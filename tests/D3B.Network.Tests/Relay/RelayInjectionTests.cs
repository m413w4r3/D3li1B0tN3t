using System.Net.Sockets;
using D3B.Data.Tests;
using D3B.Network.Captures;
using D3B.Network.Relay;

namespace D3B.Network.Tests.Relay;

public sealed class RelayInjectionTests
{
    private static byte[] Payload(int length, int seed)
    {
        var bytes = new byte[length];

        new Random(seed).NextBytes(bytes);

        return bytes;
    }

    private static async Task<IRelayConnection> ConnectionAsync(RecordingInterceptor interceptor)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        while (interceptor.Connections.Count is 0)
            await Task.Delay(10, timeout.Token);

        return interceptor.Connections[0];
    }

    [Fact]
    public async Task AFrameOfD3BsGoesToTheServerBetweenTheClientsFrames()
    {
        var interceptor = new RecordingInterceptor();
        var first = TestFrames.Framed(Payload(30, 1));
        var injected = Payload(40, 2);
        var second = TestFrames.Framed(Payload(50, 3));

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            await client.SendAsync(first, TestContext.Current.CancellationToken);
            Assert.Equal(first, await TestRelay.ReadExactlyAsync(server, first.Length));

            await (await ConnectionAsync(interceptor)).SendToServerAsync(injected, TestContext.Current.CancellationToken);
            await client.SendAsync(second, TestContext.Current.CancellationToken);

            byte[] expected = [.. TestFrames.Framed(injected), .. second];

            Assert.Equal(expected, await TestRelay.ReadExactlyAsync(server, expected.Length));
        }

        // The interceptor sees what the client sent, not what D3B did.
        Assert.Equal(2, interceptor.Frames.Count);
    }

    [Fact]
    public async Task AFrameSentWhileTheClientsFrameIsHalfGoneWaitsForItsEnd()
    {
        var interceptor = new RecordingInterceptor();
        var frame = TestFrames.Framed(Payload(300, 4));
        var injected = Payload(20, 5);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            await client.SendAsync(frame.AsMemory(0, 100), TestContext.Current.CancellationToken);
            Assert.Equal(frame[..100], await TestRelay.ReadExactlyAsync(server, 100));

            var sending = (await ConnectionAsync(interceptor)).SendToServerAsync(injected, TestContext.Current.CancellationToken);

            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(sending.IsCompleted);

            await client.SendAsync(frame.AsMemory(100), TestContext.Current.CancellationToken);
            await sending;

            byte[] expected = [.. frame[100..], .. TestFrames.Framed(injected)];

            Assert.Equal(expected, await TestRelay.ReadExactlyAsync(server, expected.Length));
        }
    }

    [Fact]
    public async Task FramesSentOneAfterAnotherGoInThatOrder()
    {
        var interceptor = new RecordingInterceptor();
        var frames = Enumerable.Range(0, 20).Select(index => Payload(10 + index, 100 + index)).ToList();

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var connection = await ConnectionAsync(interceptor);

            foreach (var frame in frames)
                await connection.SendToServerAsync(frame, TestContext.Current.CancellationToken);

            byte[] expected = [.. frames.SelectMany(TestFrames.Framed)];

            Assert.Equal(expected, await TestRelay.ReadExactlyAsync(server, expected.Length));
        }
    }

    [Fact]
    public async Task AFrameWithdrawnBeforeItWentNeverGoes()
    {
        var interceptor = new RecordingInterceptor();
        var frame = TestFrames.Framed(Payload(200, 6));
        var after = Payload(15, 7);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            await client.SendAsync(frame.AsMemory(0, 50), TestContext.Current.CancellationToken);
            Assert.Equal(frame[..50], await TestRelay.ReadExactlyAsync(server, 50));

            var connection = await ConnectionAsync(interceptor);

            using (var withdrawn = new CancellationTokenSource())
            {
                var sending = connection.SendToServerAsync(Payload(30, 8), withdrawn.Token);

                await withdrawn.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
            }

            await client.SendAsync(frame.AsMemory(50), TestContext.Current.CancellationToken);
            await connection.SendToServerAsync(after, TestContext.Current.CancellationToken);

            byte[] expected = [.. frame[50..], .. TestFrames.Framed(after)];

            Assert.Equal(expected, await TestRelay.ReadExactlyAsync(server, expected.Length));
        }
    }

    [Fact]
    public async Task AFrameCannotGoBetweenBytesThatAreNoFrames()
    {
        var interceptor = new RecordingInterceptor();

        // A length of five bytes above 32 bits: no frame can start so.
        byte[] bytes = [0xFF, 0xFF, 0xFF, 0xFF, 0x7F, .. Payload(100, 9)];

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            await client.SendAsync(bytes, TestContext.Current.CancellationToken);
            Assert.Equal(bytes, await TestRelay.ReadExactlyAsync(server, bytes.Length));

            var exception = await Assert.ThrowsAsync<RelayInjectionException>(async () => await (await ConnectionAsync(interceptor)).SendToServerAsync(Payload(10, 10), TestContext.Current.CancellationToken));

            Assert.Contains("no frames", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AFrameSentOnceTheConnectionClosedFails()
    {
        var interceptor = new RecordingInterceptor();

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();
        var connection = await ConnectionAsync(interceptor);

        using (server)
        {
            using (client)
                client.Shutdown(SocketShutdown.Both);

            await TestRelay.ReadToEndAsync(server);
        }

        await relay.WaitForLogAsync(record => record.Message.Contains("closed after", StringComparison.Ordinal));

        Assert.True(connection.Closed.IsCancellationRequested);
        await Assert.ThrowsAsync<RelayInjectionException>(() => connection.SendToServerAsync(Payload(10, 11), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheCaptureRecordsWhatD3BSentWhereItWent()
    {
        using var directory = new TemporaryDirectory();
        var interceptor = new RecordingInterceptor();
        var first = TestFrames.Framed(Payload(30, 12));
        var injected = Payload(25, 13);
        var second = TestFrames.Framed(Payload(35, 14));
        byte[] received;

        await using (var relay = await TestRelay.StartAsync(capturesDirectory: directory.Path, interceptor: interceptor))
        {
            var (client, server) = await relay.ConnectAsync();

            using (server)
            {
                using (client)
                {
                    await client.SendAsync(first, TestContext.Current.CancellationToken);
                    await TestRelay.ReadExactlyAsync(server, first.Length);
                    await (await ConnectionAsync(interceptor)).SendToServerAsync(injected, TestContext.Current.CancellationToken);
                    await client.SendAsync(second, TestContext.Current.CancellationToken);
                    client.Shutdown(SocketShutdown.Send);
                }

                received = [.. first, .. await TestRelay.ReadToEndAsync(server)];
            }

            await relay.WaitForLogAsync(record => record.Message.Contains("1 frames from D3B", StringComparison.Ordinal));
        }

        using var reader = CaptureReader.Open(Assert.Single(Directory.GetFiles(directory.Path, "*" + CaptureWriter.FileExtension)));
        var records = reader.ReadAll().ToList();

        Assert.Equal(2, reader.FormatVersion);
        Assert.Equal(TestFrames.Framed(injected), Assert.Single(records, record => record.Kind is CaptureRecordKind.Injected).Data.ToArray());

        // What the server got is what the client sent and what D3B did, in the order of their records.
        Assert.Equal(received, records.Where(record => record.Kind is CaptureRecordKind.FromClient or CaptureRecordKind.Injected).SelectMany(record => record.Data.ToArray()));
    }
}
