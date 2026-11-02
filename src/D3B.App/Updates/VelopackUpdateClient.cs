using Velopack;
using Velopack.Sources;

namespace D3B.App.Updates;

/// <summary>Finds, downloads and installs the app's newer versions through Velopack, from the repository's releases (D171, D173).</summary>
internal sealed class VelopackUpdateClient : IUpdateClient
{
    private readonly UpdateManager _manager;
    private UpdateInfo? _update;

    /// <inheritdoc/>
    public bool IsInstalled => _manager.IsInstalled;

    private UpdateInfo Update => _update ?? throw new InvalidOperationException("No newer version was found to download or install.");

    /// <summary>Initializes a new instance of the <see cref="VelopackUpdateClient"/> class.</summary>
    /// <param name="source">A GitHub repository's address, whose releases are read, or a folder or address holding Velopack's releases.</param>
    public VelopackUpdateClient(string source)
    {
        // A repository's releases hold the clients' data too (D24): GitHub's source skips those without the app's packages.
        _manager = Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Host == "github.com"
            ? new UpdateManager(new GithubSource(source, null, false))
            : new UpdateManager(source);
    }

    /// <inheritdoc/>
    public async Task<string?> CheckAsync()
    {
        _update = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);

        return _update?.TargetFullRelease.Version.ToString();
    }

    /// <inheritdoc/>
    public Task DownloadAsync(Action<int> progress)
    {
        return _manager.DownloadUpdatesAsync(Update, progress);
    }

    /// <inheritdoc/>
    public void ApplyAndRestart()
    {
        _manager.ApplyUpdatesAndRestart(Update.TargetFullRelease);
    }

    /// <inheritdoc/>
    public void ApplyOnExit()
    {
        _manager.WaitExitThenApplyUpdates(Update.TargetFullRelease, silent: true, restart: false);
    }
}
