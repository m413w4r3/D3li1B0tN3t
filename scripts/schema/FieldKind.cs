namespace D3B.Scripts.Schema;

/// <summary>What a message's field holds.</summary>
internal enum FieldKind
{
    /// <summary>One value, a property with a setter.</summary>
    Singular,

    /// <summary>A list, a <c>RepeatedField&lt;T&gt;</c>.</summary>
    Repeated,

    /// <summary>A map, a <c>MapField&lt;TKey, TValue&gt;</c>.</summary>
    Map,
}
