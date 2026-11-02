using System.Net;

namespace D3B.Network.Relay;

/// <summary>Finds the real server behind a connection the relay accepted.</summary>
internal interface IUpstreamLocator
{
    /// <summary>Finds the server a client meant to reach when it connected to a listener of the relay.</summary>
    /// <param name="route">Which of the client's connections it is.</param>
    /// <param name="listener">The relay's address and port the client connected to.</param>
    /// <param name="clientFrame">
    /// For the game, the client's first frame, which presents its ticket; empty for the connection phase, whose server is
    /// found before the client speaks.
    /// </param>
    /// <param name="cancellationToken">Stops the search, such as when the client leaves.</param>
    /// <returns>The server.</returns>
    /// <exception cref="UpstreamUnavailableException">No server can be found: the message says why.</exception>
    Task<Upstream> LocateAsync(RelayRoute route, IPEndPoint listener, ReadOnlyMemory<byte> clientFrame, CancellationToken cancellationToken);
}
