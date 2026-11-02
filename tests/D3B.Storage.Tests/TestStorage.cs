using D3B.Bot.Activities;
using D3B.Bot.Hosting;
using D3B.Bot.Localization;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;
using D3B.Data.Tests;
using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Storage.Tests;

/// <summary>The storage's services as the module registers them, on a database of a test's own, beside the bots a test plays.</summary>
internal sealed class TestStorage : IAsyncDisposable
{
    public const string SettingsActivityId = "test.settings";
    public const string PlainActivityId = "test.plain";

    private readonly TemporaryDirectory _directory = new();
    private readonly ServiceProvider _services;
    private bool _started;

    public TestPlay Play { get; }

    public FakeTimeProvider Time => Play.Time;

    public FakeLogCollector Log { get; } = new();

    public StorageSettings Settings { get; }

    public IServiceProvider Services => _services;

    public AccountBook Accounts => _services.GetRequiredService<AccountBook>();

    public ProfileBook Profiles => _services.GetRequiredService<ProfileBook>();

    public ProfileStarter Starter => _services.GetRequiredService<ProfileStarter>();

    public TestStorage(string? path = null)
    {
        Settings = new StorageSettings { Path = path ?? _directory.Combine("data", "d3b.db") };

        var services = new ServiceCollection();

        StorageModule.Register(services, Settings);

        // The recorder sees the game as the bots do, after them.
        Play = new TestPlay([new DeferredObserver(() => _services!.GetRequiredService<AccountRecorder>())]);
        services.AddSingleton(Play.Bots);
        services.AddSingleton<TimeProvider>(Play.Time);
        services.AddLogging(logging => logging.AddProvider(new FakeLoggerProvider(Log)).SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton(TestReferences.Game());
        services.AddSingleton(new ActivityCatalog(
        [
            new ActivityDefinition(PlainActivityId, "Plain", _ => new TestActivity()),
            SettingsDefinition(),
        ]));
        _services = services.BuildServiceProvider();
    }

    public static ActivityDefinition SettingsDefinition()
    {
        var services = new ServiceCollection();

        services.AddActivity<SettingsActivity, TestSettings>(SettingsActivityId, LocalizedText.Invariant("Settings"), settings =>
        {
            settings.Number(s => s.Walks, LocalizedText.Invariant("Walks"), 1, 100);
            settings.Job(s => s.Job, LocalizedText.Invariant("Job"));
            settings.Resources(s => s.Resources, LocalizedText.Invariant("Resources")).Within(s => s.Job);
        });

        return services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>().Single();
    }

    /// <summary>Opens the database, as the host does before it starts, and starts the storage's services.</summary>
    public async Task<TestStorage> StartedAsync()
    {
        var failure = await _services.GetRequiredService<IHostPreparation>().PrepareAsync(TestContext.Current.CancellationToken);

        Assert.Null(failure);

        foreach (var service in _services.GetServices<IHostedService>())
            await service.StartAsync(TestContext.Current.CancellationToken);

        _started = true;

        return this;
    }

    /// <summary>Waits until what the relay showed so far is recorded.</summary>
    public Task RecordedAsync()
    {
        return _services.GetRequiredService<AccountRecorder>().WrittenAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_started)
        {
            foreach (var service in _services.GetServices<IHostedService>())
                await service.StopAsync(CancellationToken.None);
        }

        await Play.DisposeAsync();
        await _services.DisposeAsync();
        _directory.Dispose();
    }

    /// <summary>The recorder, made once the services are, which the play needs first.</summary>
    private sealed class DeferredObserver : D3B.Bot.Network.IGameObserver
    {
        private readonly Lazy<AccountRecorder> _recorder;

        public DeferredObserver(Func<AccountRecorder> recorder)
        {
            _recorder = new Lazy<AccountRecorder>(recorder);
        }

        public void OnSessionStarted(D3B.Bot.Network.GameSession session)
        {
            ((D3B.Bot.Network.IGameObserver)_recorder.Value).OnSessionStarted(session);
        }

        public void OnFrame(D3B.Bot.Network.GameSession session, D3B.Network.Relay.RelayDirection direction, D3B.Protocol.Translation.DecodedGameFrame frame)
        {
            ((D3B.Bot.Network.IGameObserver)_recorder.Value).OnFrame(session, direction, frame);
        }
    }
}
