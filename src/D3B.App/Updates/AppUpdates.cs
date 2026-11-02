namespace D3B.App.Updates;

/// <summary>The app's updates: checked as it starts, downloaded at once, installed when the user restarts it or closes it (D173).</summary>
/// <remarks>
/// <para>
/// Restarting disconnects the clients (D91), so the app never restarts by itself: the user chooses when. The window and the
/// settings page read where the update stands (<see cref="Status"/>) every refresh, off the thread that checks.
/// </para>
/// <para>
/// A check or a download that fails is said, never thrown: the app runs on without its update.
/// </para>
/// </remarks>
internal sealed class AppUpdates
{
    private readonly IUpdateClient _client;
    private readonly Lock _lock = new();
    private Task? _checking;
    private volatile string? _newVersion;
    private volatile string? _failure;
    private volatile bool _restart;
    private int _progress;
    private int _status;

    /// <summary>Gets where the update stands.</summary>
    public UpdateStatus Status => (UpdateStatus)Volatile.Read(ref _status);

    /// <summary>Gets the newer version found, once a check found one.</summary>
    public string? NewVersion => _newVersion;

    /// <summary>Gets the share of the newer version downloaded, from 0 to 100.</summary>
    public int Progress => Volatile.Read(ref _progress);

    /// <summary>Gets why the last check or download failed, once it did.</summary>
    public string? Failure => _failure;

    /// <summary>Gets whether the app runs installed, which alone updates itself.</summary>
    public bool IsInstalled => _client.IsInstalled;

    /// <summary>Gets whether the user asked to restart the app to install the version downloaded.</summary>
    public bool RestartRequested => _restart;

    /// <summary>Initializes a new instance of the <see cref="AppUpdates"/> class.</summary>
    /// <param name="client">What finds, downloads and installs the newer versions.</param>
    public AppUpdates(IUpdateClient client)
    {
        _client = client;
        _status = (int)(client.IsInstalled ? UpdateStatus.Idle : UpdateStatus.NotInstalled);
    }

    /// <summary>Checks for a newer version, off the caller's thread, and downloads the one it finds.</summary>
    /// <returns>A task that completes once the check, and the download, are over; the one running when one already does.</returns>
    public Task CheckAsync()
    {
        lock (_lock)
        {
            if (_checking is { IsCompleted: false } || Status is UpdateStatus.NotInstalled or UpdateStatus.Ready)
                return _checking ?? Task.CompletedTask;

            _checking = Task.Run(CheckCoreAsync);

            return _checking;
        }
    }

    /// <summary>Asks for the restart that installs the version downloaded, once the app's window is gone and its host stopped.</summary>
    /// <returns>Whether a version is downloaded, which the restart installs.</returns>
    public bool RequestRestart()
    {
        if (Status is not UpdateStatus.Ready)
            return false;

        _restart = true;

        return true;
    }

    /// <summary>Installs the version downloaded, once the app's window is gone and its host stopped: now and restarting the app when asked, else once the process ended.</summary>
    public void Finish()
    {
        if (Status is not UpdateStatus.Ready)
            return;

        if (_restart)
            _client.ApplyAndRestart();
        else
            _client.ApplyOnExit();
    }

    private async Task CheckCoreAsync()
    {
        try
        {
            _failure = null;
            SetStatus(UpdateStatus.Checking);

            var version = await _client.CheckAsync().ConfigureAwait(false);

            if (version is null)
            {
                SetStatus(UpdateStatus.UpToDate);

                return;
            }

            _newVersion = version;
            Volatile.Write(ref _progress, 0);
            SetStatus(UpdateStatus.Downloading);

            await _client.DownloadAsync(progress => Volatile.Write(ref _progress, progress)).ConfigureAwait(false);

            SetStatus(UpdateStatus.Ready);
        }
        catch (Exception exception)
        {
            // Offline, the releases unreachable, a package that does not check: the app runs on, and says why.
            _failure = exception.Message;
            SetStatus(UpdateStatus.Failed);
        }
    }

    private void SetStatus(UpdateStatus status)
    {
        Volatile.Write(ref _status, (int)status);
    }
}
