using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Protocol.Game;
using Microsoft.Extensions.Configuration;

namespace D3B.Bot.Tests.Runtime;

public sealed class BotDelaysTests
{
    private static IConfiguration Configuration(params (string Key, string? Value)[] settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();
    }

    [Fact]
    public void TheMeasuredDelaysAreTheLeastAndTheDisplayTimeGoesUpToThreeSeconds()
    {
        var measured = BotDelays.Measured;

        Assert.Equal(TimeSpan.FromMilliseconds(500), measured.Display);
        Assert.Equal(TimeSpan.FromSeconds(3), measured.Answer);
        Assert.Equal(TimeSpan.FromSeconds(1), measured.WalkArrival);
        Assert.Equal(TimeSpan.FromMilliseconds(500), measured.WalkStep);
        Assert.Equal(TimeSpan.FromSeconds(10), measured.Placed);
        Assert.Equal(TimeSpan.FromSeconds(2), measured.UseEnd);
        Assert.Equal(TimeSpan.FromSeconds(1), measured.BankFollow);
        Assert.Equal(TimeSpan.FromSeconds(30), measured.Recovery);
        Assert.Equal(TimeSpan.FromMilliseconds(900), measured.FightAction);
        Assert.Equal(TimeSpan.FromSeconds(3), BotDelays.Most.Display);
        Assert.Equal(TimeSpan.FromSeconds(3.5), measured.Confirmation(5));
        Assert.Equal(measured, BotDelays.Read(Configuration()));
        Assert.All(measured.ToConfiguration().Values, Assert.Null);
    }

    [Fact]
    public void DelaysAreReadInSecondsAndReadBackFromTheirConfiguration()
    {
        var delays = BotDelays.Read(Configuration((BotDelays.DisplayKey, "1.5"), (BotDelays.RecoveryKey, "60"), (BotDelays.AnswerKey, ""), (BotDelays.FightActionKey, "2")));
        var configuration = delays.ToConfiguration();

        Assert.Equal(BotDelays.Measured with { Display = TimeSpan.FromSeconds(1.5), Recovery = TimeSpan.FromSeconds(60), FightAction = TimeSpan.FromSeconds(2) }, delays);
        Assert.Equal("2", configuration[BotDelays.FightActionKey]);
        Assert.Equal("1.5", configuration[BotDelays.DisplayKey]);
        Assert.Equal("60", configuration[BotDelays.RecoveryKey]);
        Assert.Null(configuration[BotDelays.AnswerKey]);
        Assert.Equal(delays, BotDelays.Read(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build()));
    }

    [Theory]
    [InlineData(BotDelays.DisplayKey, "0.4")]
    [InlineData(BotDelays.DisplayKey, "3.5")]
    [InlineData(BotDelays.AnswerKey, "2")]
    [InlineData(BotDelays.AnswerKey, "00:00:05")]
    [InlineData(BotDelays.RecoveryKey, "NaN")]
    [InlineData(BotDelays.WalkStepKey, "1e400")]
    [InlineData(BotDelays.FightActionKey, "0.5")]
    public void ADelayBelowItsMeasuredValueOrAboveItsMostIsRefused(string key, string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BotDelays.Read(Configuration((key, value))));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARequestOnANewMapWaitsTheDisplayTimeChosenForTheBotsAlreadyMade()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        play.Bots.Delays = BotDelays.Measured with { Display = TimeSpan.FromSeconds(2) };
        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);
        await bot.ProcessedAsync();

        var walk = bot.WalkAsync(300, TestContext.Current.CancellationToken);

        play.Time.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromMilliseconds(1));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Empty(connection.Sent);

        play.Time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.IsType<MapMovementRequest>(await connection.SentAsync(1));
        Assert.False(walk.IsCompleted);
    }

    [Fact]
    public async Task ADisplayTimeRaisedWhileARequestWaitsHoldsItLonger()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        TestWalks.Arrive(connection, TestPlay.NorthMapId, 552);
        await bot.ProcessedAsync();

        var walk = bot.WalkAsync(300, TestContext.Current.CancellationToken);

        await Task.Delay(50, TestContext.Current.CancellationToken);
        play.Bots.Delays = BotDelays.Measured with { Display = TimeSpan.FromSeconds(1) };
        play.Time.Advance(BotDelays.Measured.Display);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Empty(connection.Sent);

        play.Time.Advance(TimeSpan.FromSeconds(1) - BotDelays.Measured.Display);
        Assert.IsType<MapMovementRequest>(await connection.SentAsync(1));
        Assert.False(walk.IsCompleted);
    }

    [Fact]
    public async Task AWalkWaitsTheServersAnswerAsLongAsChosen()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(433);

        play.Bots.Delays = BotDelays.Measured with { Answer = TimeSpan.FromSeconds(6) };

        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await connection.SentAsync(1);

        var sent = play.Time.GetUtcNow();

        Assert.Equal(WalkOutcome.NoAnswer, await TestWalks.AdvanceUntilDoneAsync(play.Time, walk));
        Assert.InRange(play.Time.GetUtcNow() - sent, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(7));
    }
}
