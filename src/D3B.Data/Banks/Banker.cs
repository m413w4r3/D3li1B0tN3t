using System.Runtime.InteropServices;

namespace D3B.Data.Banks;

/// <summary>A non-player character who opens a character's bank: one talks to him, then picks one of his chest replies.</summary>
/// <remarks>
/// Every banker of client 3.7.12.12 declares the chest reply more than once, and the server offers one of them: a bot
/// picks whichever of <see cref="BankCatalog.RepliesOf"/> the dialog shows. Unmanaged and packed: MemoryPack copies an
/// array of it as raw bytes, so its layout is part of the pack format.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Banker
{
    /// <summary>The client's action to talk to a non-player character, which every banker offers.</summary>
    public const int TalkAction = 3;

    /// <summary>Gets the identifier of the non-player character, as the server names those standing on a map.</summary>
    public int NpcId { get; }

    /// <summary>Gets the text naming him, by identifier.</summary>
    public int NameId { get; }

    /// <summary>Gets the index of his first chest reply in <see cref="BankCatalog.Replies"/>.</summary>
    public int FirstReply { get; }

    /// <summary>Gets how many chest replies he has, at least one.</summary>
    public int ReplyCount { get; }

    /// <summary>Initializes a new instance of the <see cref="Banker"/> struct.</summary>
    /// <param name="npcId">The non-player character.</param>
    /// <param name="nameId">The text naming him.</param>
    /// <param name="firstReply">The index of his first chest reply.</param>
    /// <param name="replyCount">How many chest replies he has.</param>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative, or the banker has no reply.</exception>
    public Banker(int npcId, int nameId, int firstReply, int replyCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstReply);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(replyCount);

        NpcId = npcId;
        NameId = nameId;
        FirstReply = firstReply;
        ReplyCount = replyCount;
    }
}
