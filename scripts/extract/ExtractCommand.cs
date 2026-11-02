using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using D3B.Data;
using D3B.Data.Banks;
using D3B.Data.Breeds;
using D3B.Data.Characteristics;
using D3B.Data.Effects;
using D3B.Data.Icons;
using D3B.Data.Interactives;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Maps;
using D3B.Data.Monsters;
using D3B.Data.Packs;
using D3B.Data.Spells;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>The command that extracts the installed client's game data into the pack of its build.</summary>
internal static class ExtractCommand
{
    public static RootCommand Create()
    {
        var client = new Option<DirectoryInfo?>("--client")
        {
            Description = @"The client's directory. Defaults to %LOCALAPPDATA%\Ankama\Dofus-dofus3 on Windows.",
        };
        var output = new Option<DirectoryInfo?>("--output")
        {
            Description = @"The directory packs are kept in, one directory per client version. Defaults to %LOCALAPPDATA%\D3B\data, where the host looks first.",
        };
        var force = new Option<bool>("--force")
        {
            Description = "Extracts again even when the pack was made from the same files by the same extractor.",
        };
        var level = new Option<int>("--level")
        {
            Description = "The zstd compression level of the pack's entries.",
            DefaultValueFactory = _ => PackWriter.DefaultCompressionLevel,
        };
        var command = new RootCommand("Extracts the game data of the installed Dofus 3 client into the pack of its build.")
        {
            client,
            output,
            force,
            level,
        };

        command.SetAction(result => Run(
            result.GetValue(client),
            result.GetValue(output)?.FullName ?? PackLocations.DefaultRoot,
            result.GetValue(force),
            result.GetValue(level)));

        return command;
    }

    private static int Run(DirectoryInfo? clientDirectory, string root, bool force, int level)
    {
        ClientInstall client;

        try
        {
            client = ClientInstall.Locate(clientDirectory);
        }
        catch (DirectoryNotFoundException exception)
        {
            Console.Error.WriteLine(exception.Message);

            return 2;
        }

        try
        {
            return Extract(client, root, force, level);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"The extraction failed: {exception.Message}");

            return 1;
        }
    }

    private static int Extract(ClientInstall client, string root, bool force, int level)
    {
        var stopwatch = Stopwatch.StartNew();
        var build = ClientBuild.Read(client.Root);
        var manifest = LauncherManifest.Load(client);
        var inputs = TextsExtractor.Inputs(client)
            .Concat(MapsExtractor.Inputs(client))
            .Concat(WorldExtractor.Inputs(client))
            .Concat(ItemsExtractor.Inputs(client))
            .Concat(JobsExtractor.Inputs(client))
            .Concat(InteractivesExtractor.Inputs(client))
            .Concat(BanksExtractor.Inputs(client))
            .Concat(IconsExtractor.Inputs(client))
            .Concat(EffectsExtractor.Inputs(client))
            .Concat(CharacteristicsExtractor.Inputs(client))
            .Concat(SpellsExtractor.Inputs(client))
            .Concat(StatesExtractor.Inputs(client))
            .Concat(MonstersExtractor.Inputs(client))
            .Concat(BreedsExtractor.Inputs(client))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var fingerprint = InputFingerprint.Compute(client, manifest, inputs);
        var extractor = typeof(GamePack).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var path = PackLocations.PathOf(root, build);

        Console.WriteLine($"Client: {client.Root}, version {build}");

        if (!force && IsUpToDate(path, fingerprint, extractor))
        {
            Console.WriteLine($"Up to date: {path} was made from the same files by the same extractor. --force extracts again.");

            return 0;
        }

        // Everything is read and checked before the pack is opened, so that a client the extractor misreads leaves no file.
        var reading = Stopwatch.StartNew();
        var maps = MapsExtractor.Read(client);
        var graph = WorldExtractor.ReadGraph(client);
        var effects = EffectsExtractor.Read(client);
        var (items, weapons) = ItemsExtractor.Read(client, effects);
        var jobs = JobsExtractor.Read(client, items);
        var characteristics = CharacteristicsExtractor.Read(client, TextsExtractor.Held(client));
        var spells = SpellsExtractor.Read(client, effects);
        var states = StatesExtractor.Read(client, spells);
        var (monsters, monsterSubAreas) = MonstersExtractor.Read(client, spells);
        var breeds = BreedsExtractor.Read(client, spells, characteristics);
        var atlas = WorldExtractor.ReadAtlas(client, items, jobs, monsters, monsterSubAreas);
        var interactives = InteractivesExtractor.Read(client);
        var banks = BanksExtractor.Read(client, atlas, graph);
        var icons = IconsExtractor.Read(client, items, jobs, spells, breeds, monsters, atlas);
        var fights = new FightTables(spells, states, effects, monsters, breeds, characteristics);

        WorldExtractor.Check(graph, atlas, maps);

        var texts = TextsExtractor.Read(client, TextsExtractor.Named(atlas, items, jobs, interactives, banks, fights));

        Console.WriteLine($"Read in {reading.Elapsed.TotalSeconds:F1} s");

        var info = new PackInfo
        {
            ClientVersion = build.Version,
            ClientBuildDate = build.BuildDate,
            Fingerprint = fingerprint,
            ExtractorVersion = extractor,
        };
        var temporary = path + ".tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var writing = Stopwatch.StartNew();

        // Written aside, then moved: the host never sees a pack half written under the name it looks for.
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new PackWriter(stream, info, level))
        {
            foreach (var table in texts)
                writer.Add(TextTable.EntryName(table.Language), table);

            writer.Add(WorldAtlas.EntryName, atlas);
            writer.Add(WorldGraph.EntryName, graph);
            writer.Add(ItemCatalog.EntryName, items);
            writer.Add(WeaponCatalog.EntryName, weapons);
            writer.Add(JobCatalog.EntryName, jobs);
            writer.Add(InteractiveCatalog.EntryName, interactives);
            writer.Add(BankCatalog.EntryName, banks);
            writer.Add(IconCatalog.EntryName, icons);
            writer.Add(SpellCatalog.EntryName, spells);
            writer.Add(SpellStateCatalog.EntryName, states);
            writer.Add(EffectCatalog.EntryName, effects);
            writer.Add(MonsterCatalog.EntryName, monsters);
            writer.Add(BreedCatalog.EntryName, breeds);
            writer.Add(CharacteristicCatalog.EntryName, characteristics);

            foreach (var map in maps)
                writer.Add(MapData.EntryName(map.Id), map);

            writer.Complete();
        }

        Console.WriteLine($"Written in {writing.Elapsed.TotalSeconds:F1} s");

        File.Move(temporary, path, true);

        using var written = File.OpenRead(path);

        Console.WriteLine($"Pack: {path}, {written.Length:N0} bytes, format {GamePack.FormatVersion}, extracted in {stopwatch.Elapsed.TotalSeconds:F1} s");
        Console.WriteLine($"Fingerprint: {fingerprint}");
        Console.WriteLine($"SHA-256: {Convert.ToHexStringLower(SHA256.HashData(written))}");

        return 0;
    }

    private static bool IsUpToDate(string path, string fingerprint, string extractor)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var existing = GamePack.Open(path);

            return existing.Info.Fingerprint == fingerprint && existing.Info.ExtractorVersion == extractor;
        }
        catch (PackFormatException)
        {
            // A pack this extractor cannot read is replaced.
            return false;
        }
    }
}
