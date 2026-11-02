using System.CommandLine;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace D3B.Scripts.Inspect;

/// <summary><c>layout</c>: the type tree of one type, the layout its objects are serialized with.</summary>
internal static class LayoutCommand
{
    public static Command Create(Option<DirectoryInfo?> client)
    {
        var pattern = CommonOptions.Pattern(true);
        var selector = new Argument<string>("type")
        {
            Description = "A class such as Texture2D, a script as Namespace.Class or Class, or an object's path id or name.",
        };

        var depth = CommonOptions.AtLeast(
            new Option<int>("--depth") { Description = "How many levels of fields to write.", DefaultValueFactory = _ => int.MaxValue },
            0);
        var references = new Option<bool>("--references") { Description = "Writes the layout of the file's managed reference types too." };
        var command = new Command("layout", "Writes the type tree of a type from the first matching bundle that holds it.")
        {
            pattern,
            selector,
            depth,
            references,
        };

        command.SetAction(result => CommonOptions.Run(
            result,
            client,
            located => Run(located, result.GetValue(pattern), result.GetRequiredValue(selector), result.GetValue(depth), result.GetValue(references))));

        return command;
    }

    private static int Run(Client client, string? pattern, string selector, int depth, bool references)
    {
        var scripts = new ScriptResolver(client);
        var bundles = client.Bundles(pattern);

        foreach (var bundle in bundles)
        {
            using var opened = new OpenedBundle(bundle);

            foreach (var file in opened.Files)
            {
                var type = file.file.Metadata.TypeTreeTypes.FirstOrDefault(candidate => Matches(NameOf(opened, file, candidate, scripts), selector));

                if (type is null)
                    continue;

                Write(opened, file, type, scripts, depth, references);

                return 0;
            }
        }

        // Not a type name: an object's path id or name, whose type is wanted.
        var found = ObjectLookup.Find(bundles, selector, (opened, file, info) =>
        {
            var metadata = file.file.Metadata;
            var type = metadata.TypeTreeTypes[info.TypeIdOrIndex];

            Write(opened, file, type, scripts, depth, references);
        });

        if (found)
            return 0;

        Console.Error.WriteLine($"No type or object {selector} in the matching bundles.");

        return 1;
    }

    private static bool Matches(string name, string selector)
    {
        if (string.Equals(name, selector, StringComparison.OrdinalIgnoreCase))
            return true;

        // A script also matches by its class name alone.
        return name.EndsWith($".{selector}", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith($" {selector}", StringComparison.OrdinalIgnoreCase);
    }

    private static string NameOf(OpenedBundle opened, AssetsFileInstance file, TypeTreeType type, ScriptResolver scripts)
    {
        return type.TypeId == (int)AssetClassID.MonoBehaviour
            ? $"MonoBehaviour {scripts.Resolve(opened, file, type.ScriptTypeIndex)}"
            : TypeNames.OfClass(type.TypeId);
    }

    private static void Write(OpenedBundle opened, AssetsFileInstance file, TypeTreeType type, ScriptResolver scripts, int depth, bool references)
    {
        Console.WriteLine($"{opened.Bundle.RelativePath} {file.name}: {NameOf(opened, file, type, scripts)}, {type.Nodes.Count:N0} nodes, type hash {type.TypeHash}");
        LayoutWriter.Write(Console.Out, type, depth);

        if (!references)
            return;

        foreach (var reference in file.file.Metadata.RefTypes ?? [])
        {
            var name = reference.TypeReference switch
            {
                null => "unnamed",
                { Namespace.Length: 0 } named => $"{named.ClassName} ({named.AsmName})",
                var named => $"{named.Namespace}.{named.ClassName} ({named.AsmName})",
            };

            Console.WriteLine();
            Console.WriteLine($"Reference type {name}, {reference.Nodes.Count:N0} nodes");
            LayoutWriter.Write(Console.Out, reference, depth);
        }
    }
}
