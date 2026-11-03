using System.Text;
using D3B.Data.Tests;
using D3B.Network.Clients;

namespace D3B.Network.Tests.Clients;

public sealed class ClientVersionFileTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private string Path => _directory.Combine("version");

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void TheConfigurationsUrlIsReadFromItsLine()
    {
        File.WriteAllText(Path, "Version=3.7.12.12\nConfigUrl=https://cdn.test/config/dofus3.json\n");

        Assert.Equal("https://cdn.test/config/dofus3.json", ClientVersionFile.ReadConfigUrl(Path));
    }

    [Fact]
    public void AFileWithoutTheLineHasNoUrl()
    {
        File.WriteAllText(Path, "Version=3.7.12.12\n");

        Assert.Null(ClientVersionFile.ReadConfigUrl(Path));
        Assert.Throws<InvalidDataException>(() => ClientVersionFile.WriteConfigUrl(Path, "file:///C:/d3b/config.json"));
    }

    [Theory]
    [InlineData("Version=3.7.12.12\nBuildDate=2026-10-19\nConfigUrl=https://cdn.test/a.json\n", "Version=3.7.12.12\nBuildDate=2026-10-19\nConfigUrl=file:///C:/d3b/config.json\n")]
    [InlineData("Version=3.7.12.12\r\nConfigUrl=https://cdn.test/a.json\r\nOther=1", "Version=3.7.12.12\r\nConfigUrl=file:///C:/d3b/config.json\r\nOther=1")]
    public void WritingTheUrlChangesItsLineAndNoOtherByte(string before, string after)
    {
        File.WriteAllText(Path, before, Encoding.Latin1);

        ClientVersionFile.WriteConfigUrl(Path, "file:///C:/d3b/config.json");

        Assert.Equal(after, File.ReadAllText(Path, Encoding.Latin1));
    }

    [Fact]
    public void AWritePreservesArbitraryLatin1BytesOutsideTheConfigUrlLine()
    {
        byte[] before = [.. "Version=3.7.12.12\nBuildDate=2026-10-19"u8, 0xE9, .. "\nConfigUrl=https://cdn.test/a.json\n"u8];
        byte[] after = [.. "Version=3.7.12.12\nBuildDate=2026-10-19"u8, 0xE9, .. "\nConfigUrl=file:///C:/d3b/config.json\n"u8];
        File.WriteAllBytes(Path, before);

        ClientVersionFile.WriteConfigUrl(Path, "file:///C:/d3b/config.json");

        Assert.Equal(after, File.ReadAllBytes(Path));
    }

    [Fact]
    public void AByteOrderMarkIsKept()
    {
        byte[] before = [0xEF, 0xBB, 0xBF, .. "Version=3.7.12.12\nConfigUrl=https://cdn.test/a.json\n"u8];

        File.WriteAllBytes(Path, before);

        Assert.Equal("https://cdn.test/a.json", ClientVersionFile.ReadConfigUrl(Path));

        ClientVersionFile.WriteConfigUrl(Path, "file:///C:/d3b/config.json");

        Assert.Equal([0xEF, 0xBB, 0xBF, .. "Version=3.7.12.12\nConfigUrl=file:///C:/d3b/config.json\n"u8], File.ReadAllBytes(Path));
    }

    [Fact]
    public void AStagedWriteFailureLeavesTheOriginalFileAndRemovesItsTemporaryFile()
    {
        byte[] original = [0xEF, 0xBB, 0xBF, .. "Version=3.7.12.12\nConfigUrl=https://cdn.test/a.json\n"u8];
        File.WriteAllBytes(Path, original);

        // Injection covers the staging/commit boundary deterministically; the public success test below exercises the real File.Move replacement.
        // Windows File.Move failure and ACL behavior remain unverified, as documented in RUNTIME_ARTIFACTS.md.
        var exception = Assert.Throws<IOException>(() => ClientVersionFile.WriteConfigUrl(Path, "file:///C:/d3b/config.json", (temporaryPath, destinationPath) =>
        {
            Assert.NotEqual(destinationPath, temporaryPath);
            Assert.Equal(System.IO.Path.GetDirectoryName(Path), System.IO.Path.GetDirectoryName(temporaryPath));
            Assert.Equal(Path, destinationPath);
            Assert.True(File.Exists(temporaryPath));
            throw new IOException("Injected staged-write failure.");
        }));

        Assert.Equal("Injected staged-write failure.", exception.Message);
        Assert.Equal(original, File.ReadAllBytes(Path));
        Assert.Equal("https://cdn.test/a.json", ClientVersionFile.ReadConfigUrl(Path));
        Assert.Equal(Path, Assert.Single(Directory.EnumerateFiles(_directory.Path)));
    }

    [Fact]
    public void ACommitCallbackRunsOnceInTheDestinationDirectoryAndRemovesItsTemporaryFile()
    {
        File.WriteAllText(Path, "ConfigUrl=https://cdn.test/a.json\n");
        var callbackCount = 0;
        string? temporaryPath = null;

        ClientVersionFile.WriteConfigUrl(Path, "file:///C:/d3b/config.json", (stagedPath, destinationPath) =>
        {
            callbackCount++;
            temporaryPath = stagedPath;

            Assert.NotEqual(destinationPath, stagedPath);
            Assert.Equal(System.IO.Path.GetDirectoryName(Path), System.IO.Path.GetDirectoryName(stagedPath));
            Assert.Equal(Path, destinationPath);
            Assert.True(File.Exists(stagedPath));

            File.Move(stagedPath, destinationPath, overwrite: true);
        });

        Assert.Equal(1, callbackCount);
        Assert.NotNull(temporaryPath);
        Assert.False(File.Exists(temporaryPath));
        Assert.Equal("ConfigUrl=file:///C:/d3b/config.json\n", File.ReadAllText(Path));
        Assert.Equal(Path, Assert.Single(Directory.EnumerateFiles(_directory.Path)));
    }

    [Fact]
    public void ASuccessfulWriteRemovesItsTemporaryFile()
    {
        File.WriteAllText(Path, "ConfigUrl=https://cdn.test/a.json\n");

        ClientVersionFile.WriteConfigUrl(Path, "file:///C:/d3b/config.json");

        Assert.Equal(Path, Assert.Single(Directory.EnumerateFiles(_directory.Path)));
    }

    [Fact]
    public void AWriteKeepsTheUnixFileMode()
    {
        if (OperatingSystem.IsWindows())
            return;

        File.WriteAllText(Path, "ConfigUrl=https://cdn.test/a.json\n");
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        File.SetUnixFileMode(Path, mode);

        ClientVersionFile.WriteConfigUrl(Path, "file:///C:/d3b/config.json");

        Assert.Equal(mode, File.GetUnixFileMode(Path));
    }

    [Fact]
    public void AUrlNeverAddsALine()
    {
        File.WriteAllText(Path, "ConfigUrl=https://cdn.test/a.json\n");

        Assert.Throws<ArgumentException>(() => ClientVersionFile.WriteConfigUrl(Path, "https://cdn.test/a.json\nVersion=1"));
    }
}
