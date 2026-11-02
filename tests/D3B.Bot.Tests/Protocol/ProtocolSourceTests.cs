using D3B.Bot.Protocol;
using D3B.Data;
using D3B.Protocol.Connection;
using D3B.Protocol.Translation;

namespace D3B.Bot.Tests.Protocol;

public sealed class ProtocolSourceTests : IDisposable
{
    private readonly TestInstall _install = new();

    public void Dispose()
    {
        _install.Dispose();
    }

    [Fact]
    public void TheAppsOwnSchemaAndMappingAreLoadedWhenNoneAreOnThisMachine()
    {
        _install.WriteProtocol(bundled: true);

        var source = Source();

        Assert.Equal(TestInstall.Version, source.Load().ClientVersion);
        Assert.Equal(_install.BundledMappingPath, source.MappingPath);
    }

    [Fact]
    public void AMappingMadeOnThisMachineWinsOverTheAppsOwn()
    {
        _install.WriteProtocol();
        _install.WriteProtocol(edit: mapping => mapping.Messages.Clear(), bundled: true);

        var source = Source();

        Assert.Equal(TestInstall.Version, source.Load().ClientVersion);
        Assert.Equal(_install.MappingPath, source.MappingPath);
    }

    [Fact]
    public void WithoutASchemaTheMessageNamesTheBuildAndHowToMapIt()
    {
        var exception = Assert.Throws<ProtocolUnavailableException>(() => Source().Load());

        Assert.Equal(TestInstall.Version, exception.MissingBuild);
        Assert.StartsWith($"No schema for client {TestInstall.Version}: none at {_install.SchemaPath}, nor among the app's own in {_install.BundledDirectory}.", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet run --file scripts/schema.cs -- ", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"then dotnet run --file scripts/mapping.cs -- --client \"{_install.ClientDirectory}\" --data \"{_install.PacksDirectory}\"", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMappingThatDoesNotHoldIsRefused()
    {
        _install.WriteProtocol(edit: mapping => mapping.Messages.Remove(mapping.Messages.Single(pair => pair.Readable == Ping.Descriptor.FullName)));

        var exception = Assert.Throws<ProtocolUnavailableException>(() => Source().Load());

        Assert.StartsWith($"The mapping of client {TestInstall.Version} does not hold: ", exception.Message, StringComparison.Ordinal);
        Assert.Contains("d3b.connection.Ping has no counterpart.", Assert.IsType<ProtocolMismatchException>(exception.InnerException).Failures);
        Assert.Contains("dotnet run --file scripts/mapping.cs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMappingMatchedAgainstAnotherSchemaIsRefused()
    {
        _install.WriteProtocol(edit: mapping => mapping.SchemaSha256 = Google.Protobuf.ByteString.CopyFrom(new byte[32]));

        var exception = Assert.Throws<ProtocolUnavailableException>(() => Source().Load());

        Assert.Contains("The mapping was matched against another schema.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMappingOfAnotherBuildOnThisMachineIsRefused()
    {
        _install.WriteProtocol("3.7.11.1");

        var exception = Assert.Throws<ProtocolUnavailableException>(() => Source().Load());

        Assert.Contains($"maps client 3.7.11.1, not {TestInstall.Version}", exception.Message, StringComparison.Ordinal);
    }

    private ProtocolSource Source()
    {
        return new ProtocolSource(_install.Settings, new ClientBuild(TestInstall.Version, null));
    }
}
