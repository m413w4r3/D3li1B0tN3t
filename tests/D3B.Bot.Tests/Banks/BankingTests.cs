using D3B.Bot.Banks;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Banks;

public sealed class BankingTests
{
    private const long Carried = 1_000;
    private const long Fee = 10;

    private static readonly NpcActionRequest _talk = new() { Action = 3, MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId };

    /// <summary>A worn stack, two resources, and quest tickets the caller keeps, with kamas.</summary>
    private static InventoryContentEvent Inventory()
    {
        return new InventoryContentEvent
        {
            Kamas = Carried,
            Objects =
            {
                new InventoryObject { Item = new ObjectItem { Uid = 30, ItemId = 2_400, Quantity = 1 }, Position = 1 },
                new InventoryObject { Item = new ObjectItem { Uid = 12, ItemId = 303, Quantity = 40 }, Position = 63 },
                new InventoryObject { Item = new ObjectItem { Uid = 13, ItemId = 1_782, Quantity = 21 }, Position = 63 },
                new InventoryObject { Item = new ObjectItem { Uid = 14, ItemId = 30_449, Quantity = 3 }, Position = 63 },
            },
        };
    }

    /// <summary>The banker's dialog opens on his question, offering replies.</summary>
    private static void Asked(TestConnection connection, params int[] replies)
    {
        connection.Server(new NpcDialogCreationEvent { MapId = TestPlay.InsideMapId, NpcActorId = TestPlay.BankerActorId });
        Question(connection, replies);
    }

    private static void Question(TestConnection connection, params int[] replies)
    {
        connection.Server(new NpcDialogQuestionEvent { MessageId = 48_366, Replies = { replies.Select(reply => new NpcReply { ReplyId = reply }) } });
    }

