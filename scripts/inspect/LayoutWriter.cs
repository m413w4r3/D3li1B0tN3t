using AssetsTools.NET;

namespace D3B.Scripts.Inspect;

/// <summary>Writes the type tree of a type: the layout its objects are serialized with.</summary>
/// <remarks>
/// Each line is a node: its type, its name, its size in bytes or <c>var</c>, and its flags. <c>align</c> means the reader
/// skips to a four-byte boundary after the node; <c>array</c> marks the node holding a size then the items.
/// </remarks>
internal static class LayoutWriter
{
    private const uint AlignFlag = 0x4000;

    public static void Write(TextWriter writer, TypeTreeType type, int maxDepth)
    {
        var strings = type.StringBufferBytes;
        var common = TypeTreeType.COMMON_STRING_TABLE;
        var table = new TextTable("Field", "#Size", "Flags");

        foreach (var node in type.Nodes.Where(node => node.Level <= maxDepth))
        {
            var indent = new string(' ', node.Level * 2);
            var flags = new List<string>();

            if ((node.MetaFlags & AlignFlag) is not 0)
                flags.Add("align");

            if (node.TypeFlags.HasFlag(TypeTreeNodeFlags.Array))
                flags.Add("array");

            if (node.TypeFlags.HasFlag(TypeTreeNodeFlags.Ref))
                flags.Add("ref");

            if (node.TypeFlags.HasFlag(TypeTreeNodeFlags.Registry))
                flags.Add("registry");

            table.Add(
                $"{indent}{node.GetTypeString(strings, common)} {node.GetNameString(strings, common)}",
                node.ByteSize < 0 ? "var" : $"{node.ByteSize}",
                string.Join(' ', flags));
        }

        table.Write(writer);
    }
}
