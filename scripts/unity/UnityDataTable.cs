using AssetsTools.NET.Extra;

namespace D3B.Scripts.Unity;

/// <summary>A game data table: the one <c>DataRoot</c> of <c>Content/Data/data_assets_&lt;name&gt;dataroot.asset.bundle</c>.</summary>
/// <remarks>
/// Its <c>objectsById</c> pairs each key with a managed reference, and its registry holds the records, each read as
/// <see cref="UnityFields"/> through its class's type tree: a field the client adds changes nothing, and one it removes
/// or retypes fails when asked for.
/// </remarks>
internal static class UnityDataTable
{
    /// <summary>The bundle of a table.</summary>
    /// <param name="client">The client.</param>
    /// <param name="name">The table's name in the bundle's file name, such as <c>waypoints</c>.</param>
    public static string PathOf(ClientInstall client, string name)
    {
        return Path.Combine(client.StreamingAssets, "Content", "Data", $"data_assets_{name}dataroot.asset.bundle");
    }

    /// <summary>Reads every record of a table, in the order of its keys.</summary>
    /// <param name="client">The client.</param>
    /// <param name="name">The table's name in the bundle's file name, such as <c>waypoints</c>.</param>
    /// <exception cref="InvalidDataException">The bundle holds no table, or a key names a record the table lacks.</exception>
    public static IReadOnlyList<(long Key, UnityFields Record)> Read(ClientInstall client, string name)
    {
        return ReadWithRegistry(client, name).Table;
    }

    /// <summary>Reads every record of a table whose records hold their own key in an <c>id</c>, in the order of its keys.</summary>
    /// <param name="client">The client.</param>
    /// <param name="name">The table's name in the bundle's file name, such as <c>items</c>.</param>
    /// <exception cref="InvalidDataException">The bundle holds no table, or a key names a record the table lacks or of another id.</exception>
    public static IReadOnlyList<UnityFields> ReadById(ClientInstall client, string name)
    {
        return ById(name, Read(client, name));
    }

    /// <summary>Reads every record of a table whose records hold their own key in an <c>id</c>, and every managed reference of its registry by identifier.</summary>
    /// <remarks>A record names some of its parts by reference, such as a weapon its effects, which only the registry holds.</remarks>
    /// <param name="client">The client.</param>
    /// <param name="name">The table's name in the bundle's file name, such as <c>items</c>.</param>
    /// <exception cref="InvalidDataException">The bundle holds no table, or a key names a record the table lacks or of another id.</exception>
    public static (IReadOnlyList<UnityFields> Records, IReadOnlyDictionary<long, UnityFields> References) ReadByIdWithReferences(ClientInstall client, string name)
    {
        var (table, references) = ReadWithRegistry(client, name);

        return (ById(name, table), references);
    }

    private static (List<(long Key, UnityFields Record)> Table, Dictionary<long, UnityFields> References) ReadWithRegistry(ClientInstall client, string name)
    {
        var path = PathOf(client, name);

        if (!File.Exists(path))
            throw new FileNotFoundException($"The client has no {name} table: {path} does not exist.", path);

        using var bundle = new UnityBundle(path);

        foreach (var file in bundle.Files)
        {
            var metadata = file.file.Metadata;
            var references = UnityReferences.Types(file);

            foreach (var info in metadata.GetAssetsOfType(AssetClassID.MonoBehaviour))
            {
                var tree = UnityTypeTree.From(metadata.TypeTreeTypes[info.TypeIdOrIndex], $"{name} table");

                if (!tree.HasFields("objectsById", "references"))
                    continue;

                return ReadTable(UnityBundle.ReadObject(file, info), tree, references);
            }
        }

        throw new InvalidDataException($"{path} holds no data table.");
    }

    private static List<UnityFields> ById(string name, IReadOnlyList<(long Key, UnityFields Record)> table)
    {
        var records = new List<UnityFields>(table.Count);

        foreach (var (key, record) in table)
        {
            var id = record.Integer("id");

            if (key != id)
                throw new InvalidDataException($"The {name} table keys record {id} as {key}.");

            records.Add(record);
        }

        return records;
    }

    private static (List<(long Key, UnityFields Record)> Table, Dictionary<long, UnityFields> References) ReadTable(byte[] data, UnityTypeTree tree, Dictionary<string, UnityTypeTree> references)
    {
        var reader = new UnityReader(data, tree);
        long[] keys = [];
        long[] values = [];
        var records = new Dictionary<long, UnityFields>();

        foreach (var child in tree[0].Children)
        {
            switch (tree[child].Name)
            {
                case "objectsById":
                    foreach (var part in tree[child].Children)
                    {
                        switch (tree[part].Name)
                        {
                            case "m_keys":
                                keys = reader.ReadInt64Array(part);
                                break;
                            case "m_values":
                                values = reader.ReadReferenceArray(part);
                                break;
                            default:
                                reader.Skip(part);
                                break;
                        }
                    }

                    reader.AlignAfter(child);
                    break;
                case "references":
                    UnityReferences.ReadRegistry(ref reader, data, child, references, (long id, string className, ref UnityReader record) =>
                    {
                        records[id] = UnityFields.Read(ref record, 0, className);

                        return true;
                    });
                    break;
                default:
                    reader.Skip(child);
                    break;
            }
        }

        reader.ExpectEnd();

        if (keys.Length != values.Length)
            throw new InvalidDataException($"{tree.Name}: {keys.Length} keys for {values.Length} records.");

        var table = new List<(long Key, UnityFields Record)>(keys.Length);

        for (var i = 0; i < keys.Length; i++)
        {
            if (!records.TryGetValue(values[i], out var record))
                throw new InvalidDataException($"{tree.Name}: key {keys[i]} names a record the table does not hold.");

            table.Add((keys[i], record));
        }

        return (table, records);
    }
}