    /// <summary>The reply ends the dialog and opens the bank, its opening paid, in the order of the captures.</summary>
    private static void Opened(TestConnection connection)
    {
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });
        connection.Server(new KamasUpdateEvent { Kamas = Carried - Fee });
        connection.Server(new ExchangeStartedWithStorageEvent { Type = ExchangeType.Storage, MaxSlots = int.MaxValue });
        connection.Server(new StorageContentEvent { Kamas = 5 });
    }

    private static async Task<(TestConnection Connection, BotActor Bot)> InTheBankAsync(TestPlay play)
    {
        var (connection, bot) = await play.EnterAsync(396, TestPlay.InsideMapId);

        connection.Server(Inventory());
        await bot.ProcessedAsync();

        return (connection, bot);
    }

    [Fact]
    public async Task BankingTalksToTheBankerOpensTheBankPutsTheStacksAndKamasInThenLeaves()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var banking = bot.BankAsync(stack => stack.ItemId != 30_449, 100, TestContext.Current.CancellationToken);

        Assert.Equal(_talk, await TestWalks.SentAsync(play, connection, 1));
        Asked(connection, TestPlay.ChestReplyId, TestPlay.OtherReplyId);

        Assert.Equal(new NpcDialogReplyRequest { ReplyId = TestPlay.ChestReplyId }, await TestWalks.SentAsync(play, connection, 2));
        Opened(connection);

        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { 12, 13 } }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new ObjectsDeletedEvent { Uids = { 12, 13 } });
        connection.Server(new StorageObjectsUpdateEvent());
        connection.Server(new InventoryWeightEvent { Weight = 700, MaxWeight = 7_590 });

        Assert.Equal(new ExchangeKamasRequest { Quantity = 100 }, await TestWalks.SentAsync(play, connection, 4));
        connection.Server(new StorageKamasUpdateEvent { Kamas = 105 });
        connection.Server(new KamasUpdateEvent { Kamas = Carried - Fee - 100 });

        Assert.Equal(new DialogLeaveRequest(), await TestWalks.SentAsync(play, connection, 5));
        connection.Server(new ExchangeLeaveEvent { Type = DialogType.Storage });

        // What the client sends by itself once the server ended the exchange.
        connection.Client(new DialogLeaveRequest());
        connection.Client(new DialogLeaveRequest());

        Assert.Equal(BankOutcome.Done, await banking);
        await bot.ProcessedAsync();
        Assert.False(bot.IsPaused);
        Assert.False(bot.State.IsInDialog);
        Assert.Equal([14, 30], bot.State.Inventory.Keys);
        Assert.Equal(Carried - Fee - 100, bot.State.Kamas);
        Assert.Equal(700, bot.State.Weight);
        Assert.Equal(5, connection.Sent.Count);
    }

    [Fact]
    public async Task BankingLetsTheClientShowItsWindowsForTheDisplayTimeChosen()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);

        play.Bots.Delays = BotDelays.Measured with { Display = TimeSpan.FromSeconds(2) };

        var banking = bot.BankAsync(stack => stack.ItemId == 303, 0, TestContext.Current.CancellationToken);

        await TestWalks.SentAsync(play, connection, 1);
        Asked(connection, TestPlay.ChestReplyId);

        var asked = play.Time.GetUtcNow();

        Assert.IsType<NpcDialogReplyRequest>(await TestWalks.SentAsync(play, connection, 2));
        Assert.True(play.Time.GetUtcNow() - asked >= TimeSpan.FromSeconds(2));
        Opened(connection);

        var opened = play.Time.GetUtcNow();

        Assert.IsType<ExchangeTransferFromInventoryRequest>(await TestWalks.SentAsync(play, connection, 3));
        Assert.True(play.Time.GetUtcNow() - opened >= TimeSpan.FromSeconds(2));
        Assert.False(banking.IsCompleted);
    }

    [Fact]
    public async Task ThePlayerClosingTheBankInterruptsBankingAndPausesTheBot()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var banking = bot.BankAsync(_ => true, 0, TestContext.Current.CancellationToken);

        await TestWalks.SentAsync(play, connection, 1);
        Asked(connection, TestPlay.ChestReplyId);
        await TestWalks.SentAsync(play, connection, 2);
        Opened(connection);
        await TestWalks.SentAsync(play, connection, 3);

        connection.Client(new DialogLeaveRequest());

        var interrupted = await Assert.ThrowsAsync<BotInterruptedException>(() => banking);

        Assert.Equal(Interruption.Player, interrupted.Reason);
        Assert.True(bot.IsPaused);
    }

    [Fact]
    public async Task AnActionTheServerRefusesRefusesTheOpening()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var opening = bot.RunAsync((context, token) => context.OpenBankAsync(token), TestContext.Current.CancellationToken);

        await TestWalks.SentAsync(play, connection, 1);
        connection.Server(new NpcActionErrorEvent());

        Assert.Equal(BankOutcome.Refused, await opening);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task AReplyTheBankerAsksAgainForIsRefusedAndTheDialogLeft()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var opening = bot.RunAsync((context, token) => context.OpenBankAsync(token), TestContext.Current.CancellationToken);

        await TestWalks.SentAsync(play, connection, 1);
        Asked(connection, TestPlay.ChestReplyId, TestPlay.OtherReplyId);
        await TestWalks.SentAsync(play, connection, 2);
        Question(connection, TestPlay.ChestReplyId, TestPlay.OtherReplyId);

        Assert.Equal(new DialogLeaveRequest(), await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });

        Assert.Equal(BankOutcome.Refused, await opening);
        await bot.ProcessedAsync();
        Assert.False(bot.State.IsInDialog);
    }

    [Fact]
    public async Task AQuestionOfferingNoneOfTheBankersRepliesIsLeft()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);
        var opening = bot.RunAsync((context, token) => context.OpenBankAsync(token), TestContext.Current.CancellationToken);

        await TestWalks.SentAsync(play, connection, 1);
        Asked(connection, TestPlay.OtherReplyId);

        Assert.Equal(new DialogLeaveRequest(), await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new DialogLeaveEvent { Type = DialogType.Npc });

        Assert.Equal(BankOutcome.Refused, await opening);

        // The banker's NPC stands there for another part, as in Sarakech's port (D251): the bank is sought elsewhere.
        Assert.DoesNotContain(TestPlay.InsideMapId, bot.Banks.Sought());
        Assert.False(bot.Banks.TryFindBanker(bot.State, out _, out _));
    }

    [Fact]
    public async Task AMapWithoutABankerHasNoBankToOpen()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        Assert.Equal(BankOutcome.NoBank, await bot.RunAsync((context, token) => context.OpenBankAsync(token), TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ADepositPutsInAtMostTheKamasCarried()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);

        Opened(connection);
        await bot.ProcessedAsync();

        var deposit = bot.RunAsync((context, token) => context.DepositAsync([], 1_000_000, token), TestContext.Current.CancellationToken);

        Assert.Equal(new ExchangeKamasRequest { Quantity = Carried - Fee }, await TestWalks.SentAsync(play, connection, 1));
        connection.Server(new StorageKamasUpdateEvent { Kamas = 5 + Carried - Fee });
        connection.Server(new KamasUpdateEvent());

        Assert.Equal(BankOutcome.Done, await deposit);
        Assert.Single(connection.Sent);
    }

    [Fact]
    public async Task StacksTheServerTakesNoneOfAreRefusedAfterItsDelay()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);

        Opened(connection);
        await bot.ProcessedAsync();

        var deposit = bot.RunAsync((context, token) => context.DepositAsync([14, 30, 99], 0, token), TestContext.Current.CancellationToken);

        // The worn stack and one the inventory lacks are left out.
        Assert.Equal(new ExchangeTransferFromInventoryRequest { Uids = { 14 } }, await TestWalks.SentAsync(play, connection, 1));

        Assert.Equal(BankOutcome.Refused, await TestWalks.AdvanceUntilDoneAsync(play.Time, deposit));
        Assert.Contains(14, bot.State.Inventory.Keys);
    }

    [Fact]
    public async Task ADepositOutsideTheBankIsBusy()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);

        Assert.Equal(BankOutcome.Busy, await bot.RunAsync((context, token) => context.DepositAsync([12], 10, token), TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACharacterInADialogIsBusy()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);

        Asked(connection, TestPlay.ChestReplyId);
        await bot.ProcessedAsync();

        Assert.Equal(BankOutcome.Busy, await bot.RunAsync((context, token) => context.OpenBankAsync(token), TestContext.Current.CancellationToken));
        Assert.Equal(BankOutcome.Busy, await bot.BankAsync(_ => true, 0, TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task ACharacterWhereABankerStandsGoesNowhere()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await InTheBankAsync(play);

        Assert.Equal(BankOutcome.Done, await bot.RunAsync((context, token) => context.GoToBankerAsync(token), TestContext.Current.CancellationToken));
        Assert.Empty(connection.Sent);
    }

    [Fact]
    public async Task TheJourneyToABankerLeavesOutAnInsideWhereNoneStands()
    {
        // From the inside holding no banker: out by its map action, then through the door into the bank's inside.
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300, TestPlay.OtherInsideMapId);
        var journey = bot.RunAsync((context, token) => context.GoToBankerAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal(TestPlay.OtherActionCell, await TestWalks.WalkedAsync(play, connection, 1, new MapCurrentEvent { MapId = TestPlay.MapId }));
        connection.Server(TestPlay.Information(287, TestPlay.MapId));

        Assert.IsType<InteractiveUseRequest>(await TestWalks.SentAsync(play, connection, 2));
        TestWalks.Arrive(connection, TestPlay.InsideMapId, 396);

        Assert.Equal(BankOutcome.Done, await TestWalks.AdvanceUntilDoneAsync(play.Time, journey));
        Assert.Equal(TestPlay.InsideMapId, bot.State.MapId);
        Assert.Equal([TestPlay.InsideMapId], bot.Banks.Sought());
    }
}
