using D3B.Bot.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Hosting;

public sealed partial class HostProblemsTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 29, 4, 17, 11, TimeSpan.Zero));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Game connection {ConnectionId}: its account is not known, so no bot plays its character")]
    private static partial void AccountUnknown(ILogger logger, string connectionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Bot {Character}: activity {Activity} failed")]
    private static partial void ActivityFailed(ILogger logger, long character, string activity, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Listed {Count} bots")]
    private static partial void Listed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "The relay failed")]
    private static partial void RelayFailed(ILogger logger, Exception exception);

    [Fact]
    public void TheHostsWarningsAndErrorsAreKeptAsTheLogWroteThem()
    {
        using var problems = new HostProblems(_time);
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(problems).SetMinimumLevel(LogLevel.Debug));
        var log = factory.CreateLogger("D3B.Bot.Runtime.Bots");

        AccountUnknown(log, "7");
        RelayFailed(log, new IOException("The pipe broke."));

        var kept = problems.Problems.All();

        Assert.Equal(2, kept.Count);
        Assert.Equal(new HostProblem(_time.GetUtcNow(), LogLevel.Warning, "D3B.Bot.Runtime.Bots", "Game connection 7: its account is not known, so no bot plays its character", null), kept[0]);
        Assert.Equal(LogLevel.Error, kept[1].Level);
        Assert.Contains("The pipe broke.", kept[1].Exception, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatNamesABotOrInformsIsNotKept()
    {
        using var problems = new HostProblems(_time);
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(problems).SetMinimumLevel(LogLevel.Debug));
        var log = factory.CreateLogger("D3B.Bot.Runtime.BotActor");

        // The bot's journal says it already, in the app's words.
        ActivityFailed(log, 70_001, "promenade", new InvalidOperationException("The step failed."));
        Listed(log, 2);

        Assert.Empty(problems.Problems.All());
    }
}
