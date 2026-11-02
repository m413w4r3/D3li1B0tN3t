using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using D3B.Bot.Characters;
using D3B.Data.Banks;
using D3B.Data.World;
using D3B.Engine.World;

namespace D3B.Bot.Banks;

/// <summary>Where the banks are, who opens them, and which of their insides the server showed holding no banker: one per host, which every bot shares (D79).</summary>
/// <remarks>
/// <para>
/// The pack names each bank by the map in front of it, and the bankers with the replies that open the bank (D29). The
/// insides are the maps its doors lead into at its coordinates (<see cref="WorldInsides"/>): one for 7 of the 10 banks,
/// two for the others, of which only the server says which holds the banker, by the NPCs it places there.
/// </para>
/// <para>
/// A bot that enters an inside tells what it saw (<see cref="Seen"/>): one where no banker stands is no longer sought by any
/// bot of the host, until it is seen holding one. Nothing is kept from one run of the host to the next.
/// </para>
/// <para>
/// A banker's NPC may stand where he opens no bank: Sarakech's bank has three insides, and in its port's the banker's NPC
/// asks a question none of whose replies opens the bank, measured (D251). Such a map is refused for the host's run
/// (<see cref="Refuse"/>): no banker is found there, and its inside is sought no more, however its NPCs are seen.
/// </para>
/// </remarks>
public sealed class BankDirectory
{
    private readonly BankCatalog _catalog;
    private readonly FrozenSet<long> _insides;
    private readonly ConcurrentDictionary<long, bool> _bankers = new();
    private readonly ConcurrentDictionary<long, bool> _refused = new();

    /// <summary>Gets the insides of every bank, whether a banker stands there or not.</summary>
    public IReadOnlySet<long> Insides => _insides;

    /// <summary>Initializes a new instance of the <see cref="BankDirectory"/> class.</summary>
    /// <param name="catalog">The banks and bankers of the installed build's pack.</param>
    /// <param name="graph">The world graph, whose doors lead into the banks.</param>
    /// <param name="atlas">The atlas placing each map.</param>
    public BankDirectory(BankCatalog catalog, WorldGraph graph, WorldAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(atlas);

        var insides = new List<long>();

        foreach (var bank in catalog.Banks.Span)
            insides.AddRange(WorldInsides.Of(graph, atlas, bank.MapId));

        _catalog = catalog;
        _insides = insides.ToFrozenSet();
    }

    /// <summary>Gets the insides a bot may find a banker in: every inside but those the server showed holding none.</summary>
    /// <returns>The insides to seek.</returns>
    public IReadOnlySet<long> Sought()
    {
        return _insides.Where(inside => _bankers.GetValueOrDefault(inside, true)).ToFrozenSet();
    }

    /// <summary>Remembers whether a bank's inside holds a banker, as the server placed its NPCs.</summary>
    /// <param name="mapId">The map the character entered.</param>
    /// <param name="banker">Whether a banker stands there.</param>
    public void Seen(long mapId, bool banker)
    {
        if (_insides.Contains(mapId))
            _bankers[mapId] = banker && !_refused.ContainsKey(mapId);
    }

    /// <summary>Remembers that a banker's question on a map offered none of the replies that open the bank: no banker is found there any more (D251).</summary>
    /// <param name="mapId">The map the banker stands on.</param>
    public void Refuse(long mapId)
    {
        _refused[mapId] = true;
        Seen(mapId, banker: false);
    }

    /// <summary>Finds a banker among the NPCs of the character's map.</summary>
    /// <param name="state">The character's state.</param>
    /// <param name="npc">The banker as the map places him, when one stands there.</param>
    /// <param name="banker">The banker as the pack describes him, with his replies.</param>
    /// <returns>Whether a banker stands on the map.</returns>
    public bool TryFindBanker(CharacterState state, [NotNullWhen(true)] out MapNpc? npc, out Banker banker)
    {
        ArgumentNullException.ThrowIfNull(state);

        var refused = state.MapId is { } mapId && _refused.ContainsKey(mapId);

        foreach (var candidate in refused ? [] : state.Npcs.Values)
        {
            if (_catalog.TryGetBanker(candidate.NpcId, out banker))
            {
                npc = candidate;

                return true;
            }
        }

        npc = null;
        banker = default;

        return false;
    }

    /// <summary>Finds the reply that opens the bank among those a banker's question offers.</summary>
    /// <param name="banker">The banker.</param>
    /// <param name="dialog">The dialog with him, its question sent.</param>
    /// <param name="reply">The reply, when the question offers one of his.</param>
    /// <returns>Whether it does.</returns>
    public bool TryFindReply(Banker banker, NpcDialog dialog, out int reply)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        var replies = _catalog.RepliesOf(banker);

        foreach (var offered in dialog.Replies)
        {
            if (replies.Contains(offered))
            {
                reply = offered;

                return true;
            }
        }

        reply = 0;

        return false;
    }
}
