using D3B.Data.Releases;

namespace D3B.Data.Tests.Releases;

public sealed class ClientReleaseTests
{
    private const string Hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    [Fact]
    public void AReleaseIsTaggedAfterItsClientVersion()
    {
        Assert.Equal("client-3.7.12.12", ClientRelease.TagOf(new ClientBuild("3.7.12.12", null)));
    }

    [Fact]
    public void AChecksumIsWrittenAsSha256sumWritesIt()
    {
        var text = ClientRelease.FormatChecksum(Convert.FromHexString(Hash), "data-3.pack");

        Assert.Equal($"{Hash}  data-3.pack\n", text);
        Assert.Equal(Hash, ClientRelease.ParseChecksum(text, "data-3.pack"));
    }

    [Theory]
    [InlineData($"{Hash} *data-3.pack\n")]
    [InlineData($"{Hash}  data-3.pack\r\n")]
    [InlineData($"{Hash}  data-3.pack")]
    public void AChecksumReadsInEachFormSha256sumWrites(string text)
    {
        Assert.Equal(Hash, ClientRelease.ParseChecksum(text, "data-3.pack"));
    }

    [Theory]
    [InlineData($"{Hash}  data-2.pack\n")]
    [InlineData("9f86d0  data-3.pack\n")]
    [InlineData("zz86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08  data-3.pack\n")]
    [InlineData($"{Hash}  data-3.pack\n{Hash}  data-3.pack\n")]
    [InlineData("")]
    public void AChecksumOfAnythingElseIsRejected(string text)
    {
        Assert.Throws<InvalidDataException>(() => ClientRelease.ParseChecksum(text, "data-3.pack"));
    }

    [Theory]
    [InlineData("AerafalDev/D3B", true)]
    [InlineData("some-owner/some_repo.data", true)]
    [InlineData("AerafalDev", false)]
    [InlineData("AerafalDev/D3B/releases", false)]
    [InlineData("../D3B", false)]
    [InlineData("AerafalDev/D3B?x=1", false)]
    [InlineData("", false)]
    public void ARepositoryIsAnOwnerAndAName(string value, bool expected)
    {
        Assert.Equal(expected, ClientRelease.IsRepository(value));
    }
}
