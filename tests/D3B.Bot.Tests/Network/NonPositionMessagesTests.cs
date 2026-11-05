using D3B.Bot.Network;
using D3B.Network.Relay;

namespace D3B.Bot.Tests.Network;

public sealed class NonPositionMessagesTests
{
    [Fact]
    public void TheBuildsProvenNonPositionMessagesAreListed()
    {
        Assert.Contains("knb", NonPositionMessages.Messages);
        Assert.Contains("lok", NonPositionMessages.Messages);
        Assert.Contains("hmd", NonPositionMessages.Messages);
        Assert.Equal(25, NonPositionMessages.Messages.Count);
        Assert.Equal(7, NonPositionMessages.Messages.Count(message => message.Value.FromClient));
    }

    [Fact]
    public void TheExactBuildAndSideSetIsPinned()
    {
        string[] expected =
        [
            "hgf:server", "hgg:server", "hgh:server", "hla:server", "hlf:client", "hlw:server", "hmd:server",
            "kbi:server", "kcf:client", "kes:server", "kkn:client", "klu:client", "kmj:client", "kna:server",
            "knb:server", "kni:server", "kpu:client", "kpv:server", "krm:server", "krw:server", "lnt:client",
            "lok:server", "lsh:server", "lwn:server", "meq:server",
        ];
        var actual = NonPositionMessages.Messages
            .Select(message => message.Key + (message.Value.FromClient ? ":client" : ":server"))
            .Order(StringComparer.Ordinal);

        Assert.Equal(expected.Order(StringComparer.Ordinal), actual);
    }

    [Fact]
    public void AMessageOutsideTheEvidenceStaysBlocking()
    {
        Assert.DoesNotContain("hlv", NonPositionMessages.Messages);
        Assert.DoesNotContain("hmj", NonPositionMessages.Messages);
        Assert.DoesNotContain("hpe", NonPositionMessages.Messages);
        Assert.DoesNotContain("jpx", NonPositionMessages.Messages);
        Assert.DoesNotContain("jxh", NonPositionMessages.Messages);
        Assert.DoesNotContain("ltb", NonPositionMessages.Messages);
        Assert.DoesNotContain("jpe", NonPositionMessages.Messages);
        Assert.DoesNotContain("kne", NonPositionMessages.Messages);
        Assert.DoesNotContain("ifg", NonPositionMessages.Messages);
        Assert.DoesNotContain("ipe", NonPositionMessages.Messages);
        Assert.DoesNotContain("ira", NonPositionMessages.Messages);
        Assert.DoesNotContain("ism", NonPositionMessages.Messages);
        Assert.DoesNotContain("kjm", NonPositionMessages.Messages);
        Assert.DoesNotContain("kmt", NonPositionMessages.Messages);
        Assert.DoesNotContain("kxn", NonPositionMessages.Messages);
        Assert.DoesNotContain("lxb", NonPositionMessages.Messages);
        Assert.DoesNotContain("lwy", NonPositionMessages.Messages);
    }

    [Fact]
    public void AnotherBuildDoesNotInheritTheList()
    {
        Assert.True(NonPositionMessages.IsNonPosition(NonPositionMessages.ClientVersion, "knb", RelayDirection.FromServer));
        Assert.False(NonPositionMessages.IsNonPosition("3.7.12.12", "knb", RelayDirection.FromServer));
        Assert.False(NonPositionMessages.IsNonPosition(NonPositionMessages.ClientVersion, null, RelayDirection.FromServer));
        Assert.False(NonPositionMessages.IsNonPosition(NonPositionMessages.ClientVersion, "unknown", RelayDirection.FromServer));
    }

    [Fact]
    public void AMessageFromTheOtherSideIsContradictory()
    {
        Assert.False(NonPositionMessages.IsNonPosition(NonPositionMessages.ClientVersion, "knb", RelayDirection.FromClient));
        Assert.True(NonPositionMessages.IsNonPosition(NonPositionMessages.ClientVersion, "hlf", RelayDirection.FromClient));
        Assert.False(NonPositionMessages.IsNonPosition(NonPositionMessages.ClientVersion, "hlf", RelayDirection.FromServer));
    }
}
