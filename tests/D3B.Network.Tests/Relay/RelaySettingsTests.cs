using System.Net;
using D3B.Network.Relay;
using Microsoft.Extensions.Configuration;

namespace D3B.Network.Tests.Relay;

public sealed class RelaySettingsTests
{
    private const string ClientDirectory = @"C:\Games\Dofus";

    private static IConfiguration Configuration(params (string Key, string Value)[] settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();
    }

    [Fact]
    public void TheDefaultsAreD3BsAddressAndTheConnectionHostsPortsWithoutCaptures()
    {
        var settings = RelaySettings.Read(Configuration(), ClientDirectory);

        Assert.Equal(IPAddress.Parse("127.0.13.59"), settings.Address);
        Assert.Equal([5555, 443], settings.Ports);
        Assert.Equal([5556], settings.GamePorts);
        Assert.Equal(ClientDirectory, settings.ClientDirectory);
        Assert.Equal(RelaySettings.DefaultRedirectDirectory, settings.RedirectDirectory);
        Assert.Null(settings.CapturesDirectory);
    }

    [Fact]
    public void TheConfigurationSetsAddressPortsCapturesAndWhereTheClientsConfigurationIsKept()
    {
        var settings = RelaySettings.Read(Configuration(
            (RelaySettings.AddressKey, "127.1.2.3"),
            ($"{RelaySettings.PortsKey}:0", "0"),
            ($"{RelaySettings.GamePortsKey}:0", "6000"),
            ($"{RelaySettings.GamePortsKey}:1", "6001"),
            (RelaySettings.CapturesEnabledKey, "true"),
            (RelaySettings.CapturesDirectoryKey, "captures"),
            (RelaySettings.RedirectDirectoryKey, "client")), ClientDirectory);

        Assert.Equal(IPAddress.Parse("127.1.2.3"), settings.Address);
        Assert.Equal([0], settings.Ports);
        Assert.Equal([6000, 6001], settings.GamePorts);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "captures"), settings.CapturesDirectory);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "captures", "game"), settings.GameCapturesDirectory);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "client"), settings.RedirectDirectory);
    }

    [Fact]
    public void CapturesGoToLocalApplicationDataWhenEnabledWithoutADirectory()
    {
        var settings = RelaySettings.Read(Configuration((RelaySettings.CapturesEnabledKey, "true")), ClientDirectory);

        Assert.Equal(RelaySettings.DefaultCapturesDirectory, settings.CapturesDirectory);
    }

    [Fact]
    public void TheRelaysTimeoutsAreD3BsUnlessTheConfigurationRaisesThem()
    {
        var defaults = RelaySettings.Read(Configuration(), ClientDirectory);
        var configuration = Configuration((RelayTimeouts.ConnectKey, "30"), (RelayTimeouts.SelectionKey, "120.5"));
        var raised = RelaySettings.Read(configuration, ClientDirectory);

        Assert.Equal(
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(60)),
            (defaults.ConnectTimeout, defaults.CloseTimeout, defaults.HoldTimeout, defaults.SelectionLifetime));
        Assert.Equal(
            (TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(120.5)),
            (raised.ConnectTimeout, raised.CloseTimeout, raised.HoldTimeout, raised.SelectionLifetime));
        Assert.Equal(
            new Dictionary<string, string?> { [RelayTimeouts.ConnectKey] = "30", [RelayTimeouts.CloseKey] = null, [RelayTimeouts.HoldKey] = null, [RelayTimeouts.SelectionKey] = "120.5" },
            RelayTimeouts.Read(configuration).ToConfiguration());
    }

    [Theory]
    [InlineData(RelayTimeouts.ConnectKey, "5")]
    [InlineData(RelayTimeouts.HoldKey, "31")]
    [InlineData(RelayTimeouts.CloseKey, "soon")]
    public void ATimeoutBelowD3BsOrAboveItsMostIsRefused(string key, string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => RelaySettings.Read(Configuration((key, value)), ClientDirectory));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("::1")]
    [InlineData("localhost")]
    public void AnAddressThatCannotBeD3BsIsRefused(string address)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => RelaySettings.Read(Configuration((RelaySettings.AddressKey, address)), ClientDirectory));

        Assert.Contains(RelaySettings.AddressKey, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("https")]
    public void APortThatIsNoneIsRefused(string port)
    {
        Assert.Throws<InvalidOperationException>(() => RelaySettings.Read(Configuration(($"{RelaySettings.PortsKey}:0", port)), ClientDirectory));
        Assert.Throws<InvalidOperationException>(() => RelaySettings.Read(Configuration(($"{RelaySettings.GamePortsKey}:0", port)), ClientDirectory));
    }
}
