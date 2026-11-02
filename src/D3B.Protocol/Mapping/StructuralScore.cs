using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>How alike a readable type and an obfuscated one are, from their structure alone, from 0 to 1.</summary>
/// <remarks>
/// A message scores the share of its fields, and of its counterpart's, that the other has under the same number with the
/// same shape: label, presence, oneof, and type. A field whose type is already paired scores 1 when the counterpart's
/// field has the paired type, 0 otherwise; when neither is paired, half for the kind, and half for how alike the two
/// types are, one level down. Two empty messages score 1: only where they are used tells them apart.
/// </remarks>
internal static class StructuralScore
{
    /// <summary>Scores a readable message against an obfuscated one, given the pairs found so far.</summary>
    public static double Message(MessageDescriptor readable, MessageDescriptor obfuscated, MatchState state)
    {
        var count = Math.Max(readable.Fields.InDeclarationOrder().Count, obfuscated.Fields.InDeclarationOrder().Count);

        if (count is 0)
            return 1;

        var sum = 0.0;

        foreach (var field in readable.Fields.InFieldNumberOrder())
        {
            var counterpart = obfuscated.FindFieldByNumber(field.FieldNumber);

            if (counterpart is not null)
                sum += Field(field, counterpart, state);
        }

        return sum / count;
    }

    /// <summary>Scores a readable enum against an obfuscated one: the share of their value numbers both have.</summary>
    public static double Enum(EnumDescriptor readable, EnumDescriptor obfuscated)
    {
        var values = readable.Values.Select(value => value.Number).ToHashSet();
        var others = obfuscated.Values.Select(value => value.Number).ToHashSet();
        var count = Math.Max(values.Count, others.Count);

        return count is 0 ? 1 : (double)values.Count(others.Contains) / count;
    }

    private static double Field(FieldDescriptor readable, FieldDescriptor obfuscated, MatchState state)
    {
        if (!MatchState.SameShape(readable, obfuscated)
            || Descriptors.InRealOneof(readable) != Descriptors.InRealOneof(obfuscated)
            || Descriptors.IsOptional(readable) != Descriptors.IsOptional(obfuscated))
        {
            return 0;
        }

        if (readable.FieldType is FieldType.Message or FieldType.Group)
        {
            if (Descriptors.IsWellKnown(readable.MessageType) || Descriptors.IsWellKnown(obfuscated.MessageType))
                return readable.MessageType.FullName == obfuscated.MessageType.FullName ? 1 : 0;

            var paired = state.CounterpartOf(readable.MessageType);

            if (paired is not null)
                return paired == obfuscated.MessageType ? 1 : 0;

            return state.ReadableOf(obfuscated.MessageType) is not null ? 0 : 0.5 + (0.5 * Shallow(readable.MessageType, obfuscated.MessageType));
        }

        if (readable.FieldType is FieldType.Enum)
        {
            var paired = state.CounterpartOf(readable.EnumType);

            if (paired is not null)
                return paired == obfuscated.EnumType ? 1 : 0;

            return state.ReadableOf(obfuscated.EnumType) is not null ? 0 : 0.5 + (0.5 * Enum(readable.EnumType, obfuscated.EnumType));
        }

        return 1;
    }

    private static double Shallow(MessageDescriptor readable, MessageDescriptor obfuscated)
    {
        var count = Math.Max(readable.Fields.InDeclarationOrder().Count, obfuscated.Fields.InDeclarationOrder().Count);

        if (count is 0)
            return 1;

        var alike = readable.Fields.InFieldNumberOrder().Count(field => obfuscated.FindFieldByNumber(field.FieldNumber) is { } counterpart
            && MatchState.SameShape(field, counterpart)
            && Descriptors.InRealOneof(field) == Descriptors.InRealOneof(counterpart));

        return (double)alike / count;
    }
}
