using D3B.Data.Tests;
using D3B.Protocol.Mapping;
using Google.Protobuf.Reflection;

namespace D3B.Protocol.Tests.Mapping;

public sealed class PinnedPairsFileTests
{
    private static IEnumerable<MessageDescriptor> WithNested(MessageDescriptor message)
    {
        return [message, .. message.NestedTypes.SelectMany(WithNested)];
    }

    [Fact]
    public void EveryCommittedPinNamesItsBuildAndTheReadableProtocol()
    {
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Pins"), "*.json");
        var messages = ReadableProtocol.Files.SelectMany(file => file.MessageTypes).SelectMany(WithNested).ToList();
        var messageNames = messages.Select(message => message.FullName).ToHashSet();
        var enumNames = ReadableProtocol.Files.SelectMany(file => file.EnumTypes).Concat(messages.SelectMany(message => message.EnumTypes)).Select(type => type.FullName).ToHashSet();

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var pins = PinnedPairsFile.Read(file, Path.GetFileNameWithoutExtension(file));

            Assert.All(pins.Messages, pin => Assert.Contains(pin.Readable, messageNames));
            Assert.All(pins.Messages, pin => Assert.All(pin.Fields, field => Assert.NotNull(messages.Single(message => message.FullName == pin.Readable).FindFieldByNumber(field.Readable))));
            Assert.All(pins.Enums, pin => Assert.Contains(pin.Readable, enumNames));
        }
    }

    [Fact]
    public void PinsNamingAnotherBuildAreRefused()
    {
        using var directory = new TemporaryDirectory();

        var path = PinnedPairsFile.PathOf(directory.Path, "1.2.3.4");

        File.WriteAllText(path, """{ "clientVersion": "1.2.3.5" }""");

        Assert.Throws<InvalidDataException>(() => PinnedPairsFile.Read(path, "1.2.3.4"));
    }

    [Fact]
    public void PinsReadTheirPairsAndFields()
    {
        using var directory = new TemporaryDirectory();

        var path = PinnedPairsFile.PathOf(directory.Path, "1.2.3.4");

        File.WriteAllText(path, """
            {
              "clientVersion": "1.2.3.4",
              "messages": [{ "readable": "d3b.connection.Ping", "obfuscated": "abc", "fields": [{ "readable": 2, "obfuscated": 7 }] }],
              "enums": [{ "readable": "d3b.connection.ServerType", "obfuscated": "abd" }]
            }
            """);

        var pins = PinnedPairsFile.Read(path, "1.2.3.4");

        Assert.Equal("abc", Assert.Single(pins.Messages).Obfuscated);
        Assert.Equal(7, Assert.Single(pins.Messages[0].Fields).Obfuscated);
        Assert.Equal("abd", Assert.Single(pins.Enums).Obfuscated);
    }
}
