using D3B.Bot.Hosting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Network;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Bot.Tests.Interactives;

public sealed class HarvestPlanConsoleTests
{
    private static SupervisedElementUseConsole Console(TestPlay play, bool actionEnabled = false)
    {
        return new SupervisedElementUseConsole(new SupervisedElementUseSession(play.Bots, play.Tables), NullLoggerFactory.Instance)
        {
            ActionEnabled = actionEnabled,
        };
    }

    private static async Task SetReadinessAsync(TestConnection connection, D3B.Bot.Runtime.BotActor bot, int? weight = 100)
    {
        if (weight is { } value)
            connection.Server(new InventoryWeightEvent { Weight = value, MaxWeight = 1_000 });

        connection.Server(new JobExperiencesEvent
        {
            Jobs = { new JobExperience { JobId = TestPlay.Lumberjack, Level = 1 } },
        });
        await bot.ProcessedAsync();
    }

    private static async Task<string> RunAsync(SupervisedElementUseConsole console, string command)
    {
        var output = new StringWriter();
        await console.RunAsync(new StringReader(command), output, TestContext.Current.CancellationToken);
        return output.ToString();
    }

    [Fact]
    public async Task AReadyPlanIsPassiveAndReportsItsInputs()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot);
        var console = Console(play);

        var output = await RunAsync(console, $"p2j harvest plan {TestPlay.MapId} {TestPlay.Ash}\n");

        Assert.Contains("P2J HARVEST PLAN version=1 verdict=PLAN_READY reason=PLAN_READY", output);
        Assert.Contains("start_map=191104000 destination_map=191104002 transitions=1 transition_types=Scroll", output);
        Assert.Contains("candidates=0 target=NOT_VISIBLE job=OK job_level=1 required_level=1", output);
        Assert.Contains("weight=100 max_weight=1000 free_pods=900 load_percent=10 threshold=90", output);
        Assert.Contains("translation_blocked=false missing=TARGET_NOT_VISIBLE_UNTIL_ARRIVAL", output);
        Assert.Contains("action=false", output);
        Assert.Empty(connection.Sent);
        Assert.Null(bot.Activity);
    }

    [Fact]
    public async Task APlanReportsBlockedReasonsWithoutRequests()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        var console = Console(play);
        var noWeight = await RunAsync(console, $"p2j harvest plan {TestPlay.MapId} {TestPlay.Ash}\n");
        Assert.Contains("verdict=PLAN_BLOCKED reason=WEIGHT_NOT_OBSERVED", noWeight);

        await SetReadinessAsync(connection, bot);
        connection.ServerRaw(TestGame.UnknownCarriedEvent());
        await bot.ProcessedAsync();
        var latch = await RunAsync(console, $"p2j harvest plan {TestPlay.MapId} {TestPlay.Ash}\n");
        Assert.Contains("verdict=PLAN_BLOCKED reason=TRANSLATION_LATCH_CLOSED", latch);
        Assert.Contains("translation_blocked=true", latch);
        Assert.Empty(connection.Sent);
        Assert.Null(bot.Activity);
    }

    [Fact]
    public async Task AnUnreachableMapBlocksThePlan()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot);

        var output = await RunAsync(Console(play), "p2j harvest plan 42 303\n");

        Assert.Contains("verdict=PLAN_BLOCKED reason=NO_PATH", output);
        Assert.Contains("missing=NO_PATH,TARGET_NOT_VISIBLE_UNTIL_ARRIVAL", output);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AnUnknownCatalogItemBlocksThePlan()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.NorthMapId);
        await SetReadinessAsync(connection, bot);

        var output = await RunAsync(Console(play), $"p2j harvest plan {TestPlay.MapId} 999999\n");

        Assert.Contains("verdict=PLAN_BLOCKED reason=CATALOG_UNKNOWN", output);
        Assert.Contains("job=CATALOG_UNKNOWN", output);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task AVisibleTargetReportsItsApproach()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190, TestPlay.MapId);
        await SetReadinessAsync(connection, bot);

        var output = await RunAsync(Console(play), $"p2j harvest plan {TestPlay.MapId} {TestPlay.Ash}\n");
        var line = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Single(value => value.StartsWith("P2J HARVEST PLAN", StringComparison.Ordinal));

        Assert.Contains("candidates=1 target=IDENTIFIED", line);
        Assert.Contains("use_cell=", line);
        Assert.DoesNotContain("use_cell=unknown", line);
        Assert.Contains("path=", line);
        Assert.DoesNotContain("path=unknown", line);
        Assert.Empty(connection.Sent);
        Assert.Null(bot.Activity);
    }

    [Theory]
    [InlineData("p2j harvest plan", "harvest plan requires a destinationMapId and wantedItemId")]
    [InlineData("p2j harvest plan 1", "harvest plan requires a destinationMapId and wantedItemId")]
    [InlineData("p2j harvest plan nope 303", "destinationMapId must be a positive integer")]
    [InlineData("p2j harvest plan 1 0", "wantedItemId must be a positive integer")]
    [InlineData("p2j harvest plan 1 303 extra", "harvest plan requires a destinationMapId and wantedItemId")]
    public async Task InvalidPlanArgumentsAreRefused(string command, string reason)
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);

        var output = await RunAsync(Console(play), command + "\n");

        Assert.Contains($"P2J ELEMENT COMMAND invalid: {reason}; usage:", output);
        Assert.Contains("p2j harvest plan <destinationMapId> <wantedItemId>", output);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task PlanningDoesNotConsumeTheOneShot()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190, TestPlay.MapId);
        await SetReadinessAsync(connection, bot);
        var session = new SupervisedElementUseSession(play.Bots, play.Tables);

        var plan = session.PlanHarvest(TestPlay.MapId, TestPlay.Ash, thresholdPercent: 90);

        Assert.Equal("PLAN_READY", plan.Verdict);
        Assert.False(session.AttemptConsumed);
        Assert.Empty(connection.Sent);
        Assert.Null(bot.Activity);
    }
}
