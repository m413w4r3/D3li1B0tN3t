using System.Net;
using System.Net.Sockets;
using D3B.Network.Clients;

namespace D3B.Network.Relay;

/// <summary>Finds the server behind a connection: for the connection phase, the connection host Ankama's configuration names (D35); for the game, the game server named by the selection whose ticket the client presents (3f).</summary>
/// <remarks>
/// The host is resolved at each connection, as the client would: the connection host and the game servers measured are
/// load balancers, whose addresses may change. The connection phase's port is the one the client chose, since D3B listens
/// on the same ports as the connection host; the game's is the real port standing behind the game listener's.
/// </remarks>
internal sealed class UpstreamLocator : IUpstreamLocator
{
    private readonly ClientRedirect _redirect;
    private readonly GameServerSelections _selections;

    public UpstreamLocator(ClientRedirect redirect, GameServerSelections selections)
    {
        _redirect = redirect;
        _selections = selections;
    }

    public async Task<Upstream> LocateAsync(RelayRoute route, IPEndPoint listener, ReadOnlyMemory<byte> clientFrame, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listener);

        string host;
        int port;

        if (route is RelayRoute.Game)
        {
            if (!_selections.TryTake(clientFrame.Span, listener.Port, out var gameHost, out port))
                throw new UpstreamUnavailableException("No server selection D3B rewrote waits for the ticket the client presents.");

            host = gameHost;
        }
        else
        {
            host = _redirect.ConnectionHost?.Host ?? throw new UpstreamUnavailableException("The client's configuration does not go to D3B yet.");
            port = listener.Port;
        }

        IPAddress[] addresses;

        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new UpstreamUnavailableException($"{host} cannot be resolved: {exception.Message}", exception);
        }

        if (addresses.Length is 0)
            throw new UpstreamUnavailableException($"{host} has no address.");

        return new Upstream(host, [.. addresses.Select(address => new IPEndPoint(address, port))]);
    }
}
