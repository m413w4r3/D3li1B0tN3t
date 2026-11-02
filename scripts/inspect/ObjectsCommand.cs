using System.CommandLine;

namespace D3B.Scripts.Inspect;

/// <summary><c>objects</c>: the objects of some bundles, with their type, name and size.</summary>
internal static class ObjectsCommand
{
    public static Command Create(Option<DirectoryInfo?> client)
    {
        var pattern = CommonOptions.Pattern(true);
        var type = new Option<string?>("--type") { Description = "Keeps the objects whose type, or script, contains this text." };
        var name = new Option<string?>("--name") { Description = "Keeps the objects whose name contains this text." };
        var limit = CommonOptions.AtLeast(new Option<int>("--limit") { Description = "Writes at most this many objects.", DefaultValueFactory = _ => 100 }, 0);
        var command = new Command("objects", "Lists the objects of the matching bundles: path id, type, name and size.")
        {
            pattern,
            type,
            name,
            limit,
        };

        command.SetAction(result => CommonOptions.Run(
            result,
            client,
            located => Run(located, result.GetValue(pattern), result.GetValue(type), result.GetValue(name), result.GetValue(limit))));

        return command;
    }

    private static int Run(Client client, string? pattern, string? type, string? name, int limit)
    {
        var scripts = new ScriptResolver(client);
        var scanned = BundleScan.Run(client.Bundles(pattern), bundle => Read(bundle, scripts, type, name), out var failures);
        var objects = scanned.SelectMany(item => item.Result.Select(entry => (item.Bundle, Entry: entry))).ToList();
        var table = new TextTable("Bundle", "#Path id", "Type", "Name", "#Size");

        // A path id is written as is, without separators, so that it can be passed to dump and layout.
        foreach (var (bundle, entry) in objects.Take(limit))
            table.Add(bundle.FileName, $"{entry.PathId}", entry.Type, entry.Name, entry.Size);

        table.Write(Console.Out);
        Console.WriteLine();
        Console.WriteLine($"{objects.Count:N0} object(s) in {scanned.Count:N0} bundle(s){(objects.Count > limit ? $", {limit:N0} written" : string.Empty)}");

        return failures is 0 ? 0 : 1;
    }

    private static List<ObjectEntry> Read(BundlePath bundle, ScriptResolver scripts, string? type, string? name)
    {
        using var opened = new OpenedBundle(bundle);
        var entries = new List<ObjectEntry>();

        foreach (var file in opened.Files)
        {
            foreach (var info in file.file.AssetInfos)
            {
                var typeName = TypeNames.Of(opened, file, info, scripts);

                if (type is not null && !typeName.Contains(type, StringComparison.OrdinalIgnoreCase))
                    continue;

                var objectName = ObjectNames.Read(opened.Manager, file, info);

                if (name is not null && (objectName is null || !objectName.Contains(name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                entries.Add(new ObjectEntry(info.PathId, typeName, objectName, info.ByteSize));
            }
        }

        return entries;
    }

    private sealed record ObjectEntry(long PathId, string Type, string? Name, long Size);
}
