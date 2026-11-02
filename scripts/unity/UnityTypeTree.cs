using AssetsTools.NET;

namespace D3B.Scripts.Unity;

/// <summary>The type tree of a serialized type, flattened with the children of each node, to read objects without building them.</summary>
/// <remarks>
/// AssetsTools.NET's generic reading builds one object per field, too slow for the 4 GiB of maps (client.md). Reading
/// through the type tree instead walks the bytes, reads the fields the extractor asks for by name and type, and skips
/// the others, so that a field the client adds changes nothing while a field it removes or retypes fails loudly.
/// </remarks>
internal sealed class UnityTypeTree
{
    private const uint AlignFlag = 0x4000;

    private const string BackingFieldSuffix = ">k__BackingField";

    private readonly UnityTypeNode[] _nodes;

    /// <summary>The type's name, for errors.</summary>
    public string Name { get; }

    public UnityTypeNode this[int index] => _nodes[index];

    private UnityTypeTree(UnityTypeNode[] nodes, string name)
    {
        _nodes = nodes;
        Name = name;
    }

    public static UnityTypeTree From(TypeTreeType type, string name)
    {
        var strings = type.StringBufferBytes;
        var common = TypeTreeType.COMMON_STRING_TABLE;
        var source = type.Nodes;
        var children = new List<int>[source.Count];
        var parents = new Stack<int>();

        for (var i = 0; i < source.Count; i++)
        {
            children[i] = [];

            while (parents.Count > 0 && source[parents.Peek()].Level >= source[i].Level)
                parents.Pop();

            if (parents.Count > 0)
                children[parents.Peek()].Add(i);

            parents.Push(i);
        }

        var nodes = new UnityTypeNode[source.Count];

        for (var i = 0; i < source.Count; i++)
        {
            var node = source[i];
            var field = node.GetNameString(strings, common);

            nodes[i] = new UnityTypeNode
            {
                Type = node.GetTypeString(strings, common),
                Name = field,
                Field = field.StartsWith('<') && field.EndsWith(BackingFieldSuffix, StringComparison.Ordinal) ? field[1..^BackingFieldSuffix.Length] : field,
                ByteSize = node.ByteSize,
                Align = (node.MetaFlags & AlignFlag) is not 0,
                IsArray = node.TypeFlags.HasFlag(TypeTreeNodeFlags.Array),
                IsRegistry = node.TypeFlags.HasFlag(TypeTreeNodeFlags.Registry),
                Children = [.. children[i]],
            };
        }

        return new UnityTypeTree(nodes, name);
    }

    /// <summary>Finds a child by name.</summary>
    /// <returns>Its index, or -1.</returns>
    public int Child(int node, string name)
    {
        foreach (var child in _nodes[node].Children)
        {
            if (_nodes[child].Name == name)
                return child;
        }

        return -1;
    }

    /// <summary>Tells whether the root holds every one of these fields.</summary>
    public bool HasFields(params ReadOnlySpan<string> names)
    {
        foreach (var name in names)
        {
            if (Child(0, name) is -1)
                return false;
        }

        return true;
    }

    /// <summary>The array node of a field: the field itself, or its child for vectors and strings.</summary>
    public int ArrayOf(int node)
    {
        if (_nodes[node].IsArray)
            return node;

        foreach (var child in _nodes[node].Children)
        {
            if (_nodes[child].IsArray)
                return child;
        }

        throw Unexpected(node, "an array");
    }

    public InvalidDataException Unexpected(int node, string expected)
    {
        var field = _nodes[node];

        return new InvalidDataException(
            $"{Name}.{field.Name} is a {field.Type}, where the extractor expects {expected}: the client changed this type, so the extractor needs updating.");
    }
}
