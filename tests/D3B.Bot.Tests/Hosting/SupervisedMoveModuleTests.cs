using D3B.Bot.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Hosting;

public sealed class SupervisedMoveModuleTests : IDisposable
{
    private readonly TestInstall _install = new();

    public void Dispose()
    {
        _install.Dispose();
    }

    [Fact]
    public void TheReaderIsOffUnlessExplicitlyEnabled()
    {
        _install.WritePack();
        _install.WriteProtocol();

        using var host = _install.Bot().AddModule<SupervisedMoveModule>().Build();

        Assert.Empty(host.Services.GetServices<SupervisedMoveConsole>());
    }

    [Fact]
    public void TheReaderIsRegisteredWhenEnabled()
    {
        _install.WritePack();
        _install.WriteProtocol();

        using var host = _install.Bot("--Supervised:Move:Enabled", "true").AddModule<SupervisedMoveModule>().Build();

        Assert.Single(host.Services.GetServices<SupervisedMoveConsole>());
    }
}
