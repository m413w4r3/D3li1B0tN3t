using Cpp2IL.Core.Model.Contexts;
using FieldType = Google.Protobuf.Reflection.FieldDescriptorProto.Types.Type;

namespace D3B.Scripts.Schema;

/// <summary>How protobuf's C# code writes a field: the name its methods share, the protobuf type and the wire type.</summary>
/// <remarks>
/// The generated code writes a field with <c>WriteContext.Write&lt;Name&gt;</c> and builds a list's codec with
/// <c>FieldCodec.For&lt;Name&gt;</c>, so the name read in the client's code says the protobuf type, which the field's C# type
/// alone does not: an <c>int</c> may be an <c>int32</c>, a <c>sint32</c> or a <c>sfixed32</c>.
/// </remarks>
internal sealed class FieldEncoding
{
    private const string MessageType = "message";
    private const string EnumType = "enum";

    private static readonly FieldEncoding[] _all =
    [
        new() { Name = "Int32", Type = FieldType.Int32, WireType = 0, ClrType = "System.Int32" },
        new() { Name = "SInt32", Type = FieldType.Sint32, WireType = 0, ClrType = "System.Int32" },
        new() { Name = "SFixed32", Type = FieldType.Sfixed32, WireType = 5, ClrType = "System.Int32" },
        new() { Name = "UInt32", Type = FieldType.Uint32, WireType = 0, ClrType = "System.UInt32" },
        new() { Name = "Fixed32", Type = FieldType.Fixed32, WireType = 5, ClrType = "System.UInt32" },
        new() { Name = "Int64", Type = FieldType.Int64, WireType = 0, ClrType = "System.Int64" },
        new() { Name = "SInt64", Type = FieldType.Sint64, WireType = 0, ClrType = "System.Int64" },
        new() { Name = "SFixed64", Type = FieldType.Sfixed64, WireType = 1, ClrType = "System.Int64" },
        new() { Name = "UInt64", Type = FieldType.Uint64, WireType = 0, ClrType = "System.UInt64" },
        new() { Name = "Fixed64", Type = FieldType.Fixed64, WireType = 1, ClrType = "System.UInt64" },
        new() { Name = "Bool", Type = FieldType.Bool, WireType = 0, ClrType = "System.Boolean" },
        new() { Name = "Float", Type = FieldType.Float, WireType = 5, ClrType = "System.Single" },
        new() { Name = "Double", Type = FieldType.Double, WireType = 1, ClrType = "System.Double" },
        new() { Name = "String", Type = FieldType.String, WireType = 2, ClrType = "System.String" },
        new() { Name = "Bytes", Type = FieldType.Bytes, WireType = 2, ClrType = "Google.Protobuf.ByteString" },
        new() { Name = "Enum", Type = FieldType.Enum, WireType = 0, ClrType = EnumType },
        new() { Name = "Message", Type = FieldType.Message, WireType = 2, ClrType = MessageType },
    ];

    /// <summary>The name of the methods writing it, after <c>Write</c> or <c>For</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The field's protobuf type.</summary>
    public required FieldType Type { get; init; }

    /// <summary>The wire type of one value.</summary>
    public required int WireType { get; init; }

    /// <summary>Whether a list of it is packed by default in proto3: numbers, booleans and enums.</summary>
    public bool IsPackable => WireType is not 2;

    private string ClrType { get; init; } = string.Empty;

    /// <summary>Finds an encoding by the name of the methods writing it.</summary>
    public static FieldEncoding? Find(string name)
    {
        return Array.Find(_all, encoding => encoding.Name == name);
    }

    /// <summary>The encoding of a C# type when the code says nothing else: <c>int</c> is an <c>int32</c>.</summary>
    /// <exception cref="InvalidDataException">No protobuf type is written as this C# type.</exception>
    public static FieldEncoding DefaultOf(TypeAnalysisContext type)
    {
        var category = CategoryOf(type);

        return Array.Find(_all, encoding => encoding.ClrType == category)
            ?? throw new InvalidDataException($"No protobuf type is written as {type.FullName}.");
    }

    /// <summary>Tells whether a field of a C# type can be written with this encoding.</summary>
    public bool Fits(TypeAnalysisContext type)
    {
        return ClrType == CategoryOf(type);
    }

    private static string CategoryOf(TypeAnalysisContext type)
    {
        if (ProtocolTypes.IsMessage(type) || ProtocolTypes.IsAny(type))
            return MessageType;

        return type.IsEnumType ? EnumType : type.FullName;
    }
}
