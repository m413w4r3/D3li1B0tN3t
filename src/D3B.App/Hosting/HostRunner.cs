using System.Diagnostics.CodeAnalysis;
using D3B.Bot.Hosting;
using D3B.Storage;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Hosting;

/// <summary>The bots' host in the app's process: started with the app, stopped once its window is gone (D91).</summary>
/// <remarks>
/// <para>
/// The host gets ready off the UI's thread, its pack opened and its mapping audited, and the window reads where it
/// stands (<see cref="Status"/>), why it did not start (<see cref="Failure"/>), and its services once it runs.
/// </para>
/// <para>
/// The host holds every client's game connection through its relay: stopping it, as closing the app does, disconnects
/// them, and gives each client its configuration back (D35).
/// </para>
/// </remarks>
[SuppressMessage("Design", "CA1001", Justification = "The stop's token source holds no timer, and nothing reads its wait handle; disposed, a start still reading its token would throw.")]
internal sealed class HostRunner
{
    private readonly Func<CancellationToken, Task<BotHostStart>> _start;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _lock = new();
    private Task? _starting;
    private Task? _stopped;
    private volatile IHost? _host;
    private volatile string? _failure;
    private volatile string? _missingBuild;
    private int _status = (int)HostStatus.Starting;

    /// <summary>Gets where the host stands.</summary>
    public HostStatus Status => (HostStatus)Volatile.Read(ref _status);

    /// <summary>Gets why the host did not start, and what to do about it, once it failed.</summary>
    public string? Failure => _failure;

    /// <summary>Gets the installed client's build, once the host did not start for lack of its pack, schema or mapping (D172).</summary>
    public string? MissingBuild => _missingBuild;

    /// <summary>Gets the host's services while it runs, else <see langword="null"/>.</summary>
    public IServiceProvider? Services => Status is HostStatus.Running ? _host?.Services : null;

    /// <summary>Initializes a new instance of the <see cref="HostRunner"/> class, for the default modules and the database.</summary>
    /// <param name="args">The command-line arguments, read as the host's configuration.</param>
    /// <param name="settings">The host's settings the app changed (D151), which its configuration and command line win over.</param>
    public HostRunner(string[] args, IReadOnlyDictionary<string, string?> settings)
        : this(cancellationToken => BotHost.StartAsync(BotHost.CreateBuilder(args, settings).AddModule<StorageModule>(), cancellationToken))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HostRunner"/> class.</summary>
    /// <param name="start">Builds and starts the host.</param>
    internal HostRunner(Func<CancellationToken, Task<BotHostStart>> start)
    {
        _start = start;
    }

    /// <summary>Starts getting the host ready, off the caller's thread.</summary>
    public void Start()
    {
        lock (_lock)
            _starting ??= Task.Run(StartAsync);
    }

    /// <summary>Stops the host, or its getting ready: its bots stop, and the clients are disconnected.</summary>
    /// <returns>A task that completes once the host stopped and was disposed.</returns>
    public Task StopAsync()
    {
        lock (_lock)
            return _stopped ??= StopCoreAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            var start = await _start(_stopping.Token).ConfigureAwait(false);

            if (start.Host is { } host)
            {
                _host = host;
                SetStatus(HostStatus.Running);
            }
            else
            {
                _missingBuild = start.MissingBuild;
                Fail(start.Failure);
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            SetStatus(HostStatus.Stopped);
        }
        catch (Exception exception)
        {
            // Such as two modules registering one id (D93): the app shows why, where the headless host would stop.
            Fail(exception.Message);
        }
    }

    private async Task StopCoreAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);

        Task? starting;

        lock (_lock)
            starting = _starting;

        if (starting is not null)
            await starting.ConfigureAwait(false);

        if (_host is not { } host)
        {
            if (Status is not HostStatus.Failed)
                SetStatus(HostStatus.Stopped);

            return;
        }

        SetStatus(HostStatus.Stopping);

        try
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            if (host is IAsyncDisposable disposable)
                await disposable.DisposeAsync().ConfigureAwait(false);
            else
                host.Dispose();

            SetStatus(HostStatus.Stopped);
        }
    }

    private void Fail(string? failure)
    {
        _failure = failure;
        SetStatus(HostStatus.Failed);
    }

    private void SetStatus(HostStatus status)
    {
        Volatile.Write(ref _status, (int)status);
    }
}
