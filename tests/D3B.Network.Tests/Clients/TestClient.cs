using D3B.Data.Tests;
using D3B.Network.Clients;
using D3B.Network.Relay;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Network.Tests.Clients;

/// <summary>An installed client whose configuration is a local file, and a directory where D3B keeps its own, both deleted at the end.</summary>
internal sealed class TestClient : IDisposable
{
    public const string Version = "Version=3.7.12.12\nBuildDate=2026-10-19T15:11:45+00:00\nConfigUrl={0}\n";

    private readonly TemporaryDirectory _directory = new();

    public FakeLogCollector Log { get; } = new();

    public string ClientDirectory => _directory.Combine("client");

    public string RedirectDirectory => _directory.Combine("redirect");

    public string VersionFile => ClientVersionFile.PathIn(ClientDirectory);

    /// <summary>Gets the configuration Ankama serves, here a file the version file names.</summary>
    public string AnkamaConfig => _directory.Combine("ankama.json");

    public string AnkamaConfigUrl => new Uri(AnkamaConfig).AbsoluteUri;

    public string D3BConfigUrl => ClientRedirectFiles.ConfigUrl(RedirectDirectory);

    public TestClient(string connectionHosts = "\"Test:localhost:5555,443\"")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(VersionFile)!);
        File.WriteAllText(VersionFile, VersionText(AnkamaConfigUrl));
        WriteAnkamaConfig(connectionHosts);
    }

    /// <summary>A version file as the launcher writes it, naming a configuration.</summary>
    public static string VersionText(string configUrl)
    {
        return $"Version=3.7.12.12\nBuildDate=2026-10-19T15:11:45+00:00\nConfigUrl={configUrl}\n";
    }

    public void WriteAnkamaConfig(string connectionHosts)
    {
        File.WriteAllText(AnkamaConfig, $$"""{"gameAppId":1,"connectionHosts":[{{connectionHosts}}],"chatServerPort":6337}""");
    }

    public ClientRedirect Redirect()
    {
        return new ClientRedirect(Settings(), new FakeLogger<ClientRedirect>(Log));
    }

    /// <summary>The relay's way to its servers, through a redirect.</summary>
    public UpstreamLocator Locator(ClientRedirect redirect)
    {
        return new UpstreamLocator(redirect, new GameServerSelections(Settings(), TimeProvider.System));
    }

    private RelaySettings Settings()
    {
        return new RelaySettings
        {
            Address = RelaySettings.DefaultAddress,
            Ports = RelaySettings.DefaultPorts,
            GamePorts = RelaySettings.DefaultGamePorts,
            ClientDirectory = ClientDirectory,
            RedirectDirectory = RedirectDirectory,
        };
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
