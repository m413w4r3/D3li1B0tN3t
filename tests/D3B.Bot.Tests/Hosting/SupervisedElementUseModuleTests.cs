using D3B.Bot.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Hosting;

public sealed class SupervisedElementUseModuleTests : IDisposable
{
    private readonly TestInstall _install = new();

    public void Dispose()
    {
        _install.Dispose();
    }

    [Fact]
    public void TheElementUseReaderIsOffUnlessExplicitlyEnabled()
    {
        _install.WritePack();
        _install.WriteProtocol();

        using var host = _install.Bot().AddModule<SupervisedElementUseModule>().Build();

        Assert.Empty(host.Services.GetServices<SupervisedElementUseConsole>());
    }

    [Fact]
    public void TheElementUseReaderIsRegisteredWhenEnabled()
    {
        _install.WritePack();
        _install.WriteProtocol();

        using var host = _install.Bot("--Supervised:ElementUse:Enabled", "true").AddModule<SupervisedElementUseModule>().Build();

        Assert.Single(host.Services.GetServices<SupervisedElementUseConsole>());
    }
}
