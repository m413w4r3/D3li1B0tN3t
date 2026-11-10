using D3B.Bot.Hosting;
using D3B.Bot.Characters;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Network;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Bot.Tests.Interactives;

public sealed class SupervisedElementUseConsoleTests
{
    private static async Task FrontierOpenAsync(TestPlay play)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!play.Log.GetSnapshot().Any(record => record.Message.Contains("acceptance frontier open", StringComparison.Ordinal)))
        {
            Assert.True(DateTime.UtcNow < deadline, "The element-use acceptance frontier did not open.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    private static SupervisedElementUseConsole Console(TestPlay play, bool actionEnabled)
    {
        return Console(Session(play), actionEnabled);
    }

    private static SupervisedElementUseConsole Console(SupervisedElementUseSession session, bool actionEnabled)
    {
        return new SupervisedElementUseConsole(session, NullLoggerFactory.Instance)
        {
            ActionEnabled = actionEnabled,
        };
    }

    private static SupervisedElementUseSession Session(TestPlay play, Func<int, string?>? itemName = null)
    {
        return new SupervisedElementUseSession(play.Bots, play.Tables, itemName: itemName);
    }

    private static ElementUseTarget TreeTarget()
    {
        return new ElementUseTarget(TestPlay.MapId, 190, TestPlay.TreeElementId, TestPlay.TreeSkillId);
    }

    private static string[] CandidateLines(string text)
    {
        return text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("P2J ELEMENT CANDIDATE version=", StringComparison.Ordinal))
            .ToArray();
    }

    private static int CandidateId(string line)
    {
        var element = line.Split(' ').Single(token => token.StartsWith("element=", StringComparison.Ordinal));

        return int.Parse(element["element=".Length..], System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task TheConsoleDoesNothingWithoutACommand()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var console = Console(play, actionEnabled: false);
        var output = new StringWriter();

        await console.RunAsync(new StringReader(string.Empty), output, TestContext.Current.CancellationToken);

        Assert.Empty(connection.Sent);
        Assert.Contains("P2J supervised element use enabled. Commands: p2j element readiness <mapId> <cell> <elementId> <skillId> | p2j element use <mapId> <cell> <elementId> <skillId> | p2j element candidates", output.ToString());
    }

    [Fact]
    public async Task AReadinessCommandPrintsThePassivePreparation()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var console = Console(play, actionEnabled: false);
        var input = $"p2j element readiness {TestPlay.MapId} 190 {TestPlay.TreeElementId} {TestPlay.TreeSkillId}\n";
        var output = new StringWriter();

        await console.RunAsync(new StringReader(input), output, TestContext.Current.CancellationToken);

        Assert.Empty(connection.Sent);
        var text = output.ToString();
        Assert.Contains("action=false", text);
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
        var console = Console(play, actionEnabled: true);
        var output = new StringWriter();
        var ct = TestContext.Current.CancellationToken;
        var running = console.RunAsync(new StringReader(command + command), output, ct);

        Assert.IsType<InteractiveUseRequest>(await connection.SentAsync(1));
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        await bot.ProcessedAsync();
        while (!running.IsCompleted)
        {
            play.Time.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Delay(5, ct);
        }
        await running;

        var text = output.ToString();
        Assert.Contains("P2J ELEMENT USE outcome=PASS_HARVEST reason=PASS", text);
        Assert.Contains("gain=3", text);
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
    public async Task AUseCommandIsRefusedWhenTheActionGateIsClosed()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var console = Console(play, actionEnabled: false);
        var input = $"p2j element use {TestPlay.MapId} 190 {TestPlay.TreeElementId} {TestPlay.TreeSkillId}\n";
        var output = new StringWriter();

        await console.RunAsync(new StringReader(input), output, TestContext.Current.CancellationToken);

        Assert.Empty(connection.Sent);
        Assert.Contains("P2J ELEMENT COMMAND refused", output.ToString());
        Assert.Contains("action=false", output.ToString());
    }

    [Fact]
    public async Task AnInvalidElementCommandSendsNothing()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var console = Console(play, actionEnabled: false);
        var input = "p2j element use\np2j element move 1 2 3 4\np2j element readiness nope 1 2 3\n";
        var output = new StringWriter();

        await console.RunAsync(new StringReader(input), output, TestContext.Current.CancellationToken);

        Assert.Empty(connection.Sent);
        Assert.Contains("P2J ELEMENT COMMAND invalid:", output.ToString());
    }

    [Fact]
    public async Task TheCandidatesCommandIsPassiveWhenTheActionIsDisabled()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var session = Session(play, itemId => itemId is TestPlay.Ash ? "Ash \"tree\"" : null);
        var console = Console(session, actionEnabled: false);
        var input = $"p2j element candidates\np2j element use {TestPlay.MapId} 190 {TestPlay.TreeElementId} {TestPlay.TreeSkillId}\n";
        var output = new StringWriter();

        await console.RunAsync(new StringReader(input), output, TestContext.Current.CancellationToken);

        var text = output.ToString();
        Assert.Contains("action_enabled=false", text);
        Assert.Contains("target=TARGET_IDENTIFIED", text);
        Assert.Contains("action=ACTION_BLOCKED action_reason=ACTION_DISABLED", text);
        Assert.Contains("identity=Verified", text);
        Assert.Contains("item_name=\"Ash \\\"tree\\\"\"", text);
        Assert.Contains("visual_identity=unconfirmed", text);
        Assert.Contains("prep=Ready", text);
        Assert.Contains("P2J ELEMENT COMMAND refused", text);
        Assert.Empty(connection.Sent);
        Assert.Null(bot.Activity);
        Assert.False(session.Prepare(TreeTarget()).AttemptConsumed);
    }

    [Fact]
    public async Task AClosedTranslationLatchKeepsCandidateDiagnostics()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        connection.ServerRaw(TestGame.UnknownCarriedEvent());
        await bot.ProcessedAsync();
        var console = Console(play, actionEnabled: true);
        var output = new StringWriter();

        await console.RunAsync(new StringReader("p2j element candidates\n"), output, TestContext.Current.CancellationToken);

        var text = output.ToString();
        Assert.Contains("translation_blocked=true", text);
        Assert.Contains("identity=Verified", text);
        Assert.Contains("prep=Blocked prep_reason=TRANSLATION_LATCH_CLOSED", text);
        Assert.Contains("action=ACTION_BLOCKED action_reason=TRANSLATION_LATCH_CLOSED", text);
        Assert.DoesNotContain("prep=Ready", text);
        Assert.DoesNotContain("action=ACTION_READY", text);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task TheCandidatesCommandRefusesArguments()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var console = Console(play, actionEnabled: false);
        var output = new StringWriter();

        await console.RunAsync(new StringReader("p2j element candidates 1\n"), output, TestContext.Current.CancellationToken);

        Assert.Contains("P2J ELEMENT COMMAND invalid: candidates takes no arguments", output.ToString());
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task TheCandidatesOutputIsDeterministic()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var information = TestPlay.Information(190);
        information.InteractiveElements.Add(new InteractiveElement
        {
            ElementId = 600_001,
            InteractiveId = 34,
            OnCurrentMap = true,
            EnabledSkills = { new InteractiveSkill { SkillId = TestPlay.TreeSkillId, InstanceId = 9_999 } },
        });
        connection.Server(information);
        await bot.ProcessedAsync();
        var console = Console(play, actionEnabled: false);
        var output = new StringWriter();

        await console.RunAsync(new StringReader("p2j element candidates\np2j element candidates\n"), output, TestContext.Current.CancellationToken);

        var lines = CandidateLines(output.ToString());
        var first = lines[..(lines.Length / 2)];
        var second = lines[(lines.Length / 2)..];
        Assert.Equal(4, lines.Length);
        Assert.Equal(first, second);
        Assert.Equal([TestPlay.TreeElementId, 600_001], first.Select(CandidateId));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task TheCandidatesCommandReportsNoConnectedBot()
    {
        await using var play = new TestPlay();
        var console = Console(play, actionEnabled: false);
        var output = new StringWriter();

        await console.RunAsync(new StringReader("p2j element candidates\n"), output, TestContext.Current.CancellationToken);

        Assert.Contains("reason=NO_CONNECTED_BOT", output.ToString());
        Assert.Contains("candidates=0", output.ToString());
        Assert.Contains("target=TARGET_UNKNOWN", output.ToString());
        Assert.Contains("action=ACTION_BLOCKED action_reason=NO_CONNECTED_BOT", output.ToString());
    }

    [Fact]
    public async Task TheCandidatesCommandReportsAmbiguousBots()
    {
        await using var play = new TestPlay();
        var (firstConnection, _) = await play.EnterAsync(190);
        var secondCharacterId = TestPlay.CharacterId + 1;
        var secondAccount = TestPlay.Account with { Id = TestPlay.AccountId + 1 };
        var secondConnection = play.Connect(secondAccount);
        secondConnection.Server(TestPlay.Chosen(secondCharacterId));
        var information = TestPlay.Information(190);
        information.Actors.Single(actor => actor.Id == TestPlay.CharacterId).Id = secondCharacterId;
        secondConnection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        secondConnection.Server(information);
        Assert.True(play.Bots.TryGet(new CharacterKey(secondAccount.Id, secondCharacterId), out var secondBot));
        await secondBot.ProcessedAsync();
        var console = Console(play, actionEnabled: false);
        var output = new StringWriter();

        await console.RunAsync(new StringReader("p2j element candidates\n"), output, TestContext.Current.CancellationToken);

        Assert.Contains("reason=AMBIGUOUS_BOT", output.ToString());
        Assert.Contains("candidates=0", output.ToString());
        Assert.Contains("action_reason=AMBIGUOUS_BOT", output.ToString());
        Assert.Empty(firstConnection.Sent);
        Assert.Empty(secondConnection.Sent);
    }

    [Fact]
    public async Task TheCandidatesCommandDoesNotConsumeTheOneShot()
    {
        await using var play = new TestPlay();
        var (connection, _) = await play.EnterAsync(190);
        var session = Session(play);
        var console = Console(session, actionEnabled: true);
        var command = $"p2j element candidates\np2j element readiness {TestPlay.MapId} 190 {TestPlay.TreeElementId} {TestPlay.TreeSkillId}\n";
        var output = new StringWriter();

        await console.RunAsync(new StringReader(command), output, TestContext.Current.CancellationToken);

        Assert.Contains("P2J ELEMENT CANDIDATES version=1 reason=READY", output.ToString());
        Assert.Contains("P2J ELEMENT PREP state=Ready reason=READY consumed=false", output.ToString());
        Assert.False(session.AttemptConsumed);
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task TheConsoleDoesNotReportSuccessWhenTheGainIsRemoved()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(190);
        var ct = TestContext.Current.CancellationToken;
        var command = $"p2j element use {TestPlay.MapId} 190 {TestPlay.TreeElementId} {TestPlay.TreeSkillId}\n";
        var console = Console(play, actionEnabled: true);
        var output = new StringWriter();
        var running = console.RunAsync(new StringReader(command), output, ct);

        await connection.SentAsync(1);
        await FrontierOpenAsync(play);
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId, Duration = 30 });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        connection.Server(new ObjectAddedEvent
        {
            Object = new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = TestPlay.Ash, Quantity = 3 }, Position = 63 },
        });
        await bot.ProcessedAsync();
        connection.Server(new ObjectsDeletedEvent { Uids = { 12 } });
        await bot.ProcessedAsync();
        while (!running.IsCompleted)
        {
            play.Time.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Delay(5, ct);
        }
        await running;

        var text = output.ToString();
        Assert.Contains("outcome=UNCONFIRMED", text);
        Assert.DoesNotContain("outcome=PASS_HARVEST", text);
        Assert.Single(connection.Sent);
    }
}
