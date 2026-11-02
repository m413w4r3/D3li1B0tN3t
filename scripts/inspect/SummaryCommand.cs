using System.CommandLine;
using System.Text.Json;
using AssetsTools.NET.Extra;

namespace D3B.Scripts.Inspect;

/// <summary><c>summary</c>: the client's versions, then its bundles and other files per area.</summary>
internal static class SummaryCommand
{
    public static Command Create(Option<DirectoryInfo?> client)
    {
        var command = new Command("summary", "Shows the client's versions, then its bundles and other files per area. Reads headers only.");

        command.SetAction(result => CommonOptions.Run(result, client, Run));

        return command;
    }

    private static int Run(Client client)
    {
        Console.WriteLine($"Client: {client.Root}");
        WriteVersions(client);
        Console.WriteLine();

        var headers = BundleScan.Run(client.Bundles(), BundleHeader.Read, out var failures).Select(item => item.Result).ToList();
        var bundles = new TextTable("Area", "#Bundles", "#On disk", "#Decompressed", "Compression");

        foreach (var area in headers.GroupBy(header => header.Bundle.Area))
        {
            var compressions = area.GroupBy(header => header.Compression)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Key} {group.Count():N0}");

            bundles.Add(
                area.Key,
                area.Count(),
                Sizes.Format(area.Sum(header => header.DiskSize)),
                Sizes.Format(area.Sum(header => header.DataSize)),
                string.Join(", ", compressions));
        }

        bundles.Add("Total", headers.Count, Sizes.Format(headers.Sum(header => header.DiskSize)), Sizes.Format(headers.Sum(header => header.DataSize)), null);
        bundles.Write(Console.Out);
        Console.WriteLine();

        var others = new TextTable("Area", "Extension", "#Files", "#Size");
        var files = Directory.EnumerateFiles(client.StreamingAssets, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Relative: client.RelativePath(path), Size: new FileInfo(path).Length))
            .GroupBy(file => (Area: Client.AreaOf(file.Relative), Extension: Path.GetExtension(file.Relative)))
            .OrderBy(group => group.Key.Area, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Extension, StringComparer.Ordinal);

        foreach (var group in files)
            others.Add(group.Key.Area, group.Key.Extension, group.Count(), Sizes.Format(group.Sum(file => file.Size)));

        others.Write(Console.Out);

        return failures is 0 ? 0 : 1;
    }

    private static void WriteVersions(Client client)
    {
        var version = Path.Combine(client.StreamingAssets, "version");

        if (File.Exists(version))
        {
            var fields = File.ReadAllLines(version)
                .Select(line => line.Split('=', 2))
                .Where(parts => parts.Length is 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

            Console.WriteLine($"Version: {fields.GetValueOrDefault("Version")}, built {fields.GetValueOrDefault("BuildDate")}");
        }

        var manager = new AssetsManager();

        try
        {
            var managers = manager.LoadAssetsFile(Path.Combine(client.DataDirectory, "globalgamemanagers"), false);

            Console.WriteLine($"Unity: {managers.file.Metadata.UnityVersion}, serialized format {managers.file.Header.Version}");
        }
        finally
        {
            manager.UnloadAll(true);
        }

        var settings = Path.Combine(client.StreamingAssets, "aa", "settings.json");

        if (File.Exists(settings))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settings));

            if (document.RootElement.TryGetProperty("m_AddressablesVersion", out var addressables))
                Console.WriteLine($"Addressables: {addressables.GetString()}");
        }
    }
}
