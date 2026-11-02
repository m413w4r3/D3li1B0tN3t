using Cpp2IL.Core.Model.Contexts;

namespace D3B.Scripts.Schema;

/// <summary>A field of a message, as its metadata declares it: a number constant and the property it pairs with.</summary>
internal sealed class LayoutField
{
    /// <summary>The field's number.</summary>
    public required int Number { get; init; }

    /// <summary>The name of the field's property, obfuscated.</summary>
    public required string Name { get; init; }

    /// <summary>The type of the field's property.</summary>
    public required TypeAnalysisContext PropertyType { get; init; }

    /// <summary>Whether the field holds one value, a list or a map.</summary>
    public FieldKind Kind => ProtocolTypes.KindOf(PropertyType);

    /// <summary>The types of the field's values: the property's type, a list's elements, or a map's key and value.</summary>
    public IReadOnlyList<TypeAnalysisContext> ValueTypes => ProtocolTypes.ValueTypesOf(PropertyType);

    /// <summary>Whether a property telling whether the field is set follows the field's: a proto3 <c>optional</c> field, or a member of a oneof.</summary>
    public bool HasPresence { get; set; }

    /// <summary>The index of the oneof the field belongs to, or -1.</summary>
    public int OneofIndex { get; set; } = -1;
}
