using System.Text;
using D3B.Data.Tests;
using D3B.Network.Captures;
using Microsoft.Extensions.Logging;

namespace D3B.Network.Tests.Relay;

public sealed class GameRelayTests
{
    private const string Ticket = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task AGameConnectionGoesToTheServerWhoseTicketItPresents()
    {
        using var directory = new TemporaryDirectory();
        byte[] sent = [.. TestFrames.Framed(TestFrames.Presenting(Ticket)), .. TestFrames.Framed(Encoding.UTF8.GetBytes("next"))];
        var answered = TestFrames.Framed(TestFrames.Number(1, 42));

        await using (var relay = await TestRelay.StartAsync(capturesDirectory: directory.Path, findingGameServers: true))
        {
            Assert.Equal([relay.GameEndPoint.Port], relay.Selections.Add(Ticket, "localhost", [relay.UpstreamPort, 443]));

            using var client = await TestRelay.ConnectToAsync(relay.GameEndPoint);

            await client.SendAsync(sent, TestContext.Current.CancellationToken);

            using (var server = await relay.AcceptAsync())
            {
                // The frame read for its ticket goes first, then what followed it.
                Assert.Equal(sent, await TestRelay.ReadExactlyAsync(server, sent.Length));

                await server.SendAsync(answered, TestContext.Current.CancellationToken);

                Assert.Equal(answered, await TestRelay.ReadExactlyAsync(client, answered.Length));
            }

            Assert.Empty(await TestRelay.ReadToEndAsync(client));
            await relay.WaitForLogAsync(record => record.Message.Contains(" closed after ", StringComparison.Ordinal));
            Assert.Contains(relay.Log.GetSnapshot(), record => record.Message.Contains("relayed to localhost at 127.0.0.1", StringComparison.Ordinal) && record.Message.EndsWith(": the game", StringComparison.Ordinal));
        }

        // Apart from the connection phase's, which the mapping tool reads.
        Assert.Empty(Directory.GetFiles(directory.Path, "*" + CaptureWriter.FileExtension));

        using var reader = CaptureReader.Open(Assert.Single(Directory.GetFiles(Path.Combine(directory.Path, "game"), "*" + CaptureWriter.FileExtension)));
        var records = reader.ReadAll().ToList();

        Assert.Equal("localhost", reader.Header.ServerHost);
        Assert.Equal(sent, records.Where(record => record.Kind is CaptureRecordKind.FromClient).SelectMany(record => record.Data.ToArray()));
        Assert.Equal(answered, records.Where(record => record.Kind is CaptureRecordKind.FromServer).SelectMany(record => record.Data.ToArray()));
    }

    [Fact]
    public async Task AGameConnectionPresentingNoTicketWaitingIsClosed()
    {
        await using var relay = await TestRelay.StartAsync(findingGameServers: true);

        relay.Selections.Add(Ticket, "localhost", [relay.UpstreamPort]);

        using var client = await TestRelay.ConnectToAsync(relay.GameEndPoint);

        await client.SendAsync(TestFrames.Framed(TestFrames.Presenting("ffffffffffffffffffffffffffffffff")), TestContext.Current.CancellationToken);

        Assert.Empty(await TestRelay.ReadToEndAsync(client));
        await relay.WaitForLogAsync(record => record.Level is LogLevel.Warning
            && record.Message.Contains("No server selection D3B rewrote waits for the ticket the client presents.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AGameConnectionSendingNoWholeFrameInTimeIsClosed()
    {
        await using var relay = await TestRelay.StartAsync(findingGameServers: true);

        relay.Selections.Add(Ticket, "localhost", [relay.UpstreamPort]);

        using var client = await TestRelay.ConnectToAsync(relay.GameEndPoint);

        // The start of a frame, then nothing: as if the server, not the client, were to speak first.
        await client.SendAsync(TestFrames.Framed(TestFrames.Presenting(Ticket)).AsMemory(0, 10), TestContext.Current.CancellationToken);

        Assert.Empty(await TestRelay.ReadToEndAsync(client));
        await relay.WaitForLogAsync(record => record.Message.Contains("The client sent no whole frame within 0.5 s", StringComparison.Ordinal));
    }
}
