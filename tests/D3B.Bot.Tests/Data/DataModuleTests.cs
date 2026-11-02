using D3B.Bot.Data;
using D3B.Data;
using D3B.Data.Interactives;
using D3B.Data.Maps;
using D3B.Data.Packs;
using D3B.Data.Texts;
using D3B.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Data;

public sealed class DataModuleTests : IDisposable
{
    private readonly TestInstall _install = new();

    public void Dispose()
    {
        _install.Dispose();
    }

    [Fact]
    public void EveryStoreReadsTheOnePackOfTheInstalledBuild()
    {
        _install.WritePack();

        using var host = _install.Bot().AddModule<DataModule>().Build();

        var services = host.Services;
        var pack = services.GetRequiredService<GamePack>();

        Assert.Same(pack, services.GetRequiredService<GamePack>());
        Assert.Equal(TestInstall.Version, pack.Info.ClientVersion);
        Assert.Equal(TestInstall.Version, services.GetRequiredService<ClientBuild>().Version);
        Assert.Equal("Frêne", services.GetRequiredService<GameTexts>().Get(TextLanguages.French, TestInstall.AshNameId));
        Assert.Equal(TestInstall.MapId, services.GetRequiredService<GameMaps>().Get(TestInstall.MapId).Id);
        Assert.Equal(1, services.GetRequiredService<GameWorld>().Graph.Vertices.Length);
        Assert.True(services.GetRequiredService<GameTables>().Interactives.TryGet(1, out var interactive));
        Assert.Equal(TestInstall.AshNameId, interactive.NameId);
    }

    [Fact]
    public void EveryStoreIsOneForTheWholeHost()
    {
        _install.WritePack();

        using var host = _install.Bot().AddModule<DataModule>().Build();

        using var scope = host.Services.CreateScope();

        Assert.Same(host.Services.GetRequiredService<GameTexts>(), scope.ServiceProvider.GetRequiredService<GameTexts>());
        Assert.Same(host.Services.GetRequiredService<GameMaps>(), scope.ServiceProvider.GetRequiredService<GameMaps>());
        Assert.Same(host.Services.GetRequiredService<GameWorld>(), scope.ServiceProvider.GetRequiredService<GameWorld>());
        Assert.Same(host.Services.GetRequiredService<GameTables>(), scope.ServiceProvider.GetRequiredService<GameTables>());
    }

    [Fact]
    public void ThePackClosesWithTheHost()
    {
        _install.WritePack();

        GamePack pack;

        using (var host = _install.Bot().AddModule<DataModule>().Build())
            pack = host.Services.GetRequiredService<GamePack>();

        Assert.Throws<ObjectDisposedException>(() => pack.Read<InteractiveCatalog>(InteractiveCatalog.EntryName));
    }

    [Fact]
    public void NothingIsReadWhileTheModuleBuilds()
    {
        using var host = _install.Bot("--Client:Directory", Path.Combine(_install.Root, "nowhere")).AddModule<DataModule>().Build();

        var exception = Assert.Throws<DataUnavailableException>(() => host.Services.GetRequiredService<GamePack>());

        Assert.Contains("Client:Directory", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutAPackTheMissingBuildAndHowToExtractItAreNamed()
    {
        using var host = _install.Bot().AddModule<DataModule>().Build();

        var exception = Assert.Throws<DataUnavailableException>(() => host.Services.GetRequiredService<GamePack>());

        Assert.Contains($"client {TestInstall.Version}", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"dotnet run --file scripts/extract.cs -- --client \"{_install.ClientDirectory}\" --output \"{_install.PacksDirectory}\"", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePackOfAnotherBuildIsRefused()
    {
        _install.WritePack("3.7.11.1");

        using var host = _install.Bot().AddModule<DataModule>().Build();

        var exception = Assert.Throws<DataUnavailableException>(() => host.Services.GetRequiredService<GamePack>());

        Assert.Contains("extracted from client 3.7.11.1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("--force", exception.Message, StringComparison.Ordinal);
    }
}
