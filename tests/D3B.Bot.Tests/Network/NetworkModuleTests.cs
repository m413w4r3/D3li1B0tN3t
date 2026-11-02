using System.Net;
using D3B.Bot.Network;
using D3B.Network.Relay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.Bot.Tests.Network;

public sealed class NetworkModuleTests : IDisposable
{
    private readonly TestInstall _install = new();

    public void Dispose()
    {
        _install.Dispose();
    }

    [Fact]
    public void TheRelayListensWhereTheConfigurationSays()
    {
        // The relay's interceptor decodes through the build's translation, which the relay asks for once it is made.
        _install.WriteProtocol();

        using var host = _install.Bot("--Network:Address", "127.0.13.60", "--Network:Captures:Enabled", "true").AddModule<NetworkModule>().Build();

        var settings = host.Services.GetRequiredService<RelaySettings>();

        Assert.Equal(IPAddress.Parse("127.0.13.60"), settings.Address);
        Assert.Equal([0], settings.Ports);
        Assert.Equal([0], settings.GamePorts);
        Assert.Equal(RelaySettings.DefaultCapturesDirectory, settings.CapturesDirectory);
        Assert.Equal(_install.ClientDirectory, settings.ClientDirectory);
        Assert.Equal(_install.RedirectDirectory, settings.RedirectDirectory);

        // The relay first, so that it listens before the client is sent to it.
        Assert.Equal(2, host.Services.GetServices<IHostedService>().Count());
    }

    [Fact]
    public void AnAddressThatCannotBeD3BsStopsTheBuild()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _install.Bot("--Network:Address", "127.0.0.1").AddModule<NetworkModule>());

        Assert.Contains(RelaySettings.AddressKey, exception.Message, StringComparison.Ordinal);
    }
}
