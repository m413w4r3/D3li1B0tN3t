using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Journal;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Banks;

public sealed class GuildChestTests
{
    private const int ChestInstanceId = 33_001;

    private static MapInformationEvent WithChest(int cell)
    {
        var information = TestPlay.Information(cell, TestPlay.InsideMapId);

        information.InteractiveElements.Add(new InteractiveElement
        {
            ElementId = TestPlay.ChestElementId,
            InteractiveId = GuildChest.InteractiveId,
            OnCurrentMap = true,
            EnabledSkills = { new InteractiveSkill { SkillId = GuildChest.SkillId, InstanceId = ChestInstanceId } },
        });

        return information;
    }

    private static InventoryObject Stack(int uid, int itemId, int quantity)
    {
        return new InventoryObject { Item = new ObjectItem { Uid = uid, ItemId = itemId, Quantity = quantity }, Position = InventoryItem.NotWorn };
    }

    [Fact]
    public async Task StacksGoInOneAtATimeWhatTheChestRefusesStayingAndTheChestIsLeft()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(301, TestPlay.InsideMapId);

        connection.Server(WithChest(301));
        connection.Server(new InventoryContentEvent { Objects = { Stack(12, TestPlay.Ash, 40), Stack(13, 9_000, 1) } });
        await bot.ProcessedAsync();

        var deposit = bot.RunAsync((context, token) => context.GuildChestAsync(new HashSet<long> { TestPlay.InsideMapId }, _ => true, token), TestContext.Current.CancellationToken);

        Assert.Equal(new InteractiveUseRequest { ElementId = TestPlay.ChestElementId, SkillInstanceId = ChestInstanceId }, await TestWalks.SentAsync(play, connection, 1));

        // Measured: the use, then the exchange of the guild's chest and what it holds, in one millisecond.
        connection.Server(new InteractiveUsedEvent { ActorId = TestPlay.CharacterId, ElementId = TestPlay.ChestElementId, SkillId = GuildChest.SkillId });
        connection.Server(new ExchangeStartedWithTabbedStorageEvent { Type = ExchangeType.GuildChest });
        connection.Server(new StorageContentEvent());
        await bot.ProcessedAsync();

        Assert.True(bot.State.Storage?.IsGuildChest);
        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 12, Quantity = 40 }, await TestWalks.SentAsync(play, connection, 2));
        connection.Server(new ObjectDeletedEvent { Uid = 12 });

        Assert.Equal(new ExchangeObjectMoveRequest { Uid = 13, Quantity = 1 }, await TestWalks.SentAsync(play, connection, 3));
        connection.Server(new TextInformationEvent { TextId = GuildChest.RefusedTextId, Type = TextInformationType.Error });
        Assert.Equal(new DialogLeaveRequest(), await TestWalks.SentAsync(play, connection, 4));
        connection.Server(new ExchangeLeaveEvent());

        Assert.Equal(BankOutcome.Refused, await deposit);
        Assert.Equal([13], bot.State.Inventory.Keys);
        Assert.Contains(bot.Journal.All(), entry => entry is BankEntry { GuildChest: true, Stacks: 2, Outcome: BankOutcome.Refused });
    }

    [Fact]
    public async Task NoChestOnTheMapsGivenIsNoBank()
    {
        await using var play = new TestPlay();
        var (_, bot) = await play.EnterAsync(300);

        Assert.Equal(BankOutcome.NoBank, await bot.RunAsync((context, token) => context.GuildChestAsync(new HashSet<long>(), _ => true, token), TestContext.Current.CancellationToken));
    }
}
