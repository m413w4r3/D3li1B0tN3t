using System.Net;
using System.Text.Json.Nodes;
using D3B.Network.Clients;

namespace D3B.Network.Tests.Clients;

public sealed class ClientConfigurationTests
{
    private const string Json = """{"gameAppId":1,"connectionHosts":["Main:connection.test:5555,443"],"chatServerHost":"chat.test","chatServerPort":6337}""";

    [Fact]
    public void TheConnectionHostsAreReadAsTheClientListsThem()
    {
        var host = Assert.Single(ClientConfiguration.Parse(Json).ConnectionHosts);

        Assert.Equal("Main", host.Name);
        Assert.Equal("connection.test", host.Host);
        Assert.Equal([5555, 443], host.Ports);
    }

    [Fact]
    public void RedirectingChangesTheConnectionHostsAndNothingElse()
    {
        var redirected = JsonNode.Parse(ClientConfiguration.Parse(Json).RedirectedTo(IPAddress.Parse("127.0.13.59")))!.AsObject();
        var original = JsonNode.Parse(Json)!.AsObject();

        Assert.Equal("Main:127.0.13.59:5555,443", redirected["connectionHosts"]![0]!.GetValue<string>());
        Assert.Equal(original.Select(property => property.Key), redirected.Select(property => property.Key));

        original.Remove("connectionHosts");
        redirected.Remove("connectionHosts");

        Assert.True(JsonNode.DeepEquals(original, redirected));
    }

    [Theory]
    [InlineData("""{"gameAppId":1}""")]
    [InlineData("""{"connectionHosts":"Main:connection.test:5555"}""")]
    [InlineData("""{"connectionHosts":["Main:connection.test"]}""")]
    [InlineData("""{"connectionHosts":["Main:connection.test:https"]}""")]
    [InlineData("""{"connectionHosts":["Main:connection.test\r\n10.0.0.1:5555"]}""")]
    [InlineData("""{"connectionHosts":[5555]}""")]
    [InlineData("[]")]
    [InlineData("not json")]
    public void AConfigurationNotInTheClientsFormIsRefused(string json)
    {
        Assert.Throws<InvalidDataException>(() => ClientConfiguration.Parse(json));
    }
}
