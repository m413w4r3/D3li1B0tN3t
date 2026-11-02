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
    public void AByteOrderMarkIsKept()
    {
        byte[] before = [0xEF, 0xBB, 0xBF, .. "Version=3.7.12.12\nConfigUrl=https://cdn.test/a.json\n"u8];

        File.WriteAllBytes(Path, before);

        Assert.Equal("https://cdn.test/a.json", ClientVersionFile.ReadConfigUrl(Path));

        ClientVersionFile.WriteConfigUrl(Path, "file:///C:/d3b/config.json");

        Assert.Equal([0xEF, 0xBB, 0xBF, .. "Version=3.7.12.12\nConfigUrl=file:///C:/d3b/config.json\n"u8], File.ReadAllBytes(Path));
    }

    [Fact]
    public void AUrlNeverAddsALine()
    {
        File.WriteAllText(Path, "ConfigUrl=https://cdn.test/a.json\n");

        Assert.Throws<ArgumentException>(() => ClientVersionFile.WriteConfigUrl(Path, "https://cdn.test/a.json\nVersion=1"));
    }
}
