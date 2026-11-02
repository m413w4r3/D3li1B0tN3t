using System.Collections.Immutable;
using D3B.Bot.Banks;
using D3B.Bot.Characters;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Banks;

public sealed class BankDirectoryTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    [Fact]
    public void TheInsidesAreThoseOfEveryBank()
    {
        Assert.Equal([TestPlay.InsideMapId, TestPlay.OtherInsideMapId], Directory().Insides.Order());
    }

    [Fact]
    public void AnInsideSeenWithoutABankerIsSoughtAgainOnceSeenWithOne()
    {
        var banks = Directory();

        banks.Seen(TestPlay.OtherInsideMapId, banker: false);
        Assert.Equal([TestPlay.InsideMapId], banks.Sought());

        banks.Seen(TestPlay.OtherInsideMapId, banker: true);
        Assert.Equal(2, banks.Sought().Count);

        // A map that is no bank's inside is not remembered.
        banks.Seen(TestPlay.MapId, banker: false);
        Assert.Equal(2, banks.Sought().Count);
    }

    [Fact]
    public void AMapRefusedHoldsNoBankerHoweverItsNpcsAreSeen()
    {
        var banks = Directory();

        banks.Refuse(TestPlay.OtherInsideMapId);
        banks.Seen(TestPlay.OtherInsideMapId, banker: true);

        Assert.Equal([TestPlay.InsideMapId], banks.Sought());
    }

    [Fact]
    public void TheBankerIsTheNpcThePackNamesSoAndHisReplyTheOneTheQuestionOffers()
    {
        var banks = Directory();
        var state = CharacterState.Chosen(TestPlay.Key, TestPlay.Chosen().Success.Character) with
        {
            Npcs = ImmutableSortedDictionary.CreateRange(new Dictionary<int, MapNpc>
            {
                [-20_000] = new(-20_000, 1_088, 302),
                [TestPlay.BankerActorId] = new(TestPlay.BankerActorId, TestPlay.BankerNpcId, 259),
            }),
        };

        Assert.True(banks.TryFindBanker(state, out var npc, out var banker));
        Assert.Equal(TestPlay.BankerActorId, npc.ActorId);
        Assert.True(banks.TryFindReply(banker, new NpcDialog(npc.ActorId) { Replies = [TestPlay.OtherReplyId, TestPlay.ChestReplyId] }, out var reply));
        Assert.Equal(TestPlay.ChestReplyId, reply);
        Assert.False(banks.TryFindReply(banker, new NpcDialog(npc.ActorId) { Replies = [TestPlay.OtherReplyId] }, out _));
        Assert.False(banks.TryFindBanker(state with { Npcs = state.Npcs.Remove(TestPlay.BankerActorId) }, out _, out _));
    }

    private BankDirectory Directory()
    {
        return new BankDirectory(_play.Tables.Banks, _play.World.Graph, _play.World.Atlas);
    }
}
