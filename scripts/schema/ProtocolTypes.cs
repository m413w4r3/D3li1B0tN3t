using Cpp2IL.Core.Model.Contexts;

namespace D3B.Scripts.Schema;

/// <summary>What protobuf's C# code makes of a protocol's types, told from the client's metadata.</summary>
internal static class ProtocolTypes
{
    /// <summary>The full name of <c>google.protobuf.Any</c>, the one well-known type the protocol uses.</summary>
    public const string AnyName = ".google.protobuf.Any";

    /// <summary>The file declaring <c>google.protobuf.Any</c>.</summary>
    public const string AnyFile = "google/protobuf/any.proto";

    private const string AnyClrName = "Google.Protobuf.WellKnownTypes.Any";
    private const string RepeatedFieldName = "Google.Protobuf.Collections.RepeatedField`1";
    private const string MapFieldName = "Google.Protobuf.Collections.MapField`2";

    /// <summary>Tells whether a type is a generated message: every one implements <c>IBufferMessage</c>.</summary>
    public static bool IsMessage(TypeAnalysisContext type)
    {
        return type.InterfaceContexts.Any(contract => contract.FullName == "Google.Protobuf.IBufferMessage") && !IsAny(type);
    }

    /// <summary>Tells whether a type is <c>google.protobuf.Any</c>, the envelope of the game connection's messages.</summary>
    public static bool IsAny(TypeAnalysisContext type)
    {
        return type.FullName == AnyClrName;
    }

    /// <summary>Tells whether a nested type is the compiler's, such as the <c>&lt;&gt;c</c> class of a message's lambdas.</summary>
    public static bool IsCompilerGenerated(TypeAnalysisContext type)
    {
        return type.Name.StartsWith('<');
    }

    /// <summary>Tells what a field property's type makes of the field: one value, a list or a map.</summary>
    public static FieldKind KindOf(TypeAnalysisContext type)
    {
        if (type is not GenericInstanceTypeAnalysisContext generic)
            return FieldKind.Singular;

        return generic.GenericType.FullName switch
        {
            RepeatedFieldName => FieldKind.Repeated,
            MapFieldName => FieldKind.Map,
            _ => FieldKind.Singular,
        };
    }

    /// <summary>The types of a field's values: the property's type, a list's elements, or a map's key and value.</summary>
    public static IReadOnlyList<TypeAnalysisContext> ValueTypesOf(TypeAnalysisContext type)
    {
        return KindOf(type) is FieldKind.Singular ? [type] : ((GenericInstanceTypeAnalysisContext)type).GenericArguments;
    }

    /// <summary>The protobuf full name of a message or an enum, with its leading dot, such as <c>.mga.mfy</c>.</summary>
    /// <remarks>
    /// The generated code nests a message's own messages and enums in a static class, <c>Types</c>, which protobuf's names
    /// skip. The protocol's types have no namespace, so their names have no package.
    /// </remarks>
    /// <exception cref="InvalidDataException">The type is nested in something that is neither a message nor its types.</exception>
    public static string FullNameOf(TypeAnalysisContext type)
    {
        if (IsAny(type))
            return AnyName;

        var parts = new List<string>();

        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (current == type || IsMessage(current))
                parts.Add(current.Name);
            else if (!current.IsStatic || current.DeclaringType is null || !IsMessage(current.DeclaringType))
                throw new InvalidDataException($"{type.FullName} is nested in {current.FullName}, which is neither a message nor a message's types.");
        }

        parts.Reverse();

        return "." + string.Join('.', parts);
    }
}
