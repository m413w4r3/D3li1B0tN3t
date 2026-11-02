namespace D3B.Scripts.Schema;

/// <summary>A oneof of a message, as its metadata declares it: the property telling which field is set, and its enum.</summary>
internal sealed class LayoutOneof
{
    /// <summary>The name of the property telling which field is set, obfuscated.</summary>
    public required string Name { get; init; }

    /// <summary>The numbers of the oneof's fields: the values of its case enum, but for zero, which means none.</summary>
    public required IReadOnlyList<int> Numbers { get; init; }
}
