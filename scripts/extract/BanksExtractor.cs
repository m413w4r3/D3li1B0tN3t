using D3B.Data.Banks;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads where the banks are, from the world map's hints, and the bankers, from the non-player characters' replies.</summary>
/// <remarks>
/// The client's data marks neither: a bank is a hint drawn with <see cref="BankHintGfx"/>, and a banker a character
/// whose reply reads <see cref="ChestReply"/> in French, as measured on client 3.7.12.12 (client.md). Where a banker
/// stands, the server tells. The extraction fails when it finds no bank or no banker, so that a client changing either
/// mark is noticed.
/// </remarks>
internal static class BanksExtractor
{
    /// <summary>The graphic of the world map's bank hints: the 10 hints of client 3.7.12.12 drawn with it are all named Banque.</summary>
    private const int BankHintGfx = 401;

    /// <summary>What a chest reply reads in French, with or without a final full stop: 29 replies of 8 characters in client 3.7.12.12.</summary>
    private const string ChestReply = "Consulter son coffre personnel";

    private static readonly string[] _tables = ["hints", "npcs", "npcactions"];

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [.. _tables.Select(table => UnityDataTable.PathOf(client, table))];
    }

    public static BankCatalog Read(ClientInstall client, WorldAtlas atlas, WorldGraph graph)
    {
        try
        {
            var banks = ReadBanks(client, atlas, graph);

            CheckTalkAction(client);

            var (bankers, replies) = ReadBankers(client);
            var catalog = new BankCatalog(banks, bankers, replies);

            Console.WriteLine($"Banks: {banks.Length:N0}, {bankers.Length:N0} bankers with {replies.Length:N0} chest replies");

            return catalog;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"The banks do not fit what the extractor measured: {exception.Message}", exception);
        }
    }

    private static Bank[] ReadBanks(ClientInstall client, WorldAtlas atlas, WorldGraph graph)
    {
        var banks = UnityDataTable.ReadById(client, "hints")
            .Where(record => record.Int32("gfx") is BankHintGfx)
            .Select(record => new Bank(record.Int32("id"), record.Int32("mapId")))
            .OrderBy(bank => bank.Id)
            .ToArray();

        if (banks.Length is 0)
            throw new InvalidDataException($"No hint of the world map is drawn with graphic {BankHintGfx}: the client marks its banks otherwise.");

        foreach (var bank in banks)
        {
            if (!atlas.TryGetPosition(bank.MapId, out _) || !graph.Contains(bank.MapId))
                throw new InvalidDataException($"Bank {bank.Id} stands on map {bank.MapId}, which has no position or no vertex in the world graph.");
        }

        return banks;
    }

    private static void CheckTalkAction(ClientInstall client)
    {
        var talk = UnityDataTable.ReadById(client, "npcactions").FirstOrDefault(record => record.Int32("id") is Banker.TalkAction);

        if (talk is null || talk.Int32("realId") is not Banker.TalkAction)
            throw new InvalidDataException($"The client's non-player character action {Banker.TalkAction} is not the one to talk.");
    }

    private static (Banker[] Bankers, int[] Replies) ReadBankers(ClientInstall client)
    {
        var french = TextsExtractor.Open(client, TextLanguages.French);
        var offsets = new Dictionary<int, int>(french.Texts.Count);

        foreach (var (id, offset) in french.Texts)
            offsets.TryAdd(id, offset);

        var bankers = new List<Banker>();
        var replies = new List<int>();

        foreach (var npc in UnityDataTable.ReadById(client, "npcs").OrderBy(record => record.Int32("id")))
        {
            var id = npc.Int32("id");
            var chest = new SortedSet<int>();

            foreach (var reply in npc.Items("dialogReplies"))
            {
                var values = reply.Int32Array("values");

                if (values.Length is not 2)
                    throw new InvalidDataException($"Non-player character {id} lists a reply as {values.Length} numbers, where the extractor expects a reply and its text.");

                if (offsets.TryGetValue(values[1], out var offset) && french.TextAt(offset).StartsWith(ChestReply, StringComparison.Ordinal))
                    chest.Add(values[0]);
            }

            if (chest.Count is 0)
                continue;

            if (!npc.Int32Array("actions").Contains(Banker.TalkAction))
                throw new InvalidDataException($"Banker {id} cannot be talked to: his actions lack {Banker.TalkAction}.");

            bankers.Add(new Banker(id, npc.Int32("nameId"), replies.Count, chest.Count));
            replies.AddRange(chest);
        }

        if (bankers.Count is 0)
            throw new InvalidDataException($"No non-player character has a reply reading '{ChestReply}': the client opens banks otherwise.");

        return ([.. bankers], [.. replies]);
    }
}
