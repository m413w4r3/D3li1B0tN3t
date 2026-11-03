using D3B.Bot.Network;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Bot.Tests.Network;

public sealed class GameLogTests
{
    [Fact]
    public void ADecodedGameMessageLogsItsNameSideLengthAndState()
    {
        var log = new FakeLogCollector();
        var logger = new FakeLogger<GameSession>(log);

        GameLog.Decoded(logger, "connection", "client", "d3b.game.MapMovementEvent", 42, "understood");

        var record = Assert.Single(log.GetSnapshot());

        Assert.Contains("d3b.game.MapMovementEvent", record.Message, StringComparison.Ordinal);
        Assert.Contains("client", record.Message, StringComparison.Ordinal);
        Assert.Contains("42", record.Message, StringComparison.Ordinal);
        Assert.Contains("understood", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownGameMessageIsWarnedAboutWithoutItsPayload()
    {
        var log = new FakeLogCollector();
        var logger = new FakeLogger<GameSession>(log);

        GameLog.NotUnderstood(logger, "connection", "server", "d3b.game.MapMovementEvent", "readable protocol lacks the carried type");

        var record = Assert.Single(log.GetSnapshot());

        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("d3b.game.MapMovementEvent", record.Message, StringComparison.Ordinal);
        Assert.Contains("server", record.Message, StringComparison.Ordinal);
        Assert.Contains("readable protocol lacks", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AReadablePartialMessageIsLoggedAtInformationWithoutByteCounts()
    {
        var log = new FakeLogCollector();
        var logger = new FakeLogger<GameSession>(log);

        GameLog.Partial(logger, "connection", "server", "d3b.game.MapInformationEvent", "readable payload contains fields this schema leaves out");

        var record = Assert.Single(log.GetSnapshot());

        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Contains("partial readable d3b.game.MapInformationEvent", record.Message, StringComparison.Ordinal);
        Assert.Contains("fields this schema leaves out", record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("bytes", record.Message, StringComparison.Ordinal);
    }
}
