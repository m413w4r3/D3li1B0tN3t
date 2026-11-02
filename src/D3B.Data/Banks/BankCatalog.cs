using MemoryPack;

namespace D3B.Data.Banks;

/// <summary>Where the banks are, and who opens a character's bank there.</summary>
/// <remarks>Each table is sorted by identifier and found by binary search. Loaded once and shared: nothing in it changes.</remarks>
[MemoryPackable]
public sealed partial class BankCatalog
{
    /// <summary>The name of the pack entry holding the catalog.</summary>
    public const string EntryName = "banks/catalog";

    /// <summary>Gets the banks, in ascending order of identifier.</summary>
    public ReadOnlyMemory<Bank> Banks { get; }

    /// <summary>Gets the bankers, in ascending order of non-player character.</summary>
    public ReadOnlyMemory<Banker> Bankers { get; }

    /// <summary>Gets the dialog replies that open the bank: each banker's together, ascending, in the order of the bankers.</summary>
    public ReadOnlyMemory<int> Replies { get; }

    /// <summary>Initializes a new instance of the <see cref="BankCatalog"/> class.</summary>
    /// <param name="banks">The banks, in strictly ascending order of identifier.</param>
    /// <param name="bankers">The bankers, in strictly ascending order of non-player character, each naming his own replies.</param>
    /// <param name="replies">The chest replies, each banker's together and in the order of the bankers, with nothing between.</param>
    /// <exception cref="ArgumentException">A table is not in strictly ascending order, or the bankers' replies do not follow one another.</exception>
    [MemoryPackConstructor]
    public BankCatalog(ReadOnlyMemory<Bank> banks, ReadOnlyMemory<Banker> bankers, ReadOnlyMemory<int> replies)
    {
        Tables.CheckAscending(banks.Span, static bank => bank.Id, nameof(banks));
        Tables.CheckAscending(bankers.Span, static banker => banker.NpcId, nameof(bankers));
        Tables.CheckSlices(bankers.Span, static banker => (banker.FirstReply, banker.ReplyCount), replies.Length, nameof(replies));

        Banks = banks;
        Bankers = bankers;
        Replies = replies;
    }

    /// <summary>Finds a banker.</summary>
    /// <param name="npcId">A non-player character, such as one the server says stands on the map.</param>
    /// <param name="banker">The banker, when found.</param>
    /// <returns>Whether the character opens a character's bank.</returns>
    public bool TryGetBanker(int npcId, out Banker banker)
    {
        return Tables.TryFind(Bankers.Span, npcId, static banker => banker.NpcId, out banker);
    }

    /// <summary>Gets the dialog replies with which a banker opens a character's bank.</summary>
    /// <param name="banker">A banker of the catalog.</param>
    /// <returns>His replies, in ascending order.</returns>
    public ReadOnlySpan<int> RepliesOf(Banker banker)
    {
        return Replies.Span.Slice(banker.FirstReply, banker.ReplyCount);
    }
}
