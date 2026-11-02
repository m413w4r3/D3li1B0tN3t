using AssetsTools.NET.Extra;

namespace D3B.Scripts.Unity;

/// <summary>Reads managed references (<c>[SerializeReference]</c>): their classes' type trees, and an object's registry.</summary>
/// <remarks>
/// An object lists the identifiers of the objects it references, and its registry, written once at its end, holds them,
/// each laid out by the type tree of its own class, which the serialized file stores beside its types.
/// </remarks>
internal static class UnityReferences
{
    /// <summary>The type trees of a serialized file's managed reference classes, by full name.</summary>
    public static Dictionary<string, UnityTypeTree> Types(AssetsFileInstance file)
    {
        var types = new Dictionary<string, UnityTypeTree>(StringComparer.Ordinal);

        foreach (var type in file.file.Metadata.RefTypes ?? [])
        {
            if (type.TypeReference is not { } reference)
                continue;

            var name = reference.Namespace.Length is 0 ? reference.ClassName : $"{reference.Namespace}.{reference.ClassName}";

            types.TryAdd(name, UnityTypeTree.From(type, reference.ClassName));
        }

        return types;
    }

    /// <summary>Reads a registry, handing each referenced object to <paramref name="visit"/>.</summary>
    /// <param name="reader">The reader, at the registry.</param>
    /// <param name="data">The object's bytes, which the readers of referenced objects share.</param>
    /// <param name="node">The registry's node.</param>
    /// <param name="types">The type trees of the file's reference classes, from <see cref="Types"/>.</param>
    /// <param name="visit">Reads the objects it wants.</param>
    public static void ReadRegistry(ref UnityReader reader, ReadOnlySpan<byte> data, int node, IReadOnlyDictionary<string, UnityTypeTree> types, UnityReferenceVisitor visit)
    {
        var tree = reader.Tree;

        foreach (var child in tree[node].Children)
        {
            switch (tree[child].Name)
            {
                case "version":
                    var version = reader.ReadInt32(child);

                    if (version is not 2)
                        throw new InvalidDataException($"{tree.Name}: the reference registry is version {version}; the extractor reads version 2.");

                    break;
                case "RefIds":
                    ReadObjects(ref reader, data, child, types, visit);
                    break;
                default:
                    reader.Skip(child);
                    break;
            }
        }

        reader.AlignAfter(node);
    }

    private static void ReadObjects(ref UnityReader reader, ReadOnlySpan<byte> data, int node, IReadOnlyDictionary<string, UnityTypeTree> types, UnityReferenceVisitor visit)
    {
        var tree = reader.Tree;
        var item = reader.BeginArray(node, out var count);
        var rid = tree.Child(item, "rid");
        var type = tree.Child(item, "type");

        if (rid is -1 || type is -1 || tree[type].Children.Length is not 3)
            throw tree.Unexpected(item, "a referenced object of an identifier, a type and data");

        for (var i = 0; i < count; i++)
        {
            var id = reader.ReadInt64(rid);
            var className = reader.ReadString(tree[type].Children[0]);
            var ns = reader.ReadString(tree[type].Children[1]);

            reader.Skip(tree[type].Children[2]);
            reader.AlignAfter(type);

            // The null reference has no type and no data.
            if (className.Length is 0)
            {
                reader.AlignAfter(item);

                continue;
            }

            var fullName = ns.Length is 0 ? className : $"{ns}.{className}";
            var objectTree = types.GetValueOrDefault(fullName)
                ?? throw new InvalidDataException($"{tree.Name} references a {fullName}, whose type tree the file does not hold.");
            var objectReader = new UnityReader(data, objectTree) { Position = reader.Position };

            if (!visit(id, fullName, ref objectReader))
            {
                foreach (var field in objectTree[0].Children)
                    objectReader.Skip(field);
            }

            reader.Position = objectReader.Position;
            reader.AlignAfter(item);
        }

        reader.EndArray(node);
    }
}
