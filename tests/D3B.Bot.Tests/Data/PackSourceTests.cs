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

    [Fact]
    public void APackOfAnotherBuildWithTheSameVersionIsRefused()
    {
        _install.WritePack(buildDate: "2026-11-03T14:11:23+00:00");

        var exception = Assert.Throws<DataUnavailableException>(() => new PackSource(_install.Settings).Open());

        Assert.Contains($"extracted from client {TestInstall.Version}, built 2026-11-03", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not from the installed build, built 2026-10-19", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.MissingBuild);
    }

    [Fact]
    public void APackOfAnotherVersionOnThisMachineIsRefused()
    {
        _install.WritePack("3.7.11.1");

        var exception = Assert.Throws<DataUnavailableException>(() => new PackSource(_install.Settings).Open());

        Assert.Contains("extracted from client 3.7.11.1", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"not from {TestInstall.Version}, the one installed", exception.Message, StringComparison.Ordinal);
    }
}
