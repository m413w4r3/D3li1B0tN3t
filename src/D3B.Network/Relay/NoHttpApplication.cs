using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;

namespace D3B.Network.Relay;

/// <summary>The HTTP application Kestrel asks for at start, which the relay never uses.</summary>
/// <remarks>
/// <see cref="Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServer.StartAsync"/> takes an HTTP application even when
/// every endpoint ends in a connection handler, as the relay's do: no request ever reaches this one.
/// </remarks>
internal sealed class NoHttpApplication : IHttpApplication<object>
{
    public object CreateContext(IFeatureCollection contextFeatures)
    {
        throw new NotSupportedException("The relay serves no HTTP.");
    }

    public Task ProcessRequestAsync(object context)
    {
        throw new NotSupportedException("The relay serves no HTTP.");
    }

    public void DisposeContext(object context, Exception? exception)
    {
    }
}
