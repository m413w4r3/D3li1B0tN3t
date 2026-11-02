using D3B.Network.Clients;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace D3B.Network.Relay;

/// <summary>Registers the relay in a host's services.</summary>
public static class RelayServiceCollectionExtensions
{
    /// <summary>Registers the relay: Kestrel on D3B's loopback address, and the client sent there, with the host (D35, D36).</summary>
    /// <remarks>
    /// The relay listens before the client is sent to it, and the client is given its own configuration back before the
    /// relay stops: the Generic Host stops its services in the reverse order it started them. An
    /// <see cref="IRelayInterceptor"/> registered in the services watches every connection relayed, and may rewrite the
    /// frames it holds (D49); <see cref="GameServerSelections"/> takes the game servers whose selection it rewrote.
    /// </remarks>
    /// <param name="services">The host's services.</param>
    /// <param name="settings">Where the relay listens, where it keeps the client's configuration, and whether it records captures.</param>
    /// <param name="clientVersion">Gets the installed client's version, which captures record, or null when unknown.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddRelay(this IServiceCollection services, RelaySettings settings, Func<IServiceProvider, string?>? clientVersion = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(settings);
        services.AddSingleton<ClientRedirect>();
        services.AddSingleton<GameServerSelections>();
        services.AddSingleton<IUpstreamLocator, UpstreamLocator>();

        // Owns the memory pool of the servers' connections; the container disposes it with the host.
        services.AddSingleton(provider => new SocketConnectionContextFactory(
            new SocketConnectionFactoryOptions(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<SocketConnectionContextFactory>()));
        services.AddSingleton(provider => new RelayConnectionHandler(
            provider.GetRequiredService<RelaySettings>(),
            provider.GetRequiredService<IUpstreamLocator>(),
            provider.GetRequiredService<SocketConnectionContextFactory>(),
            provider.GetRequiredService<TimeProvider>(),
            () => clientVersion?.Invoke(provider),
            provider.GetService<IRelayInterceptor>(),
            provider.GetRequiredService<ILogger<RelayConnectionHandler>>()));
        services.AddSingleton<RelayServer>();
        services.AddHostedService(provider => provider.GetRequiredService<RelayServer>());
        services.AddHostedService(provider => provider.GetRequiredService<ClientRedirect>());

        return services;
    }
}
