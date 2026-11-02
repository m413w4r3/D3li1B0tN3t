using System.Net;
using System.Net.Sockets;
using D3B.Network.Relay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Network.Tests.Relay;

/// <summary>The relay on a port of its own on D3B's address, and a server on 127.0.0.1 that it relays every client to.</summary>
internal sealed class TestRelay : IAsyncDisposable
{
    public const string Host = "connection.test";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly ServiceProvider _services;
    private readonly RelayServer _server;
    private readonly TcpListener _upstream;
    private readonly IPEndPoint _upstreamEndPoint;

    public FakeLogCollector Log { get; }

    public IPEndPoint EndPoint => _server.EndPoints[0];

    public IPEndPoint GameEndPoint => _server.GameEndPoints[0];

    public int UpstreamPort => _upstreamEndPoint.Port;

    public GameServerSelections Selections => _services.GetRequiredService<GameServerSelections>();

    private TestRelay(ServiceProvider services, TcpListener upstream, IPEndPoint upstreamEndPoint, FakeLogCollector log)
    {
        _services = services;
        Log = log;
        _server = services.GetRequiredService<RelayServer>();
        _upstream = upstream;
        _upstreamEndPoint = upstreamEndPoint;
    }

    /// <summary>Starts the relay, which sends every client to the test's server, or to none when it has none, and shows an interceptor its connections.</summary>
    /// <param name="withServer">Whether the relay has a server to send clients to.</param>
    /// <param name="capturesDirectory">Where captures go, or null when none is recorded.</param>
    /// <param name="interceptor">What watches the connections.</param>
    /// <param name="findingGameServers">Whether game connections find their server through the selections, as the host's do, rather than the test's server always.</param>
    /// <param name="holdTimeout">How long a frame is held, half a second by default.</param>
    /// <param name="refuseUpstream">Whether the selected server should have no listener.</param>
    public static async Task<TestRelay> StartAsync(bool withServer = true, string? capturesDirectory = null, IRelayInterceptor? interceptor = null, bool findingGameServers = false, TimeSpan? holdTimeout = null, bool refuseUpstream = false)
    {
        var upstream = new TcpListener(IPAddress.Loopback, 0);

        upstream.Start();
        var upstreamEndPoint = (IPEndPoint)upstream.LocalEndpoint;

        if (refuseUpstream)
            upstream.Stop();

        var settings = new RelaySettings
        {
            Address = RelaySettings.DefaultAddress,
            Ports = [0],
            GamePorts = [0],
            ClientDirectory = Path.Combine(AppContext.BaseDirectory, "no-client"),
            RedirectDirectory = Path.Combine(AppContext.BaseDirectory, "no-redirect"),
            CapturesDirectory = capturesDirectory,
            CloseTimeout = TimeSpan.FromSeconds(2),
            HoldTimeout = holdTimeout ?? TimeSpan.FromMilliseconds(500),
        };
        var log = new FakeLogCollector();
        var collection = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(new FakeLoggerProvider(log)))
            .AddRelay(settings, _ => "3.7.12.12");

        if (!findingGameServers)
            collection.AddSingleton<IUpstreamLocator>(new FixedUpstreamLocator(withServer ? new Upstream(Host, [upstreamEndPoint]) : null));

        if (interceptor is not null)
            collection.AddSingleton(interceptor);

        var services = collection.BuildServiceProvider();
        var relay = new TestRelay(services, upstream, upstreamEndPoint, log);

        await relay._server.StartAsync(TestContext.Current.CancellationToken);

        return relay;
    }

    public static async Task<byte[]> ReadToEndAsync(Socket socket)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[8_192];

        while (true)
        {
            var read = await socket.ReceiveAsync(buffer, TestContext.Current.CancellationToken).AsTask().WaitAsync(_timeout, TestContext.Current.CancellationToken);

            if (read is 0)
                return bytes.ToArray();

            bytes.Write(buffer, 0, read);
        }
    }

    public static async Task<byte[]> ReadExactlyAsync(Socket socket, int count)
    {
        var bytes = new byte[count];
        var read = 0;

        while (read < count)
        {
            var received = await socket.ReceiveAsync(bytes.AsMemory(read), TestContext.Current.CancellationToken).AsTask().WaitAsync(_timeout, TestContext.Current.CancellationToken);

            if (received is 0)
                throw new EndOfStreamException($"The connection closed after {read} of {count} bytes.");

            read += received;
        }

        return bytes;
    }

    /// <summary>Connects a client to the relay, and returns it with the server's side of its connection.</summary>
    public async Task<(Socket Client, Socket Server)> ConnectAsync()
    {
        var client = await ConnectToAsync(EndPoint);
        var server = await AcceptAsync();

        return (client, server);
    }

    /// <summary>Connects a client to the relay's endpoint.</summary>
    public static async Task<Socket> ConnectToAsync(IPEndPoint endPoint)
    {
        var client = new Socket(SocketType.Stream, ProtocolType.Tcp);

        await client.ConnectAsync(endPoint, TestContext.Current.CancellationToken);

        return client;
    }

    /// <summary>Accepts the relay's connection to the test's server.</summary>
    public async Task<Socket> AcceptAsync()
    {
        return await _upstream.AcceptSocketAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(_timeout, TestContext.Current.CancellationToken);
    }

    /// <summary>Waits until the relay has logged a message.</summary>
    public async Task WaitForLogAsync(Func<FakeLogRecord, bool> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(_timeout);

        while (!Log.GetSnapshot().Any(predicate))
            await Task.Delay(20, timeout.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.StopAsync(CancellationToken.None);
        await _services.DisposeAsync();
        _upstream.Dispose();
    }
}
