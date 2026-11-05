using D3B.Bot.Diagnostics;
using D3B.Bot.Network;
using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
}
