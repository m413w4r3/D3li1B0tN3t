using D3B.Bot.Settings;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;
using D3B.Data;
using D3B.Data.Banks;
using D3B.Data.Interactives;
using D3B.Data.Jobs;
using D3B.Data.Monsters;
using D3B.Data.Packs;
using D3B.Data.Tests;
using D3B.Data.Texts;

namespace D3B.App.Tests;

/// <summary>The game's values of a small world, named in French and English by a pack's texts, as the app's pickers read them.</summary>
/// <remarks>
/// Its tables name what the map of <see cref="TestPlay"/> holds, as the map page names it: the tree and the door its
/// server describes, the tree's skill, which gathers, the door's, the banker, and a monster its fights meet.
/// </remarks>
internal sealed class TestGameData : IDisposable
{
    /// <summary>A monster the pack names.</summary>
    public const int Gobball = 3_271;

    private static readonly (int Id, string French, string English)[] _names =
    [
        (101, "Amakna", "Amakna"),
        (102, "Astrub", "Astrub"),
        (110, "Forêt d'Amakna", "Amakna Forest"),
        (111, "Collines d'Amakna", "Amakna Hills"),
        (120, "Cité d'Astrub", "Astrub City"),
        (202, "Bûcheron", "Lumberjack"),
        (224, "Mineur", "Miner"),
        (403, "Frêne", "Ash"),
        (404, "Châtaignier", "Chestnut"),
        (412, "Fer", "Iron"),
        (501, "Frêne", "Ash tree"),
        (502, "Porte", "Door"),
        (503, "Couper", "Cut"),
        (504, "Entrer", "Enter"),
        (505, "Banquier", "Banker"),
        (506, "Bouftou", "Gobball"),
    ];

    private readonly TemporaryDirectory _directory = new();
    private readonly GamePack _pack;

    public GameReferences References { get; } = TestReferences.Game();

    public GameTexts Texts { get; }

    public GameTables Tables { get; }

    public TestGameData()
    {
        var path = _directory.Combine("texts.pack");
        var info = new PackInfo { ClientVersion = TestInstall.Version, Fingerprint = "0123456789abcdef", ExtractorVersion = "0.0.0-test" };

        using (var stream = File.Create(path))
        using (var writer = new PackWriter(stream, info, 3))
        {
            writer.Add(TextTable.EntryName(TextLanguages.French), new TextTable(TextLanguages.French, [.. _names.Select(name => name.Id)], [.. _names.Select(name => name.French)]));
            writer.Add(TextTable.EntryName(TextLanguages.English), new TextTable(TextLanguages.English, [.. _names.Select(name => name.Id)], [.. _names.Select(name => name.English)]));
            writer.Add(InteractiveCatalog.EntryName, new InteractiveCatalog(new[] { new Interactive(34, 501), new Interactive(316, 502) }));
            writer.Add(JobCatalog.EntryName, new JobCatalog(
                new[] { new Job(Job.Base, 200, Job.NoIcon), new Job(TestReferences.Lumberjack, 202, 1) },
                new[] { new Skill(TestPlay.TreeSkillId, 503, TestReferences.Lumberjack, 289, 1, 1, false), new Skill(184, 504, Job.Base, Skill.NoItem, 0, 1, false) }));
            writer.Add(BankCatalog.EntryName, new BankCatalog(Array.Empty<Bank>(), new[] { new Banker(TestPlay.BankerNpcId, 505, 0, 1) }, new[] { TestPlay.ChestReplyId }));
            writer.Add(MonsterCatalog.EntryName, new MonsterCatalog(
                new[] { new Monster { Id = Gobball, NameId = 506 } },
                ReadOnlyMemory<MonsterGrade>.Empty,
                ReadOnlyMemory<MonsterSpell>.Empty,
                ReadOnlyMemory<byte>.Empty,
                ReadOnlyMemory<MonsterRatio>.Empty,
                ReadOnlyMemory<MonsterRace>.Empty,
                ReadOnlyMemory<MonsterRace>.Empty));
            writer.Complete();
        }

        _pack = GamePack.Open(path);
        Texts = new GameTexts(_pack);
        Tables = new GameTables(_pack);
    }

    public void Dispose()
    {
        _pack.Dispose();
        _directory.Dispose();
    }
}
