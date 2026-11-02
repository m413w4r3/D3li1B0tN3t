using System.CommandLine;

namespace D3B.Scripts.Inspect;

/// <summary><c>bundles</c>: how each bundle is stored, from its header.</summary>
internal static class BundlesCommand
{
    public static Command Create(Option<DirectoryInfo?> client)
    {
        var pattern = CommonOptions.Pattern(false);
        var entries = new Option<bool>("--entries") { Description = "Lists the files each bundle stores." };
        var command = new Command("bundles", "Lists bundles with their sizes, compression and blocks. Reads headers only.")
        {
            pattern,
            entries,
        };

        command.SetAction(result => CommonOptions.Run(result, client, located => Run(located, result.GetValue(pattern), result.GetValue(entries))));

        return command;
    }

    private static int Run(Client client, string? pattern, bool entries)
    {
        var headers = BundleScan.Run(client.Bundles(pattern), BundleHeader.Read, out var failures).Select(item => item.Result).ToList();

        if (entries)
        {
            foreach (var header in headers)
            {
                Console.WriteLine(
                    $"{header.Bundle.RelativePath}: {Sizes.Format(header.DiskSize)} on disk, {Sizes.Format(header.DataSize)} of data, "
                    + $"{header.Compression} in {header.Blocks.Count:N0} block(s) of up to {Sizes.Format(header.BlockSize)}, "
                    + $"UnityFS {header.Version}, engine {header.EngineVersion}");

                foreach (var entry in header.Entries)
                    Console.WriteLine($"  {entry.Name}  {Sizes.Format(entry.Size)}{(entry.IsSerialized ? "  serialized" : string.Empty)}");
            }
        }
        else
        {
            var table = new TextTable("Bundle", "#On disk", "#Decompressed", "Compression", "#Blocks", "#Block size", "#Entries");

            foreach (var header in headers)
            {
                table.Add(
                    header.Bundle.RelativePath,
                    Sizes.Format(header.DiskSize),
                    Sizes.Format(header.DataSize),
                    header.Compression,
                    header.Blocks.Count,
                    Sizes.Format(header.BlockSize),
                    header.Entries.Count);
            }

            table.Write(Console.Out);
        }

        Console.WriteLine();
        Console.WriteLine(
            $"{headers.Count:N0} bundle(s), {Sizes.Format(headers.Sum(header => header.DiskSize))} on disk, "
            + $"{Sizes.Format(headers.Sum(header => header.DataSize))} decompressed");

        return failures is 0 ? 0 : 1;
    }
}
