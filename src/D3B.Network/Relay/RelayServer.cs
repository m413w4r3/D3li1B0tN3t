using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace D3B.Network.Relay;

/// <summary>Kestrel, listening on D3B's loopback address, each connection handed to the relay (D8, D23, D36).</summary>
/// <remarks>
/// <para>
/// A hosted service of the Generic Host that D3B's bot builder wraps: the host has no web host, so the relay runs
/// Kestrel's server itself, on Kestrel's socket transport, with one connection handler per endpoint and no HTTP.
/// </para>
/// <para>
/// Stopping closes the listeners, asks every relayed connection to close, and waits for them within the host's
/// shutdown time.
/// </para>
/// </remarks>
internal sealed class RelayServer : IHostedService, IDisposable
{
    private readonly RelaySettings _settings;
    private readonly GameServerSelections _selections;
    private readonly KestrelServer _server;
    private readonly List<ListenOptions> _listeners = [];
    private readonly List<ListenOptions> _gameListeners = [];
    private readonly ILogger _log;

    /// <summary>Gets the connection phase's endpoints, with the ports chosen when the settings ask for port 0.</summary>
    public IReadOnlyList<IPEndPoint> EndPoints => [.. _listeners.Select(listener => listener.IPEndPoint!)];

    /// <summary>Gets the game listener's endpoints, with the ports chosen when the settings ask for port 0.</summary>
    public IReadOnlyList<IPEndPoint> GameEndPoints => [.. _gameListeners.Select(listener => listener.IPEndPoint!)];

    public RelayServer(RelaySettings settings, GameServerSelections selections, IServiceProvider services, ILoggerFactory loggers)
    {
        _settings = settings;
        _selections = selections;
        _log = loggers.CreateLogger<RelayServer>();

        var options = new KestrelServerOptions { ApplicationServices = services, AddServerHeader = false };
        var handler = services.GetRequiredService<RelayConnectionHandler>();

        foreach (var (ports, route, listeners) in new[] { (settings.Ports, RelayRoute.Connection, _listeners), (settings.GamePorts, RelayRoute.Game, _gameListeners) })
        {
            foreach (var port in ports)
            {
                options.Listen(settings.Address, port, listener =>
                {
                    listener.Protocols = HttpProtocols.Http1;
                    listener.Run(connection => handler.RelayAsync(connection, route));
                    listeners.Add(listener);
                });
            }
        }

        var transport = new SocketTransportFactory(Options.Create(new SocketTransportOptions()), loggers);

        _server = new KestrelServer(Options.Create(options), transport, loggers);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _server.StartAsync(new NoHttpApplication(), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or SocketException)
        {
            // Kestrel reports a port in use as an IOException, and any other refusal, such as a port Windows reserves, as is.
            throw new RelayUnavailableException(
                $"D3B cannot listen on {_settings.Address} on ports {string.Join(", ", _settings.Ports.Concat(_settings.GamePorts))}: {exception.Message} "
                + $"Another program may hold the port there: set {RelaySettings.AddressKey} to another address of 127.0.0.0/8, or {RelaySettings.GamePortsKey} to other ports.",
                exception);
        }

        foreach (var endPoint in EndPoints)
            RelayLog.Listening(_log, endPoint);

        foreach (var endPoint in GameEndPoints)
            RelayLog.ListeningForGame(_log, endPoint);

        _selections.OnListening([.. GameEndPoints.Select(endPoint => endPoint.Port)]);

        if (_settings.CapturesDirectory is { } directory)
            RelayLog.Recording(_log, directory);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _server.StopAsync(cancellationToken);
    }

    public void Dispose()
    {
        _server.Dispose();
    }
}
