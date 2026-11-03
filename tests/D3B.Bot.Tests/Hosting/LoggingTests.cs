using D3B.Bot.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Tests.Hosting;

public sealed class LoggingTests
{
    [Fact]
    public void TheSupervisedArgumentEnablesTheGameSessionAtDebug()
    {
        var bot = BotHost.CreateBuilder(["--Logging:LogLevel:D3B.Bot.Network.GameSession", "Debug"]);
        using var host = bot.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("D3B.Bot.Network.GameSession");

        Assert.True(logger.IsEnabled(LogLevel.Debug));
    }
}
