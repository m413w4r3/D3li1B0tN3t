using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>Checks a mapping against the readable protocol and the build's schema: the contract a mapping must hold (D44).</summary>
/// <remarks>
/// Every readable message and enum must have a counterpart in the schema, one to one, and every readable field one with
/// the number the mapping gives, the same type, through the mapping for messages and enums, the same label and
/// presence, and the same oneof: the members of a readable oneof are the members of one oneof of the counterpart, and
/// no other readable oneof's. Every readable enum value must be a value of the counterpart. What the build has beyond
/// the readable protocol is noted, not failed: a reader meets it as unknown fields. A partial message leaves some of its
/// counterpart's fields out on purpose, which is counted apart from the notes, so that a note is what a build added. The
/// mapping must also name the format, the schema and the readable protocol it is checked with.
/// </remarks>
public static class ContractAudit
{
    /// <summary>Audits a mapping.</summary>
    /// <param name="readable">The readable protocol's files.</param>
    /// <param name="schema">The build's schema.</param>
    /// <param name="mapping">The mapping.</param>
    public static AuditReport Audit(IReadOnlyList<FileDescriptor> readable, ProtocolSchema schema, ProtocolMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(readable);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(mapping);

        var failures = new List<string>();
        var notes = new List<string>();
        var unread = new List<string>();

        if (mapping.FormatVersion != MappingFile.FormatVersion)
            failures.Add($"The mapping is of format {mapping.FormatVersion}, where this reader reads format {MappingFile.FormatVersion}.");

        if (!mapping.SchemaSha256.Span.SequenceEqual([.. schema.Sha256]))
            failures.Add("The mapping was matched against another schema.");

        if (!mapping.ReadableDigest.Span.SequenceEqual(ProtocolDigest.Of(readable)))
            failures.Add("The mapping was matched against another readable protocol.");

        var messages = readable.SelectMany(file => Descriptors.Messages(file.MessageTypes)).ToList();
        var enums = readable.SelectMany(Descriptors.Enums).ToList();
        var messagePairs = Index(mapping.Messages.Select(pair => (pair.Readable, pair.Obfuscated, pair)), "message", failures);
        var enumPairs = Index(mapping.Enums.Select(pair => (pair.Readable, pair.Obfuscated, pair)), "enum", failures);
        var fields = 0;
        var values = 0;

        foreach (var message in messages)
        {
            if (!messagePairs.TryGetValue(message.FullName, out var pair))
            {
                failures.Add($"{message.FullName} has no counterpart.");

                continue;
            }

            var counterpart = schema.FindMessage(pair.Obfuscated);

            if (counterpart is null)
            {
                failures.Add($"{message.FullName} is {pair.Obfuscated}, which the schema lacks.");

                continue;
            }

            fields += message.Fields.InDeclarationOrder().Count;
            AuditFields(message, counterpart, pair, messagePairs, enumPairs, failures, Descriptors.IsPartial(message) ? unread : notes);
        }

        foreach (var type in enums)
        {
            if (!enumPairs.TryGetValue(type.FullName, out var pair))
            {
                failures.Add($"{type.FullName} has no counterpart.");

                continue;
            }

            var counterpart = schema.FindEnum(pair.Obfuscated);

            if (counterpart is null)
            {
                failures.Add($"{type.FullName} is {pair.Obfuscated}, which the schema lacks.");

                continue;
            }

            var readableValues = type.Values.Select(value => value.Number).ToHashSet();
            var numbers = Numbers(pair.Values, readableValues, $"value of {type.FullName}", failures);
            var paired = new HashSet<int>();

            foreach (var number in readableValues.Order())
            {
                values++;

                if (!numbers.TryGetValue(number, out var obfuscated))
                    failures.Add($"Value {number} of {type.FullName} has no counterpart.");
                else if (counterpart.FindValueByNumber(obfuscated) is null)
                    failures.Add($"Value {number} of {type.FullName} is value {obfuscated} of {counterpart.FullName}, which has none.");
                else if (!paired.Add(obfuscated))
                    failures.Add($"Value {obfuscated} of {counterpart.FullName} is the counterpart of two values of {type.FullName}.");
            }

            var extra = counterpart.Values.Select(value => value.Number).Distinct().Count(number => !paired.Contains(number));

            if (extra > 0)
                notes.Add($"{type.FullName}: {extra} values of {counterpart.FullName} have no readable counterpart.");
        }

        foreach (var pair in mapping.Messages.Where(pair => messages.All(message => message.FullName != pair.Readable)))
            failures.Add($"The mapping pairs {pair.Readable}, which the readable protocol lacks.");

        foreach (var pair in mapping.Enums.Where(pair => enums.All(type => type.FullName != pair.Readable)))
            failures.Add($"The mapping pairs {pair.Readable}, which the readable protocol lacks.");

        return new AuditReport(failures, notes, unread, messages.Count, fields, enums.Count, values);
    }

    private static Dictionary<string, T> Index<T>(IEnumerable<(string Readable, string Obfuscated, T Pair)> pairs, string kind, List<string> failures)
    {
        var byReadable = new Dictionary<string, T>(StringComparer.Ordinal);
        var byObfuscated = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (readable, obfuscated, pair) in pairs)
        {
            if (!byReadable.TryAdd(readable, pair))
                failures.Add($"The mapping pairs the {kind} {readable} twice.");

            if (!byObfuscated.TryAdd(obfuscated, readable))
                failures.Add($"The {kind} {obfuscated} is the counterpart of both {byObfuscated[obfuscated]} and {readable}.");
        }

