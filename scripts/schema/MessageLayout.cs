using System.Globalization;
using System.Reflection;
using Cpp2IL.Core.Model.Contexts;

namespace D3B.Scripts.Schema;

/// <summary>A message's fields and oneofs, read from the members protobuf's C# generator gives every message.</summary>
/// <remarks>
/// The generator declares, field after field, a constant holding the field's number, then the field's property: the
/// n-th constant is the n-th field property's number, whatever the obfuscated names. A property with a setter, or a list
/// or a map, is a field. A <c>bool</c> property with a getter only, right after a field's, tells whether that field is
/// set: a proto3 <c>optional</c> field, or a member of a oneof. A oneof has a getter-only property whose type is an enum
/// nested in the message, whose values but zero are its fields' numbers. Anything else fails the reading: a member the
/// generator does not write means a message this reading would misread.
/// </remarks>
internal sealed class MessageLayout
{
    private const string DescriptorName = "Google.Protobuf.Reflection.MessageDescriptor";

    /// <summary>The message's fields, in the order they are declared.</summary>
    public required IReadOnlyList<LayoutField> Fields { get; init; }

    /// <summary>The message's oneofs, in the order they are declared.</summary>
    public required IReadOnlyList<LayoutOneof> Oneofs { get; init; }

    /// <summary>The static class holding the message's own messages and enums, when it has any.</summary>
    public required TypeAnalysisContext? Types { get; init; }

    /// <summary>Reads the layout of a message.</summary>
    /// <exception cref="InvalidDataException">The message has a member the generator does not write, or numbers that do not pair.</exception>
    public static MessageLayout Read(TypeAnalysisContext message)
    {
        var numbers = Numbers(message);
        var caseEnums = message.NestedTypes.Where(nested => nested.IsEnumType).ToList();
        var fields = new List<LayoutField>();
        var oneofs = new List<LayoutOneof>();

        foreach (var property in message.Properties)
        {
            var type = property.PropertyType;

            if (property.IsStatic || type.FullName == DescriptorName)
                continue;

            if (property.Setter is not null || ProtocolTypes.KindOf(type) is not FieldKind.Singular)
            {
                if (fields.Count == numbers.Count)
                    throw new InvalidDataException($"{message.FullName} has more field properties than field numbers.");

                fields.Add(new LayoutField { Number = numbers[fields.Count], Name = property.Name, PropertyType = type });
            }
            else if (type.FullName == "System.Boolean")
            {
                var field = fields.Count > 0 ? fields[^1] : null;

                // Protobuf's C# code gives no such property to a message field: null says it is unset.
                if (field is null || field.HasPresence || field.Kind is not FieldKind.Singular || ProtocolTypes.IsMessage(field.PropertyType) || ProtocolTypes.IsAny(field.PropertyType))
                    throw new InvalidDataException($"{message.FullName}.{property.Name} tells whether a field is set, but follows no field that may be unset.");

                field.HasPresence = true;
            }
            else if (caseEnums.Contains(type))
            {
                oneofs.Add(new LayoutOneof { Name = property.Name, Numbers = [.. EnumValues(type).Where(value => value is not 0)] });
            }
            else
            {
                throw new InvalidDataException($"{message.FullName}.{property.Name}, of type {type.FullName}, is neither a field, nor whether a field is set, nor a oneof's case.");
            }
        }

        if (fields.Count != numbers.Count)
            throw new InvalidDataException($"{message.FullName} has {numbers.Count} field numbers for {fields.Count} field properties.");

        if (oneofs.Count != caseEnums.Count)
            throw new InvalidDataException($"{message.FullName} nests {caseEnums.Count} enums for {oneofs.Count} oneofs.");

        for (var index = 0; index < oneofs.Count; index++)
        {
            foreach (var number in oneofs[index].Numbers)
            {
                var field = fields.Find(candidate => candidate.Number == number)
                    ?? throw new InvalidDataException($"{message.FullName}: oneof {oneofs[index].Name} names field {number}, which the message lacks.");

                if (field.Kind is not FieldKind.Singular || field.OneofIndex >= 0)
                    throw new InvalidDataException($"{message.FullName}: field {number} cannot be in oneof {oneofs[index].Name}.");

                field.OneofIndex = index;
            }
        }

        return new MessageLayout { Fields = fields, Oneofs = oneofs, Types = TypesOf(message) };
    }

    /// <summary>The values of an enum, in the order they are declared.</summary>
    public static IEnumerable<int> EnumValues(TypeAnalysisContext type)
    {
        return type.Fields
            .Where(field => field.IsStatic && field.Attributes.HasFlag(FieldAttributes.Literal))
            .Select(field => Convert.ToInt32(field.ConstantValue, CultureInfo.InvariantCulture));
    }

    private static List<int> Numbers(TypeAnalysisContext message)
    {
        var numbers = new List<int>();

        foreach (var field in message.Fields.Where(field => field.Attributes.HasFlag(FieldAttributes.Literal)))
        {
            if (field.ConstantValue is not int number || number <= 0)
                throw new InvalidDataException($"{message.FullName}.{field.Name} is a constant but no field number.");

            numbers.Add(number);
        }

        if (numbers.Distinct().Count() != numbers.Count)
            throw new InvalidDataException($"{message.FullName} declares a field number twice.");

        return numbers;
    }

    private static TypeAnalysisContext? TypesOf(TypeAnalysisContext message)
    {
        TypeAnalysisContext? types = null;

        foreach (var nested in message.NestedTypes)
        {
            if (nested.IsEnumType || ProtocolTypes.IsCompilerGenerated(nested))
                continue;

            if (!nested.IsStatic || types is not null)
                throw new InvalidDataException($"{message.FullName} nests {nested.FullName}, which is not the one static class of its types.");

            types = nested;
        }

        return types;
    }
}
