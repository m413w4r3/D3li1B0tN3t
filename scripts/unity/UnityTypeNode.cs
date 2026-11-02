namespace D3B.Scripts.Unity;

/// <summary>A node of a <see cref="UnityTypeTree"/>: a field, its type, and how it is laid out.</summary>
internal sealed class UnityTypeNode
{
    public required string Type { get; init; }

    public required string Name { get; init; }

    /// <summary>The field's name as the source names it: an auto-property's <c>&lt;x&gt;k__BackingField</c> is <c>x</c>.</summary>
    /// <remarks>Some layouts of a class serialize a field, others the backing field of a property of the same name.</remarks>
    public required string Field { get; init; }

    /// <summary>The size of a primitive, in bytes; for other nodes, Unity's sum of their fields, padding excluded.</summary>
    public int ByteSize { get; init; }

    /// <summary>Whether the reader skips to a four-byte boundary after the node.</summary>
    public bool Align { get; init; }

    /// <summary>Whether the node holds a count, then that many items of its second child.</summary>
    public bool IsArray { get; init; }

    /// <summary>Whether the node is a registry of managed references, written once at the end of an object.</summary>
    public bool IsRegistry { get; init; }

    public required int[] Children { get; init; }
}