        return byReadable;
    }

    /// <summary>Indexes number pairs by readable number, failing a pair twice and a pair of a number the readable type lacks.</summary>
    /// <remarks>
    /// A pair of a number the readable type lacks would be read past by everything else here, yet tell a reader to expect
    /// that obfuscated number: a pin mistyped, or a mapping made for another readable protocol.
    /// </remarks>
    private static Dictionary<int, int> Numbers(IEnumerable<NumberPair> pairs, HashSet<int> readable, string kind, List<string> failures)
    {
        var numbers = new Dictionary<int, int>();

        foreach (var pair in pairs)
        {
            if (!readable.Contains(pair.Readable))
                failures.Add($"The mapping pairs {kind} {pair.Readable}, which the readable protocol lacks.");
            else if (!numbers.TryAdd(pair.Readable, pair.Obfuscated))
                failures.Add($"The mapping pairs {kind} {pair.Readable} twice.");
        }

        return numbers;
    }

    private static void AuditFields(
        MessageDescriptor message,
        MessageDescriptor counterpart,
        MessagePair pair,
        Dictionary<string, MessagePair> messagePairs,
        Dictionary<string, EnumPair> enumPairs,
        List<string> failures,
        List<string> notes)
    {
        var numbers = Numbers(pair.Fields, message.Fields.InDeclarationOrder().Select(field => field.FieldNumber).ToHashSet(), $"field of {message.FullName}", failures);
        var paired = new Dictionary<int, FieldDescriptor>();
        var oneofs = new Dictionary<OneofDescriptor, OneofDescriptor>();
        var claimed = new Dictionary<OneofDescriptor, OneofDescriptor>();

        foreach (var field in message.Fields.InFieldNumberOrder())
        {
            var name = $"Field {field.FieldNumber} of {message.FullName}, {Descriptors.TypeOf(field)} {field.Name},";

            if (!numbers.TryGetValue(field.FieldNumber, out var number))
            {
                failures.Add($"{name} has no counterpart in {counterpart.FullName}.");

                continue;
            }

            var other = counterpart.FindFieldByNumber(number);

            if (other is null)
            {
                failures.Add($"{name} is field {number} of {counterpart.FullName}, which has none.");

                continue;
            }

            if (!paired.TryAdd(number, field))
            {
                failures.Add($"{name} is field {number} of {counterpart.FullName}, already the counterpart of field {paired[number].FieldNumber}.");

                continue;
            }

            var theirs = $"{Descriptors.TypeOf(other)} {other.Name}";

            if (!SameType(field, other, messagePairs, enumPairs))
                failures.Add($"{name} does not have the type of field {number} of {counterpart.FullName}, {theirs}.");

            if (field.IsRepeated != other.IsRepeated || field.IsMap != other.IsMap)
                failures.Add($"{name} does not have the label of field {number} of {counterpart.FullName}, {theirs}.");

            if (Descriptors.IsOptional(field) != Descriptors.IsOptional(other))
                failures.Add($"{name} does not have the presence of field {number} of {counterpart.FullName}, {theirs}.");

            AuditOneof(field, other, name, counterpart, oneofs, claimed, failures);
        }

        var extra = counterpart.Fields.InDeclarationOrder().Count(field => !paired.ContainsKey(field.FieldNumber));

        if (extra > 0)
            notes.Add($"{message.FullName}: {extra} of the {counterpart.Fields.InDeclarationOrder().Count} fields of {counterpart.FullName} have no readable counterpart.");
    }

    private static void AuditOneof(
        FieldDescriptor field,
        FieldDescriptor other,
        string name,
        MessageDescriptor counterpart,
        Dictionary<OneofDescriptor, OneofDescriptor> oneofs,
        Dictionary<OneofDescriptor, OneofDescriptor> claimed,
        List<string> failures)
    {
        var mine = field.RealContainingOneof;
        var theirs = other.RealContainingOneof;

        if (mine is null || theirs is null)
        {
            if (mine is not null || theirs is not null)
                failures.Add($"{name} is {(mine is null ? "not " : string.Empty)}in a oneof, and field {other.FieldNumber} of {counterpart.FullName} is {(theirs is null ? "not" : "in one")}.");

            return;
        }

        if (oneofs.TryGetValue(mine, out var known) && known != theirs)
        {
            failures.Add($"{name}, of oneof {mine.Name}, is in another oneof of {counterpart.FullName} than the oneof's other fields.");

            return;
        }

        if (claimed.TryGetValue(theirs, out var owner) && owner != mine)
        {
            failures.Add($"{name}, of oneof {mine.Name}, is in the oneof of {counterpart.FullName} oneof {owner.Name}'s fields are in.");

            return;
        }

        oneofs[mine] = theirs;
        claimed[theirs] = mine;
    }

    private static bool SameType(FieldDescriptor field, FieldDescriptor other, Dictionary<string, MessagePair> messagePairs, Dictionary<string, EnumPair> enumPairs)
    {
        return field.FieldType switch
        {
            FieldType.Message or FieldType.Group when Descriptors.IsWellKnown(field.MessageType) => other.FieldType is FieldType.Message or FieldType.Group
                && other.MessageType.FullName == field.MessageType.FullName,
            FieldType.Message or FieldType.Group => other.FieldType is FieldType.Message or FieldType.Group
                && messagePairs.TryGetValue(field.MessageType.FullName, out var message)
                && message.Obfuscated == other.MessageType.FullName,
            FieldType.Enum => other.FieldType is FieldType.Enum
                && enumPairs.TryGetValue(field.EnumType.FullName, out var type)
                && type.Obfuscated == other.EnumType.FullName,
            _ => field.FieldType == other.FieldType,
        };
    }
}
