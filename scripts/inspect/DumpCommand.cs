using System.CommandLine;

namespace D3B.Scripts.Inspect;

/// <summary><c>dump</c>: the fields of one object, as a tree.</summary>
internal static class DumpCommand
{
    public static Command Create(Option<DirectoryInfo?> client)
    {
        var pattern = CommonOptions.Pattern(true);
        var selector = new Argument<string>("object") { Description = "The object: its path id, or its name." };
        var depth = CommonOptions.AtLeast(new Option<int>("--depth") { Description = "How many levels of fields to write.", DefaultValueFactory = _ => 6 }, 0);
        var items = CommonOptions.AtLeast(new Option<int>("--items") { Description = "How many items of each array to write.", DefaultValueFactory = _ => 4 }, 1);
        var command = new Command("dump", "Writes the fields of the first object that matches, in the first matching bundle that holds it.")
        {
            pattern,
            selector,
            depth,
            items,
        };

        command.SetAction(result => CommonOptions.Run(
            result,
            client,
            located => Run(located, result.GetValue(pattern), result.GetRequiredValue(selector), result.GetValue(depth), result.GetValue(items))));

        return command;
    }

    private static int Run(Client client, string? pattern, string selector, int depth, int items)
    {
        var scripts = new ScriptResolver(client);
        var found = ObjectLookup.Find(client.Bundles(pattern), selector, (opened, file, info) =>
        {
            var field = opened.Manager.GetBaseField(file, info);

            Console.WriteLine($"{opened.Bundle.RelativePath} {file.name} path id {info.PathId}: {TypeNames.Of(opened, file, info, scripts)}, {info.ByteSize:N0} bytes");
            new ValueWriter(Console.Out, depth, items).Write(field);
        });

        if (found)
            return 0;

        Console.Error.WriteLine($"No object {selector} in the matching bundles.");

        return 1;
    }
}
