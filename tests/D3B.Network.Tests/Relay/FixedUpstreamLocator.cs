using System.Net;
using D3B.Network.Relay;

namespace D3B.Network.Tests.Relay;

/// <summary>Sends every connection to one server, or to none, in place of the hosts line and DNS.</summary>
internal sealed class FixedUpstreamLocator : IUpstreamLocator
{
    private readonly Upstream? _upstream;

    public FixedUpstreamLocator(Upstream? upstream)
    {
        _upstream = upstream;
    }

    public Task<Upstream> LocateAsync(RelayRoute route, IPEndPoint listener, ReadOnlyMemory<byte> clientFrame, CancellationToken cancellationToken)
    {
        return _upstream is { } upstream
            ? Task.FromResult(upstream)
            : Task.FromException<Upstream>(new UpstreamUnavailableException("No line sends a host to D3B."));
    }
}
