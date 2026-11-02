using D3B.Bot.Data;

namespace D3B.Bot.Tests.Data;

public sealed class PackSourceTests : IDisposable
{
    private readonly TestInstall _install = new();

    public void Dispose()
    {
        _install.Dispose();
    }

    [Fact]
    public void TheAppsOwnPackIsOpenedWhenNoneIsOnThisMachine()
    {
        _install.WritePack(bundled: true);

        var source = new PackSource(_install.Settings);

        using var pack = source.Open();

        Assert.Equal(TestInstall.Version, pack.Info.ClientVersion);
        Assert.Equal(_install.BundledPackPath, source.PackPath);
    }

    [Fact]
    public void APackExtractedOnThisMachineWinsOverTheAppsOwn()
    {
        _install.WritePack();
        _install.WritePack("3.7.11.1", bundled: true);

        var source = new PackSource(_install.Settings);

        using var pack = source.Open();

        Assert.Equal(TestInstall.Version, pack.Info.ClientVersion);
        Assert.Equal(_install.PackPath, source.PackPath);
    }

    [Fact]
    public void AMissingPackNamesItsBuildAndHowToExtractIt()
    {
        var exception = Assert.Throws<DataUnavailableException>(() => new PackSource(_install.Settings).Open());

        Assert.Equal(TestInstall.Version, exception.MissingBuild);
        Assert.Contains($"No pack for client {TestInstall.Version}: none at {_install.PackPath}, nor at {_install.BundledPackPath}", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet run --file scripts/extract.cs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAppsOwnPackOfAnotherBuildIsRefused()
    {
        _install.WritePack("3.7.11.1", bundled: true);

        var exception = Assert.Throws<DataUnavailableException>(() => new PackSource(_install.Settings).Open());

        Assert.Contains("extracted from client 3.7.11.1", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.MissingBuild);
    }
}
