using System.Buffers;
using System.Net.Sockets;
using D3B.Data.Tests;
using D3B.Network.Captures;
using D3B.Network.Framing;
using Microsoft.Extensions.Logging;

namespace D3B.Network.Tests.Relay;

public sealed class RelayTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private static byte[] Bytes(int length, int seed)
    {
        var bytes = new byte[length];

        new Random(seed).NextBytes(bytes);

        return bytes;
    }

    [Fact]
    public async Task BytesAreRelayedAsTheyAreBothWays()
    {
        await using var relay = await TestRelay.StartAsync();
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var fromClient = Bytes(200_000, 1);
            var fromServer = Bytes(300_000, 2);

            // Both sides speak at once; neither closes before it has heard the other, since a close ends both ways.
            var serverReceives = TestRelay.ReadExactlyAsync(server, fromClient.Length);
            var clientReceives = TestRelay.ReadExactlyAsync(client, fromServer.Length);

            await Task.WhenAll(
                client.SendAsync(fromClient, TestContext.Current.CancellationToken).AsTask(),
                server.SendAsync(fromServer, TestContext.Current.CancellationToken).AsTask());

            Assert.Equal(fromClient, await serverReceives);
            Assert.Equal(fromServer, await clientReceives);
        }
    }

    [Fact]
    public async Task AFrameSplitAcrossSocketWritesIsRelayedByteForByte()
    {
        await using var relay = await TestRelay.StartAsync();
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var payload = Bytes(300, 3);
            var writer = new ArrayBufferWriter<byte>();

            FrameWriter.Write(writer, payload);
            var frame = writer.WrittenSpan.ToArray();

            await client.SendAsync(frame.AsMemory(0, 1), TestContext.Current.CancellationToken);

            // Receiving the first varint byte proves it crossed the relay before the rest is written.
            var firstByte = await TestRelay.ReadExactlyAsync(server, 1);

            await client.SendAsync(frame.AsMemory(1), TestContext.Current.CancellationToken);
            var rest = await TestRelay.ReadExactlyAsync(server, frame.Length - firstByte.Length);
            var relayed = firstByte.Concat(rest).ToArray();

            Assert.Equal(frame, relayed);
        }
    }

    [Fact]
    public async Task SeveralClientsAreRelayedAtOnce()
    {
        await using var relay = await TestRelay.StartAsync();
        var connections = new List<(Socket Client, Socket Server)>();

        // Every client connects before any speaks, so that all are relayed at the same time.
        for (var index = 0; index < 8; index++)
            connections.Add(await relay.ConnectAsync());

        try
        {
            // The server answers each client with what it was sent, reversed: no connection may get another's bytes.
            var exchanges = connections.Select(async (connection, index) =>
            {
                var sent = Bytes(50_000 + index, index);

                await connection.Client.SendAsync(sent, TestContext.Current.CancellationToken);

                var received = await TestRelay.ReadExactlyAsync(connection.Server, sent.Length);

                await connection.Server.SendAsync(received.Reverse().ToArray(), TestContext.Current.CancellationToken);

                return (Sent: sent, Answer: await TestRelay.ReadExactlyAsync(connection.Client, sent.Length));
            });

            foreach (var (sent, answer) in await Task.WhenAll(exchanges).WaitAsync(_timeout, TestContext.Current.CancellationToken))
                Assert.Equal(sent.Reverse(), answer);
        }
        finally
        {
            foreach (var (client, server) in connections)
            {
                client.Dispose();
                server.Dispose();
            }
        }
    }

    [Fact]
    public async Task TheServerClosingClosesTheClientOnceItsLastBytesAreThere()
    {
        await using var relay = await TestRelay.StartAsync();
        var (client, server) = await relay.ConnectAsync();

        using (client)
        {
            var clientReceives = TestRelay.ReadToEndAsync(client);

            using (server)
                await server.SendAsync("bye"u8.ToArray(), TestContext.Current.CancellationToken);

            Assert.Equal("bye"u8.ToArray(), await clientReceives);
        }
    }

    [Fact]
    public async Task TheClientClosingClosesTheServer()
    {
        await using var relay = await TestRelay.StartAsync();
        var (client, server) = await relay.ConnectAsync();

        using (server)
        {
            var serverReceives = TestRelay.ReadToEndAsync(server);

            using (client)
                await client.SendAsync("hello"u8.ToArray(), TestContext.Current.CancellationToken);

            Assert.Equal("hello"u8.ToArray(), await serverReceives);
        }

        await relay.WaitForLogAsync(record => record.Message.Contains("5 bytes from the client, 0 from the server", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AClientWithNoServerToGoToIsClosed()
    {
        await using var relay = await TestRelay.StartAsync(withServer: false);
        using var client = new Socket(SocketType.Stream, ProtocolType.Tcp);

        await client.ConnectAsync(relay.EndPoint, TestContext.Current.CancellationToken);

        Assert.Empty(await TestRelay.ReadToEndAsync(client));
        await relay.WaitForLogAsync(record => record.Message.Contains("no server to relay it to. No line sends a host to D3B.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnUpstreamConnectionRefusalClosesTheClient()
    {
        await using var relay = await TestRelay.StartAsync(refuseUpstream: true);
        using var client = await TestRelay.ConnectToAsync(relay.EndPoint);

        Assert.Empty(await TestRelay.ReadToEndAsync(client));

        await relay.WaitForLogAsync(record => record.Message.Contains("closed: no server to relay it to. No address of connection.test answered.", StringComparison.Ordinal));
        Assert.Contains(relay.Log.GetSnapshot(), record => record.Message.Contains("did not answer", StringComparison.Ordinal));
        Assert.DoesNotContain(relay.Log.GetSnapshot(), record => record.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task EachConnectionIsRecordedWhenCapturesAreOn()
    {
        using var directory = new TemporaryDirectory();
        var sent = Bytes(100_000, 3);
        var answered = Bytes(70_000, 4);

        await using (var relay = await TestRelay.StartAsync(capturesDirectory: directory.Path))
        {
            var (client, server) = await relay.ConnectAsync();

            using (server)
            {
                using (client)
                {
                    await client.SendAsync(sent, TestContext.Current.CancellationToken);
                    await TestRelay.ReadExactlyAsync(server, sent.Length);
                    await server.SendAsync(answered, TestContext.Current.CancellationToken);
                    await TestRelay.ReadExactlyAsync(client, answered.Length);
                }

                Assert.Empty(await TestRelay.ReadToEndAsync(server));
            }

            await relay.WaitForLogAsync(record => record.Message.Contains(" closed after ", StringComparison.Ordinal));
        }

        var path = Assert.Single(Directory.GetFiles(directory.Path, "*" + CaptureWriter.FileExtension));

        using var reader = CaptureReader.Open(path);
        var records = reader.ReadAll().ToList();

        Assert.Equal(TestRelay.Host, reader.Header.ServerHost);
        Assert.Equal("3.7.12.12", reader.Header.ClientVersion);
        Assert.False(reader.IsTruncated);
        Assert.Equal(sent, records.Where(record => record.Kind is CaptureRecordKind.FromClient).SelectMany(record => record.Data.ToArray()));
        Assert.Equal(answered, records.Where(record => record.Kind is CaptureRecordKind.FromServer).SelectMany(record => record.Data.ToArray()));
        Assert.Contains(records, record => record.Kind is CaptureRecordKind.ClientClosed);
        Assert.Contains(records, record => record.Kind is CaptureRecordKind.ServerClosed);
    }
}
