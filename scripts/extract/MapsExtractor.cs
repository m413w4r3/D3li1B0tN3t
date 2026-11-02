using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using AssetsTools.NET.Extra;
using D3B.Data.Maps;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads every map of <c>Content/Map/Data</c>, in both layouts, into <see cref="MapData"/>.</summary>
/// <remarks>
/// Bundles are read in parallel, one <see cref="UnityBundle"/> each, and the maps sorted by id afterwards, so the pack
/// does not depend on the order threads finish in.
/// </remarks>
internal static class MapsExtractor
{
    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return client.Bundles(Path.Combine("Map", "Data"));
    }

    public static IReadOnlyList<MapData> Read(ClientInstall client)
    {
        var bundles = Inputs(client);

        if (bundles.Count is 0)
            throw new FileNotFoundException($"The client has no maps: {Path.Combine(client.StreamingAssets, "Content", "Map", "Data")} holds no bundle.");

        var maps = new ConcurrentBag<MapData>();
        var layouts = new ConcurrentDictionary<ClientMapLayout, int>();
        var twoGraphics = 0;

        try
        {
            ReadAll(bundles, maps, layouts, ref twoGraphics);
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(inner => inner is IOException or InvalidDataException))
        {
            // The command reports what failed in a bundle, not that a parallel loop did.
            ExceptionDispatchInfo.Throw(exception.InnerExceptions[0]);
        }

        var sorted = maps.OrderBy(map => map.Id).ToList();

        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Id == sorted[i - 1].Id)
                throw new InvalidDataException($"The client holds map {sorted[i].Id} twice.");
        }

        Console.WriteLine(
            $"Maps: {sorted.Count:N0} ({string.Join(", ", layouts.OrderBy(layout => layout.Key).Select(layout => $"{layout.Value:N0} {layout.Key}"))}), "
            + $"{sorted.Sum(map => map.Interactives.Length):N0} interactive elements, "
            + $"{sorted.SelectMany(map => map.Interactives.ToArray()).Select(element => element.GfxId).Distinct().Count():N0} graphics; "
            + $"elements drawn twice on their cell with two graphics, kept with the lowest: {twoGraphics:N0}");

        return sorted;
    }

    private static void ReadAll(IReadOnlyList<string> bundles, ConcurrentBag<MapData> maps, ConcurrentDictionary<ClientMapLayout, int> layouts, ref int twoGraphics)
    {
        var total = 0;

        Parallel.ForEach(bundles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, path =>
        {
            using var bundle = new UnityBundle(path);

            foreach (var file in bundle.Files)
            {
                var metadata = file.file.Metadata;
                var reader = new MapReader(UnityReferences.Types(file));
                var trees = new Dictionary<int, (UnityTypeTree Tree, ClientMapLayout? Layout)>();

                foreach (var info in metadata.GetAssetsOfType(AssetClassID.MonoBehaviour))
                {
                    if (!trees.TryGetValue(info.TypeIdOrIndex, out var type))
                    {
                        var tree = UnityTypeTree.From(metadata.TypeTreeTypes[info.TypeIdOrIndex], "map");

                        type = (tree, MapReader.LayoutOf(tree));
                        trees.Add(info.TypeIdOrIndex, type);
                    }

                    // The map bundles also hold the dictionary of element definitions, which is not a map.
                    if (type.Layout is not { } layout)
                        continue;

                    try
                    {
                        maps.Add(reader.Read(UnityBundle.ReadObject(file, info), type.Tree, layout));
                    }
                    catch (Exception exception) when (exception is ArgumentException or OverflowException)
                    {
                        throw new InvalidDataException($"{Path.GetFileName(path)}, object {info.PathId}: {exception.Message}", exception);
                    }

                    layouts.AddOrUpdate(layout, 1, (_, count) => count + 1);
                }

                Interlocked.Add(ref total, reader.TwoGraphics);
            }
        });

        twoGraphics = total;
    }
}
