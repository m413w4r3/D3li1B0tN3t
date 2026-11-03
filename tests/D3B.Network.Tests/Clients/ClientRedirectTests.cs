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

        var originalConfig = await File.ReadAllTextAsync(_client.AnkamaConfig, TestContext.Current.CancellationToken);
        var originalConfigCopy = Path.Combine(_client.RedirectDirectory, ClientRedirectFiles.OriginalConfigFileName);
        var config = Path.Combine(_client.RedirectDirectory, ClientRedirectFiles.ConfigFileName);
        var given = ClientConfiguration.Parse(await File.ReadAllTextAsync(config, TestContext.Current.CancellationToken));

        Assert.Equal(_client.AnkamaConfigUrl, ClientRedirectFiles.OriginalUrl(_client.RedirectDirectory));
        Assert.Equal(originalConfig, await File.ReadAllTextAsync(originalConfigCopy, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(config));
        Assert.Equal(_client.D3BConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
        Assert.StartsWith("file:///", _client.D3BConfigUrl, StringComparison.Ordinal);
        Assert.Equal("Test:127.0.13.59:5555,443", Assert.Single(given.ConnectionHosts).ToString());
        Assert.Equal("localhost", redirect.ConnectionHost?.Host);

        await redirect.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TestClient.VersionText(_client.AnkamaConfigUrl), await File.ReadAllTextAsync(_client.VersionFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AFirstRedirectSidecarFailureLeavesTheVersionFileUntouched()
    {
        var original = await File.ReadAllBytesAsync(_client.VersionFile, TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(_client.RedirectDirectory, ClientRedirectFiles.OriginalUrlFileName));

        using var redirect = _client.Redirect();

        var exception = await Assert.ThrowsAsync<RelayUnavailableException>(() => redirect.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("D3B cannot send the client", exception.Message, StringComparison.Ordinal);
        Assert.Equal(original, await File.ReadAllBytesAsync(_client.VersionFile, TestContext.Current.CancellationToken));
        Assert.Equal(_client.AnkamaConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
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

        var savedUrlPath = Path.Combine(_client.RedirectDirectory, ClientRedirectFiles.OriginalUrlFileName);
        var savedUrlBytes = await File.ReadAllBytesAsync(savedUrlPath, TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(savedUrlPath, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var savedUrlLastWriteTimeUtc = File.GetLastWriteTimeUtc(savedUrlPath);
        UnixFileMode? originalMode = null;

        if (!OperatingSystem.IsWindows())
        {
            originalMode = File.GetUnixFileMode(savedUrlPath);
            File.SetUnixFileMode(savedUrlPath, originalMode.Value & ~(UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite));
        }

        try
        {
            using var redirect = _client.Redirect();

            await redirect.StartAsync(TestContext.Current.CancellationToken);

            Assert.Equal(savedUrlBytes, await File.ReadAllBytesAsync(savedUrlPath, TestContext.Current.CancellationToken));
            Assert.Equal(savedUrlLastWriteTimeUtc, File.GetLastWriteTimeUtc(savedUrlPath));

            await redirect.StopAsync(TestContext.Current.CancellationToken);

            Assert.Equal(savedUrlBytes, await File.ReadAllBytesAsync(savedUrlPath, TestContext.Current.CancellationToken));
            Assert.Equal(savedUrlLastWriteTimeUtc, File.GetLastWriteTimeUtc(savedUrlPath));
            Assert.Equal(_client.AnkamaConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
        }
        finally
        {
            if (!OperatingSystem.IsWindows() && originalMode is { } mode)
                File.SetUnixFileMode(savedUrlPath, mode);
        }
    }

    [Fact]
    public async Task ACrashWithNoSavedUrlFailsWithRepairInstructions()
    {
        string crashed;

        using (var first = _client.Redirect())
        {
            await first.StartAsync(TestContext.Current.CancellationToken);
            crashed = await File.ReadAllTextAsync(_client.VersionFile, TestContext.Current.CancellationToken);
        }

        await File.WriteAllTextAsync(_client.VersionFile, crashed, TestContext.Current.CancellationToken);
        File.Delete(Path.Combine(_client.RedirectDirectory, ClientRedirectFiles.OriginalUrlFileName));

        using var redirect = _client.Redirect();

        var exception = await Assert.ThrowsAsync<RelayUnavailableException>(() => redirect.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("repair the client from the launcher", exception.Message, StringComparison.Ordinal);
        Assert.Equal(_client.D3BConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
        Assert.Null(ClientRedirectFiles.OriginalUrl(_client.RedirectDirectory));
    }

    [Fact]
    public async Task AMalformedConfigurationLeavesTheOriginalVersionFileUntouched()
    {
        var original = await File.ReadAllBytesAsync(_client.VersionFile, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(_client.AnkamaConfig, "{not-json", TestContext.Current.CancellationToken);

        using var redirect = _client.Redirect();

        await Assert.ThrowsAsync<RelayUnavailableException>(() => redirect.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(original, await File.ReadAllBytesAsync(_client.VersionFile, TestContext.Current.CancellationToken));
        Assert.Equal(_client.AnkamaConfigUrl, ClientVersionFile.ReadConfigUrl(_client.VersionFile));
        Assert.Null(ClientRedirectFiles.OriginalUrl(_client.RedirectDirectory));
    }

    [Fact]
    public async Task AVersionFileWithoutConfigUrlFailsAndKeepsItsBytes()
    {
        byte[] original = "Version=3.7.12.12\nBuildDate=2026-10-19T15:11:45+00:00\n"u8.ToArray();
        await File.WriteAllBytesAsync(_client.VersionFile, original, TestContext.Current.CancellationToken);

        using var redirect = _client.Redirect();

        var exception = await Assert.ThrowsAsync<RelayUnavailableException>(() => redirect.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("has no ConfigUrl line", exception.Message, StringComparison.Ordinal);
        Assert.Equal(original, await File.ReadAllBytesAsync(_client.VersionFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RedirectingOneInstallationLeavesAnotherInstallationUntouched()
    {
        using var other = new TestClient("\"Other:other.test:5555,443\"");
        var otherVersion = await File.ReadAllBytesAsync(other.VersionFile, TestContext.Current.CancellationToken);
        var otherConfig = await File.ReadAllBytesAsync(other.AnkamaConfig, TestContext.Current.CancellationToken);

        using (var redirect = _client.Redirect())
        {
            await redirect.StartAsync(TestContext.Current.CancellationToken);

            Assert.Equal(otherVersion, await File.ReadAllBytesAsync(other.VersionFile, TestContext.Current.CancellationToken));
            Assert.Equal(otherConfig, await File.ReadAllBytesAsync(other.AnkamaConfig, TestContext.Current.CancellationToken));

            await redirect.StopAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(otherVersion, await File.ReadAllBytesAsync(other.VersionFile, TestContext.Current.CancellationToken));
        Assert.Equal(otherConfig, await File.ReadAllBytesAsync(other.AnkamaConfig, TestContext.Current.CancellationToken));
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

    [Fact]
    public async Task AChangedBuildStopsTheHostAndKeepsTheLaunchersNewConfiguration()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        using var redirect = _client.Redirect(new ClientBuildIdentity("3.7.12.12", new DateTimeOffset(2026, 10, 19, 15, 11, 45, TimeSpan.Zero)), lifetime);

        await redirect.StartAsync(TestContext.Current.CancellationToken);

        var updated = Path.Combine(Path.GetDirectoryName(_client.AnkamaConfig)!, "updated.json");
        File.WriteAllText(updated, """{"connectionHosts":["Test:127.0.0.1:5555,443"]}""");
        var newVersion = $"Version=3.7.13.17\nBuildDate=2026-11-03T14:11:23+00:00\nConfigUrl={new Uri(updated).AbsoluteUri}\n";

        await File.WriteAllTextAsync(_client.VersionFile, newVersion, TestContext.Current.CancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(_timeout);

        while (!lifetime.StopApplicationCalled)
            await Task.Delay(50, timeout.Token);

        Assert.Equal(newVersion, await File.ReadAllTextAsync(_client.VersionFile, TestContext.Current.CancellationToken));

        await redirect.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(newVersion, await File.ReadAllTextAsync(_client.VersionFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ASameVersionWithAnotherBuildDateStopsTheHost()
    {
        using var lifetime = new FakeHostApplicationLifetime();
        using var redirect = _client.Redirect(new ClientBuildIdentity("3.7.12.12", new DateTimeOffset(2026, 10, 19, 15, 11, 45, TimeSpan.Zero)), lifetime);

        await redirect.StartAsync(TestContext.Current.CancellationToken);

        var updated = Path.Combine(Path.GetDirectoryName(_client.AnkamaConfig)!, "updated.json");
        File.WriteAllText(updated, """{"connectionHosts":["Test:127.0.0.1:5555,443"]}""");
        var newVersion = $"Version=3.7.12.12\nBuildDate=2026-11-03T14:11:23+00:00\nConfigUrl={new Uri(updated).AbsoluteUri}\n";

        await File.WriteAllTextAsync(_client.VersionFile, newVersion, TestContext.Current.CancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(_timeout);

        while (!lifetime.StopApplicationCalled)
            await Task.Delay(50, timeout.Token);

        await redirect.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(newVersion, await File.ReadAllTextAsync(_client.VersionFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AChangedBuildAtStartLeavesTheVersionFileUntouched()
    {
        var original = await File.ReadAllTextAsync(_client.VersionFile, TestContext.Current.CancellationToken);
        using var lifetime = new FakeHostApplicationLifetime();
        using var redirect = _client.Redirect(new ClientBuildIdentity("3.7.13.17", new DateTimeOffset(2026, 11, 3, 14, 11, 23, TimeSpan.Zero)), lifetime);

        var exception = await Assert.ThrowsAsync<RelayUnavailableException>(() => redirect.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("D3B was prepared for", exception.Message, StringComparison.Ordinal);
        Assert.Equal(original, await File.ReadAllTextAsync(_client.VersionFile, TestContext.Current.CancellationToken));
        Assert.False(lifetime.StopApplicationCalled);
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
