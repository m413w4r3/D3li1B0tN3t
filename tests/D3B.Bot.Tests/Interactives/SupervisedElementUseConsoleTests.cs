using D3B.Bot.Hosting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Bot.Tests.Interactives;

public sealed class SupervisedElementUseConsoleTests
{
    [Fact]
    public async Task TheConsoleDoesNothingWithoutACommand()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var console = new SupervisedElementUseConsole(new SupervisedElementUseSession(play.Bots, play.Tables), NullLoggerFactory.Instance);
        var output = new StringWriter();

        await console.RunAsync(new StringReader(string.Empty), output, TestContext.Current.CancellationToken);

        Assert.Empty(connection.Sent);
        Assert.Contains("P2J supervised element use enabled. Commands: p2j element readiness <mapId> <cell> <elementId> <skillId> | p2j element use <mapId> <cell> <elementId> <skillId>", output.ToString());
    }

    [Fact]
    public async Task AReadinessCommandPrintsThePassivePreparation()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var console = new SupervisedElementUseConsole(new SupervisedElementUseSession(play.Bots, play.Tables), NullLoggerFactory.Instance);
        var input = $"p2j element readiness {TestPlay.MapId} 190 {TestPlay.TreeElementId} {TestPlay.TreeSkillId}\n";
        var output = new StringWriter();

        await console.RunAsync(new StringReader(input), output, TestContext.Current.CancellationToken);

        Assert.Empty(connection.Sent);
        var text = output.ToString();
        Assert.Contains("P2J ELEMENT PREP state=Ready reason=READY", text);
        Assert.Contains("consumed=", text);
        Assert.Contains("session_epoch=", text);
        Assert.Contains("state_revision=", text);
        Assert.Contains("connected=", text);
        Assert.Contains("session=", text);
        Assert.Contains("translation_blocked=", text);
        Assert.Contains("paused=", text);
        Assert.Contains("acting=", text);
        Assert.Contains("input_mode=", text);
        Assert.Contains("map=", text);
        Assert.Contains("cell=", text);
        Assert.Contains("provenance=", text);
        Assert.Contains("trusted=", text);
        Assert.Contains("element=", text);
        Assert.Contains("interactive=", text);
        Assert.Contains("element_cell=", text);
        Assert.Contains("gfx=", text);
        Assert.Contains("skill=", text);
        Assert.Contains("instance=", text);
        Assert.Contains("item=", text);
        Assert.Contains("min_level=", text);
        Assert.Contains("uses_from_current_cell=true", text);
    }

    [Fact]
    public async Task AUseCommandPrintsPassHarvestAndRefusesTheSecondUse()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var command = $"p2j element use {TestPlay.MapId} 190 {TestPlay.TreeElementId} {TestPlay.TreeSkillId}\n";
        var console = new SupervisedElementUseConsole(new SupervisedElementUseSession(play.Bots, play.Tables), NullLoggerFactory.Instance);
        var output = new StringWriter();
        var running = console.RunAsync(new StringReader(command + command), output, TestContext.Current.CancellationToken);

        Assert.IsType<InteractiveUseRequest>(await connection.SentAsync(1));
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        await bot.ProcessedAsync();
        await running;

        var text = output.ToString();
        Assert.Contains("P2J ELEMENT USE outcome=PASS_HARVEST reason=PASS", text);
        Assert.Contains("request_sent=", text);
        Assert.Contains("request_may_have_been_sent=", text);
        Assert.Contains("used=", text);
        Assert.Contains("ended=", text);
        Assert.Contains("target_map=", text);
        Assert.Contains("target_cell=", text);
        Assert.Contains("element=", text);
        Assert.Contains("skill=", text);
        Assert.Contains("instance=", text);
        Assert.Contains("item=", text);
        Assert.Contains("inventory_before=", text);
        Assert.Contains("inventory_after=", text);
        Assert.Contains("gain=", text);
        Assert.Contains("translation_blocked=", text);
        Assert.Contains("input_mode=", text);
        Assert.Contains("detail=", text);
        Assert.Contains("outcome=BLOCKED reason=ALREADY_CONSUMED", text);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AnInvalidElementCommandSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var console = new SupervisedElementUseConsole(new SupervisedElementUseSession(play.Bots, play.Tables), NullLoggerFactory.Instance);
        var input = "p2j element use\np2j element move 1 2 3 4\np2j element readiness nope 1 2 3\n";
        var output = new StringWriter();

        await console.RunAsync(new StringReader(input), output, TestContext.Current.CancellationToken);

        Assert.Empty(connection.Sent);
        Assert.Contains("P2J ELEMENT COMMAND invalid:", output.ToString());
    }
}
