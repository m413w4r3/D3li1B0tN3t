using System.Net;
using D3B.Network.Clients;
using D3B.Network.Relay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.Network.Tests.Clients;

public sealed class ClientRedirectTests : IDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly TestClient _client = new();

    public void Dispose()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task TheClientIsSentToD3BWhileTheHostRunsAndGetsItsOwnConfigurationBack()
    {
        using var redirect = _client.Redirect();

        await redirect.StartAsync(TestContext.Current.CancellationToken);

        var given = ClientConfiguration.Parse(await File.ReadAllTextAsync(Path.Combine(_client.RedirectDirectory, ClientRedirectFiles.ConfigFileName), TestContext.Current.CancellationToken));

        Assert.Equal(_client.D3BConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
        Assert.StartsWith("file:///", _client.D3BConfigUrl, StringComparison.Ordinal);
        Assert.Equal("Test:127.0.13.59:5555,443", Assert.Single(given.ConnectionHosts).ToString());
        Assert.Equal("localhost", redirect.ConnectionHost?.Host);

        await redirect.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TestClient.VersionText(_client.AnkamaConfigUrl), await File.ReadAllTextAsync(_client.VersionFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AStartAfterACrashFindsAnkamasConfigurationAgain()
    {
        string crashed;

        // A killed host neither stops nor is disposed: the file it leaves names D3B's configuration.
        using (var first = _client.Redirect())
        {
            await first.StartAsync(TestContext.Current.CancellationToken);
            crashed = await File.ReadAllTextAsync(_client.VersionFile, TestContext.Current.CancellationToken);
        }

        await File.WriteAllTextAsync(_client.VersionFile, crashed, TestContext.Current.CancellationToken);

        using var redirect = _client.Redirect();

        await redirect.StartAsync(TestContext.Current.CancellationToken);
        await redirect.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_client.AnkamaConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
    }

    [Fact]
    public async Task AHostDisposedWithoutStoppingGivesTheClientItsConfigurationBack()
    {
        // As when a service started after this one fails to start: the host is disposed, never stopped.
        using (var redirect = _client.Redirect())
        {
            await redirect.StartAsync(TestContext.Current.CancellationToken);

            Assert.Equal(_client.D3BConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
        }

        Assert.Equal(_client.AnkamaConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
    }

    [Fact]
    public async Task ALaterHostedServiceStartFailureRestoresTheClientConfiguration()
    {
        var originalConfig = await File.ReadAllTextAsync(_client.AnkamaConfig, TestContext.Current.CancellationToken);
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(_ => _client.Redirect());
        builder.Services.AddSingleton<IHostedService>(services => services.GetRequiredService<ClientRedirect>());
        builder.Services.AddSingleton<IHostedService, FailingHostedService>();

        var host = builder.Build();

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

            Assert.Equal(_client.D3BConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
        }
        finally
        {
            // BotHost disposes a host when a later hosted service fails to start.
            host.Dispose();
        }

        Assert.Equal(_client.AnkamaConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
        Assert.Equal(originalConfig, await File.ReadAllTextAsync(_client.AnkamaConfig, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnotherD3BsConfigurationIsNeverTakenForAnkamas()
    {
        // As another D3B's copy would read: relaying to it would connect the relay to itself.
        _client.WriteAnkamaConfig("\"Test:127.0.13.60:5555,443\"");

        using var redirect = _client.Redirect();

        var exception = await Assert.ThrowsAsync<RelayUnavailableException>(() => redirect.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("a D3B address", exception.Message, StringComparison.Ordinal);
        Assert.Equal(_client.AnkamaConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
        Assert.Null(ClientRedirectFiles.OriginalUrl(_client.RedirectDirectory));
    }

    [Fact]
    public async Task TheLastConfigurationReadIsUsedWhenAnkamasCannotBe()
    {
        using (var first = _client.Redirect())
        {
            await first.StartAsync(TestContext.Current.CancellationToken);
            await first.StopAsync(TestContext.Current.CancellationToken);
        }

        File.Delete(_client.AnkamaConfig);

        using var redirect = _client.Redirect();

        await redirect.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal("localhost", redirect.ConnectionHost?.Host);
        Assert.Contains(_client.Log.GetSnapshot(), record => record.Message.Contains("the copy of", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithoutAnyConfigurationTheRelayIsUnavailable()
    {
        File.Delete(_client.AnkamaConfig);

        using var redirect = _client.Redirect();

        var exception = await Assert.ThrowsAsync<RelayUnavailableException>(() => redirect.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(_client.AnkamaConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
        Assert.Contains("kept no copy", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeveralConnectionHostsAreRefused()
    {
        _client.WriteAnkamaConfig("\"A:a.test:5555\",\"B:b.test:5555\"");

        using var redirect = _client.Redirect();

        var exception = await Assert.ThrowsAsync<RelayUnavailableException>(() => redirect.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("2 connection hosts", exception.Message, StringComparison.Ordinal);
        Assert.Equal(_client.AnkamaConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
    }

    [Fact]
    public async Task TheLauncherRewritingTheVersionFileIsFollowed()
    {
        using var redirect = _client.Redirect();

        await redirect.StartAsync(TestContext.Current.CancellationToken);

        // An update writes the file anew, with Ankama's URL, here another configuration naming another host.
        var updated = Path.Combine(Path.GetDirectoryName(_client.AnkamaConfig)!, "updated.json");

        File.WriteAllText(updated, """{"connectionHosts":["Test:127.0.0.1:5555,443"]}""");
        File.WriteAllText(_client.VersionFile, TestClient.VersionText(new Uri(updated).AbsoluteUri));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(_timeout);

        while (ClientVersionFile.ReadConfigUrl(_client.VersionFile) != _client.D3BConfigUrl || redirect.ConnectionHost?.Host != "127.0.0.1")
            await Task.Delay(50, timeout.Token);

        await redirect.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new Uri(updated).AbsoluteUri, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
    }

    [Fact]
    public async Task TheRelayFindsTheConnectionHostTheConfigurationNames()
    {
        using var redirect = _client.Redirect();

        await redirect.StartAsync(TestContext.Current.CancellationToken);

        var upstream = await _client.Locator(redirect).LocateAsync(RelayRoute.Connection, new IPEndPoint(RelaySettings.DefaultAddress, 443), ReadOnlyMemory<byte>.Empty, TestContext.Current.CancellationToken);

        Assert.Equal("localhost", upstream.Host);
        Assert.NotEmpty(upstream.EndPoints);
        Assert.All(upstream.EndPoints, endPoint => Assert.True(IPAddress.IsLoopback(endPoint.Address) && endPoint.Port is 443));
    }

    [Fact]
    public async Task BeforeTheClientIsSentToD3BTheRelayHasNoServer()
    {
        using var redirect = _client.Redirect();

        await Assert.ThrowsAsync<UpstreamUnavailableException>(() => _client.Locator(redirect).LocateAsync(RelayRoute.Connection, new IPEndPoint(RelaySettings.DefaultAddress, 443), ReadOnlyMemory<byte>.Empty, TestContext.Current.CancellationToken));
    }

    private sealed class FailingHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.FromException(new InvalidOperationException("The later hosted service failed to start."));
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
