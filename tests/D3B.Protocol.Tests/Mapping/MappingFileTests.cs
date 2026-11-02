using D3B.Data.Tests;
using D3B.Protocol.Mapping;
using Google.Protobuf;

namespace D3B.Protocol.Tests.Mapping;

public sealed class MappingFileTests
{
    [Fact]
    public void AMappingIsKeptBesideItsBuildsPack()
    {
        Assert.Equal(Path.Combine("data", "3.7.12.12", $"mapping-{MappingFile.FormatVersion}.binpb"), MappingFile.PathOf("data", "3.7.12.12"));
    }

    [Fact]
    public void AMappingReadsBackAsWritten()
    {
        using var directory = new TemporaryDirectory();

        var mapping = new ProtocolMapping
        {
            FormatVersion = MappingFile.FormatVersion,
            ClientVersion = "3.7.12.12",
            SchemaSha256 = ByteString.CopyFrom(1, 2, 3),
            Messages = { new MessagePair { Readable = "d3b.connection.Ping", Obfuscated = "abc", Evidence = MatchEvidence.Field } },
        };
        var path = MappingFile.PathOf(directory.Path, "3.7.12.12");
        var bytes = MappingFile.Write(path, mapping);

        Assert.Equal(mapping, MappingFile.Read(path));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".tmp"));
    }
}
