using D3B.Bot.Diagnostics;
using D3B.Bot.Network;
using D3B.Bot.Protocol;
using D3B.Core.Modules;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Ipc;
using D3B.Bot.Tests.Network;
using D3B.Network.Relay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Tests.Diagnostics;

public sealed class DiagnosticModuleTests
{
    [Fact]
    public void DisabledConfigurationRegistersNoDiagnosticObserver()
    {
        var builder = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.AddModule<DiagnosticModule>();
        using var host = builder.Build();

        Assert.Empty(host.Services.GetServices<IGameObserver>());
        Assert.Null(host.Services.GetService<DiagnosticObserver>());
    }

    [Fact]
    public async Task ReportSetupFailureKeepsObservationEnabled()
    {
        using var install = new TestInstall();
        install.WriteProtocol();
        var bot = install.Bot(
            "--Diagnostic:Enabled", "true",
            "--Diagnostic:EnginePath", "/bin/true",
            "--Diagnostic:ReportEnabled", "true",
            "--Diagnostic:ReportMaxBytes", "1");
        var sidecarFactory = new RecordingUnavailableSidecarFactory();
        bot.Services.AddSingleton<IDiagnosticSidecarFactory>(sidecarFactory);
        bot.AddModule<ProtocolModule>();
        bot.AddModule<DiagnosticModule>();
        using IHost host = bot.Build();
        var observer = host.Services.GetRequiredService<DiagnosticObserver>();
        var settings = new RelaySettings
        {
            Address = RelaySettings.DefaultAddress,
            Ports = RelaySettings.DefaultPorts,
            GamePorts = RelaySettings.DefaultGamePorts,
            ClientDirectory = install.ClientDirectory,
            RedirectDirectory = install.RedirectDirectory,
        };
        using var connection = new FakeRelayConnection("diagnostic-module-test", RelayRoute.Game);
        var relay = new RelayDecoder(
            TestGame.Translation(),
            new GameServerSelections(settings, TimeProvider.System),
            new GameAccounts(settings.SelectionLifetime, TimeProvider.System),
            [observer],
            host.Services.GetRequiredService<ILoggerFactory>());

        _ = relay.Intercept(connection);
        await sidecarFactory.Started.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await observer.DisposeAsync();
    }

    private sealed class RecordingUnavailableSidecarFactory : IDiagnosticSidecarFactory
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<SidecarStartResult> StartAsync(SidecarOptions options, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return Task.FromResult(new SidecarStartResult(null, DiagnosticErrorCode.SocketFailed));
        }
    }
}
