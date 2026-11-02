using System.CommandLine;
using AssetsTools.NET;

namespace D3B.Scripts.Inspect;

/// <summary><c>types</c>: which types the serialized files of each area hold, and how much of each.</summary>
internal static class TypesCommand
{
    public static Command Create(Option<DirectoryInfo?> client)
    {
        var pattern = CommonOptions.Pattern(false);
        var sample = CommonOptions.Sample();
        var references = new Option<bool>("--references") { Description = "Lists the managed reference types too ([SerializeReference])." };
        var command = new Command("types", "Counts objects per type and area, with the serialized files' format, Unity version and platform.")
        {
            pattern,
            sample,
            references,
        };

        command.SetAction(result => CommonOptions.Run(
            result,
            client,
            located => Run(located, CommonOptions.Select(located, result.GetValue(pattern), result.GetValue(sample)), result.GetValue(references))));

        return command;
    }

    private static int Run(Client client, IReadOnlyList<BundlePath> bundles, bool listReferences)
    {
        var scripts = new ScriptResolver(client);
        var scanned = BundleScan.Run(bundles, bundle => Read(bundle, scripts), out var failures);

        foreach (var area in scanned.GroupBy(item => item.Bundle.Area))
        {
            var files = area.SelectMany(item => item.Result).ToList();

            Console.WriteLine($"== {area.Key}: {area.Count():N0} bundle(s), {files.Count:N0} serialized file(s)");

            foreach (var format in files.GroupBy(file => file.Format).OrderByDescending(group => group.Count()))
                Console.WriteLine($"   {format.Count():N0} file(s): {format.Key}");

            var table = new TextTable("Type", "#Objects", "#Size", "#Bundles");
            var types = area.SelectMany(item => item.Result.SelectMany(file => file.Types.Select(type => (item.Bundle, Type: type.Key, type.Value.Count, type.Value.Bytes))))
                .GroupBy(entry => entry.Type)
                .Select(group => (Type: group.Key, Count: group.Sum(entry => entry.Count), Bytes: group.Sum(entry => entry.Bytes), Bundles: group.Select(entry => entry.Bundle).Distinct().Count()))
                .OrderByDescending(type => type.Bytes);

            foreach (var type in types)
                table.Add(type.Type, type.Count, Sizes.Format(type.Bytes), type.Bundles);

            table.Write(Console.Out);

            var referenceTypes = files.SelectMany(file => file.References).Distinct().Order(StringComparer.Ordinal).ToList();

            Console.WriteLine($"{referenceTypes.Count:N0} managed reference type(s)");

            if (listReferences)
            {
                foreach (var reference in referenceTypes)
                    Console.WriteLine($"   {reference}");
            }

            Console.WriteLine();
        }

        return failures is 0 ? 0 : 1;
    }

    private static List<FileTypes> Read(BundlePath bundle, ScriptResolver scripts)
    {
        using var opened = new OpenedBundle(bundle);
        var files = new List<FileTypes>();

        foreach (var file in opened.Files)
        {
            var metadata = file.file.Metadata;
            var types = new Dictionary<string, (int Count, long Bytes)>(StringComparer.Ordinal);

            foreach (var info in file.file.AssetInfos)
            {
                var name = TypeNames.Of(opened, file, info, scripts);
                var (count, bytes) = types.GetValueOrDefault(name);

                types[name] = (count + 1, bytes + info.ByteSize);
            }

            var references = (metadata.RefTypes ?? []).Select(NameOf).ToList();
            var format = $"format {file.file.Header.Version}, Unity {metadata.UnityVersion}, platform {Platforms.Name(metadata.TargetPlatform)}, "
                + (metadata.TypeTreeEnabled ? "type trees" : "no type trees");

            files.Add(new FileTypes(format, types, references));
        }

        return files;
    }

    private static string NameOf(TypeTreeType type)
    {
        var reference = type.TypeReference;

        if (reference is null)
            return "unnamed";

        var name = reference.Namespace.Length is 0 ? reference.ClassName : $"{reference.Namespace}.{reference.ClassName}";

        return $"{name} ({reference.AsmName})";
    }

    /// <param name="Format">The serialized file's format, Unity version, platform and type tree presence.</param>
    /// <param name="Types">The number of objects and their total size per type.</param>
    /// <param name="References">The managed reference types the file declares.</param>
    private sealed record FileTypes(string Format, Dictionary<string, (int Count, long Bytes)> Types, List<string> References);
}
