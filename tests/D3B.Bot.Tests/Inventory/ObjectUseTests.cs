using D3B.Bot.Characters;
using D3B.Bot.Inventory;
using D3B.Bot.Journal;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Inventory;

public sealed class ObjectUseTests
{
    private const int Bread = 468;

    private static InventoryContentEvent Carrying(int quantity)
    {
        return new InventoryContentEvent { Objects = { new InventoryObject { Item = new ObjectItem { Uid = 50, ItemId = Bread, Quantity = quantity }, Position = InventoryItem.NotWorn } } };
    }

    private static CharacterStatsEvent Stats(int most, int vitality, int lacking)
    {
        Characteristic Of(int id, int value)
        {
            return new Characteristic { Id = id, Detailed = new DetailedCharacteristic { Number5 = value } };
        }

        return new CharacterStatsEvent { Stats = new CharacterStats { Characteristics = { Of(0, most), Of(11, vitality), Of(97, lacking) } } };
    }

    [Fact]
    public async Task FoodEatenLessensItsStackAndTheLifeRegeneratesTwoPointsASecond()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        connection.Server(Carrying(3));
        connection.Server(Stats(55, 1_000, -600));
        await bot.ProcessedAsync();

        Assert.Equal((455, 1_055), (bot.State.Life, bot.State.MaxLife));

        var use = bot.RunAsync((context, token) => context.UseObjectAsync(50, token), TestContext.Current.CancellationToken);

        Assert.Equal(new ObjectUseRequest { Uid = 50 }, await connection.SentAsync(1));

        // Measured: the regeneration ends and starts again, the stack lessens, the life healed, then the weight.
        connection.Server(new LifeRegenEndEvent { Life = 455, MaxLife = 1_055 });
        connection.Server(new LifeRegenStartEvent { Interval = 5 });
        connection.Server(new ObjectQuantityEvent { Object = new ObjectQuantity { Uid = 50, Quantity = 2 } });
        connection.Server(Stats(55, 1_000, -500));
        connection.Server(new InventoryWeightEvent { Weight = 100, MaxWeight = 1_000 });

        Assert.Equal(ObjectUseOutcome.Used, await use);
        Assert.Equal(2, bot.State.Inventory[50].Quantity);
        Assert.Equal(555, bot.LifeAt(play.Time.GetUtcNow()));
        Assert.Equal(565, bot.LifeAt(play.Time.GetUtcNow().AddSeconds(5)));
        Assert.Equal(1_055, bot.LifeAt(play.Time.GetUtcNow().AddHours(1)));
        Assert.Contains(bot.Journal.All(), entry => entry is ObjectUseEntry { ItemId: Bread, Outcome: ObjectUseOutcome.Used });
    }

    [Fact]
    public async Task FoodAtFullLifeIsRefusedAndNothingUsed()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        connection.Server(Carrying(1));
        await bot.ProcessedAsync();

        var use = bot.RunAsync((context, token) => context.UseObjectAsync(50, token), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new TextInformationEvent { TextId = ObjectUse.FullLifeTextId, Type = TextInformationType.Message });

        Assert.Equal(ObjectUseOutcome.Refused, await use);
        Assert.Equal(1, bot.State.Inventory[50].Quantity);
        Assert.Equal(ObjectUseOutcome.NotCarried, await bot.RunAsync((context, token) => context.UseObjectAsync(51, token), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheLastObjectOfAStackUsedDeletesIt()
    {
        await using var play = new TestPlay();
        var (connection, bot) = await play.EnterAsync(300);

        connection.Server(Carrying(1));
        await bot.ProcessedAsync();

        var use = bot.RunAsync((context, token) => context.UseObjectAsync(50, token), TestContext.Current.CancellationToken);

        await connection.SentAsync(1);
        connection.Server(new ObjectDeletedEvent { Uid = 50 });
        connection.Server(new InventoryWeightEvent { Weight = 90, MaxWeight = 1_000 });

        Assert.Equal(ObjectUseOutcome.Used, await use);
        Assert.False(bot.State.Inventory.ContainsKey(50));
    }
}
