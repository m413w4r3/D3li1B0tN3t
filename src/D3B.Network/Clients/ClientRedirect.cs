using System.Net;
using D3B.Network.Relay;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static D3B.Network.Clients.ClientRedirectFiles;

namespace D3B.Network.Clients;

/// <summary>Sends the official clients to D3B through their configuration, while the host runs (D35).</summary>
/// <remarks>
/// <para>
/// At start, it reads the configuration Ankama's <c>ConfigUrl</c> names, writes a copy whose connection hosts are D3B's
/// address, and points the client's <c>ConfigUrl</c> at that copy as a <c>file:</c> URL: the client refuses plain HTTP, and
/// reads a local file. It keeps Ankama's URL and its last configuration beside the copy, so that a start after a crash
/// finds them again, and so that a configuration Ankama cannot serve for a moment does not stop the host.
/// </para>
/// <para>
/// While the host runs, it watches the version file: the launcher rewrites it when it updates the client, and the client
/// is pointed at D3B again. At stop, it puts Ankama's URL back, so that a client started without D3B plays directly; and
/// so does a host disposed without being stopped, such as one whose start failed in a later service.
/// </para>
/// </remarks>
internal sealed class ClientRedirect : IHostedService, IDisposable
{
    private const int MaxRetries = 5;

    private static readonly TimeSpan _downloadTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _settleDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _disposeTimeout = TimeSpan.FromSeconds(5);

    private readonly RelaySettings _settings;
    private readonly ILogger _log;
    private readonly HttpClient _http = new() { Timeout = _downloadTimeout };
    private readonly SemaphoreSlim _applying = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private FileSystemWatcher? _watcher;
    private Timer? _settle;
    private volatile ConnectionHost? _connectionHost;
    private int _retries;
    private bool _stopped;
    private bool _disposed;

    /// <summary>Gets the connection host Ankama's configuration names, which the relay connects clients to.</summary>
    /// <remarks>Null until the host has started.</remarks>
    public ConnectionHost? ConnectionHost => _connectionHost;

    private string VersionFile => ClientVersionFile.PathIn(_settings.ClientDirectory);

    public ClientRedirect(RelaySettings settings, ILogger<ClientRedirect> log)
    {
        _settings = settings;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ApplyAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or HttpRequestException)
        {
            throw new RelayUnavailableException($"D3B cannot send the client to itself through its configuration: {exception.Message}", exception);
        }

        var versionFile = VersionFile;

        _settle = new Timer(_ => _ = ReapplyAsync(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(versionFile)!, Path.GetFileName(versionFile))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };

        // The launcher may write the file in several steps: the check waits until it has been still for a moment.
        _watcher.Changed += (_, _) => Settle();
        _watcher.Created += (_, _) => Settle();
        _watcher.Renamed += (_, _) => Settle();
        _watcher.EnableRaisingEvents = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // First, so that a check the launcher's last write started cannot point the client at D3B after the restore, and
        // so that one downloading Ankama's configuration gives the lock up at once.
        await _stopping.CancelAsync();

        if (_watcher is not null)
            _watcher.EnableRaisingEvents = false;

        _settle?.Change(Timeout.Infinite, Timeout.Infinite);

        await _applying.WaitAsync(cancellationToken);

