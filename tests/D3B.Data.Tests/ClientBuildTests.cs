namespace D3B.Data.Tests;

public sealed class ClientBuildTests
{
    [Fact]
    public void TheVersionAndBuildDateAreReadFromTheVersionFile()
    {
        var build = ClientBuild.Parse("Version=3.7.12.12\r\nBuildDate=2026-10-19T15:11:45+00:00\r\nConfigUrl=https://example.com/config.json\r\n");

        Assert.Equal("3.7.12.12", build.Version);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 15, 11, 45, TimeSpan.Zero), build.BuildDate);
    }

    [Fact]
    public void TheBuildDateIsOptional()
    {
        Assert.Null(ClientBuild.Parse("Version=3.7.12.12").BuildDate);
    }

    [Fact]
    public void AVersionFileWithoutAVersionIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => ClientBuild.Parse("BuildDate=2026-10-19T15:11:45+00:00"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("3.7.")]
    [InlineData("3.7.12.12-beta")]
    [InlineData(@"..\..\Windows")]
    public void AVersionIsNumbersSeparatedByDots(string version)
    {
        Assert.Throws<InvalidDataException>(() => ClientBuild.Parse($"Version={version}"));
        Assert.Throws<ArgumentException>(() => new ClientBuild(version, null));
    }

    [Fact]
    public void TheBuildIsReadFromTheInstalledClient()
    {
        using var directory = new TemporaryDirectory();
        var streamingAssets = directory.Combine("Dofus_Data", "StreamingAssets");

        Directory.CreateDirectory(streamingAssets);
        File.WriteAllText(Path.Combine(streamingAssets, "version"), "Version=3.7.12.12\n");

        Assert.Equal("3.7.12.12", ClientBuild.Read(directory.Path).Version);
    }

    [Fact]
    public void AClientWithoutAVersionFileIsReported()
    {
        using var directory = new TemporaryDirectory();

        Assert.Throws<FileNotFoundException>(() => ClientBuild.Read(directory.Path));
    }
}