        try
        {
            _stopped = true;
            Restore();
        }
        finally
        {
            _applying.Release();
        }
    }

    public void Dispose()
    {
        // The container disposes it twice: once as itself, once as the hosted service its factory returned.
        if (_disposed)
            return;

        _disposed = true;
        _stopping.Cancel();
        _watcher?.Dispose();
        _settle?.Dispose();

        // A host disposed without being stopped, as when a later service failed to start, must not leave the client
        // pointed at a relay that is gone.
        if (_applying.Wait(_disposeTimeout))
        {
            try
            {
                if (!_stopped && _connectionHost is not null)
                    Restore();
            }
            finally
            {
                _applying.Release();
            }
        }

        _http.Dispose();
        _applying.Dispose();
        _stopping.Dispose();
    }

    private void Settle()
    {
        try
        {
            _settle?.Change(_settleDelay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // The watcher's last event can come once the host has let the timer go.
        }
    }

    private async Task ApplyAsync(CancellationToken cancellationToken)
    {
        await _applying.WaitAsync(cancellationToken);

        try
        {
            if (_stopping.IsCancellationRequested)
                return;

            var directory = _settings.RedirectDirectory;
            var versionFile = VersionFile;
            var ours = ConfigUrl(directory);
            var current = ClientVersionFile.ReadConfigUrl(versionFile) ?? throw new InvalidDataException($"{versionFile} has no ConfigUrl line.");

            // Ours already, as after a crash: Ankama's URL is the one kept when the client was first sent to D3B.
            var original = current == ours
                ? OriginalUrl(directory) ?? throw new InvalidDataException($"{versionFile} names D3B's configuration, and Ankama's URL is lost: repair the client from the launcher.")
                : current;

            Directory.CreateDirectory(directory);

            var json = await DownloadAsync(original, directory, cancellationToken);
            var configuration = ClientConfiguration.Parse(json);

            // A connection carries no name the relay could tell hosts apart by: each would need an address of its own.
            if (configuration.ConnectionHosts.Count is not 1)
                throw new InvalidDataException($"The client's configuration lists {configuration.ConnectionHosts.Count} connection hosts; D3B relays one.");

            var host = configuration.ConnectionHosts[0];

            // Another D3B's copy, such as one kept in another directory: relaying to it would make the relay connect to
            // itself, and keeping its URL would give the client a D3B URL back at stop.
            if (IPAddress.TryParse(host.Host, out var address) && RelaySettings.IsRelayAddress(address))
                throw new InvalidDataException($"{original} sends clients to {host.Host}, a D3B address, not Ankama's: repair the client from the launcher.");

            var configFile = Path.Combine(directory, ConfigFileName);

            File.WriteAllText(Path.Combine(directory, OriginalUrlFileName), original);
            File.WriteAllText(Path.Combine(directory, OriginalConfigFileName), json);
            File.WriteAllText(configFile, configuration.RedirectedTo(_settings.Address));

            if (current != ours)
                ClientVersionFile.WriteConfigUrl(versionFile, ours);

            var unheard = host.Ports.Except(_settings.Ports).ToList();

            _connectionHost = host;
            ClientLog.Redirected(_log, host.Host, _settings.Address, configFile);

            // Port 0 is a test's: the relay listens wherever the system put it.
            if (unheard.Count > 0 && !_settings.Ports.Contains(0))
            {
                var ports = string.Join(", ", host.Ports);
                var listened = string.Join(", ", _settings.Ports);

                ClientLog.PortsNotListened(_log, ports, listened);
            }
        }
        finally
        {
            _applying.Release();
        }
    }

    private async Task<string> DownloadAsync(string url, string directory, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && !uri.IsFile))
            throw new InvalidDataException($"The client's configuration URL '{url}' is neither HTTPS nor a file.");

        try
        {
            return uri.IsFile ? await File.ReadAllTextAsync(uri.LocalPath, cancellationToken) : await _http.GetStringAsync(uri, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            var copy = Path.Combine(directory, OriginalConfigFileName);

            // The configuration names hosts that change rarely: the last one read beats a host that does not start.
            if (!File.Exists(copy))
                throw new InvalidDataException($"The client's configuration cannot be read from {url}, and D3B kept no copy: {exception.Message}", exception);

            ClientLog.UsingCopy(_log, url, File.GetLastWriteTime(copy), exception.Message);

            return await File.ReadAllTextAsync(copy, cancellationToken);
        }
    }

    private async Task ReapplyAsync()
    {
        try
        {
            if (_stopping.IsCancellationRequested || ClientVersionFile.ReadConfigUrl(VersionFile) == ConfigUrl(_settings.RedirectDirectory))
                return;

            ClientLog.Rewritten(_log, VersionFile);
            await ApplyAsync(_stopping.Token);
            Interlocked.Exchange(ref _retries, 0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // The launcher may still be writing the file, or hold it open: the check comes again, a few times.
            if (Interlocked.Increment(ref _retries) <= MaxRetries)
            {
                Settle();

                return;
            }

            Interlocked.Exchange(ref _retries, 0);
            ClientLog.RedirectFailed(_log, exception.Message);
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
            // The host is stopping: the restore takes over.
        }
    }

    private void Restore()
    {
        try
        {
            var versionFile = VersionFile;
            var original = OriginalUrl(_settings.RedirectDirectory);

            // Only D3B's own URL is replaced: a URL the launcher wrote meanwhile is already Ankama's.
            if (original is null || ClientVersionFile.ReadConfigUrl(versionFile) != ConfigUrl(_settings.RedirectDirectory))
                return;

            ClientVersionFile.WriteConfigUrl(versionFile, original);
            ClientLog.Restored(_log, versionFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ClientLog.RedirectFailed(_log, exception.Message);
        }
    }
}
