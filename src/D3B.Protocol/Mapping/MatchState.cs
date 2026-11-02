using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>The pairs found so far, one to one both ways, and what contradicted them.</summary>
/// <remarks>
/// Evidence comes strongest first, pins, then anchors, then fields, then scores, so the first pair a type gets stays; a
/// later one that disagrees is a conflict, which fails the mapping rather than being settled by a guess.
/// </remarks>
internal sealed class MatchState
{
    private readonly Dictionary<MessageDescriptor, MessageDescriptor> _messages = [];
    private readonly Dictionary<MessageDescriptor, MessageDescriptor> _obfuscatedMessages = [];
    private readonly Dictionary<MessageDescriptor, (MatchEvidence Evidence, double? Score)> _messageEvidence = [];
    private readonly Dictionary<EnumDescriptor, EnumDescriptor> _enums = [];
    private readonly Dictionary<EnumDescriptor, EnumDescriptor> _obfuscatedEnums = [];
    private readonly Dictionary<EnumDescriptor, (MatchEvidence Evidence, double? Score)> _enumEvidence = [];
    private readonly Dictionary<MessageDescriptor, SortedDictionary<int, int>> _fields = [];
    private readonly HashSet<MessageDescriptor> _propagated = [];
    private readonly List<string> _conflicts = [];
    private readonly HashSet<string> _conflictSet = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Conflicts => _conflicts;

    /// <summary>Tells whether two fields have the same shape: label, map or not, and kind of type.</summary>
    public static bool SameShape(FieldDescriptor readable, FieldDescriptor obfuscated)
    {
        return readable.IsRepeated == obfuscated.IsRepeated && readable.IsMap == obfuscated.IsMap && Kind(readable) == Kind(obfuscated);
    }

    private static FieldType Kind(FieldDescriptor field)
    {
        return field.FieldType is FieldType.Group ? FieldType.Message : field.FieldType;
    }

    private static string Name(MatchEvidence evidence)
    {
        return evidence switch
        {
            MatchEvidence.Pin => "a pin",
            MatchEvidence.Capture => "the captures",
            MatchEvidence.Field => "a field",
            MatchEvidence.Score => "its score",
            _ => "nothing",
        };
    }

    public MessageDescriptor? CounterpartOf(MessageDescriptor readable)
    {
        return _messages.GetValueOrDefault(readable);
    }

    public MessageDescriptor? ReadableOf(MessageDescriptor obfuscated)
    {
        return _obfuscatedMessages.GetValueOrDefault(obfuscated);
    }

    public EnumDescriptor? CounterpartOf(EnumDescriptor readable)
    {
        return _enums.GetValueOrDefault(readable);
    }

    public EnumDescriptor? ReadableOf(EnumDescriptor obfuscated)
    {
        return _obfuscatedEnums.GetValueOrDefault(obfuscated);
    }

    public (MatchEvidence Evidence, double? Score) EvidenceOf(MessageDescriptor readable)
    {
        return _messageEvidence[readable];
    }

    public (MatchEvidence Evidence, double? Score) EvidenceOf(EnumDescriptor readable)
    {
        return _enumEvidence[readable];
    }

    public IReadOnlyList<(int Readable, int Obfuscated)> FieldsOf(MessageDescriptor readable)
    {
        return _fields.TryGetValue(readable, out var fields) ? [.. fields.Select(field => (field.Key, field.Value))] : [];
    }

    /// <summary>Pairs two messages, unless either is already paired.</summary>
    /// <returns>Whether the pair is new.</returns>
    public bool Pair(MessageDescriptor readable, MessageDescriptor obfuscated, MatchEvidence evidence, double? score = null)
    {
        if (_messages.TryGetValue(readable, out var known))
        {
            if (known != obfuscated)
                Conflict($"{readable.FullName} is {known.FullName} by {Name(_messageEvidence[readable].Evidence)}, and {obfuscated.FullName} by {Name(evidence)}.");

            return false;
        }

        if (_obfuscatedMessages.TryGetValue(obfuscated, out var other))
        {
            Conflict($"{obfuscated.FullName} is {other.FullName} by {Name(_messageEvidence[other].Evidence)}, and {readable.FullName} by {Name(evidence)}.");

            return false;
        }

        _messages[readable] = obfuscated;
        _obfuscatedMessages[obfuscated] = readable;
        _messageEvidence[readable] = (evidence, score);

        return true;
    }

    /// <summary>Pairs two enums, unless either is already paired.</summary>
    /// <returns>Whether the pair is new.</returns>
    public bool Pair(EnumDescriptor readable, EnumDescriptor obfuscated, MatchEvidence evidence, double? score = null)
    {
        if (_enums.TryGetValue(readable, out var known))
        {
            if (known != obfuscated)
                Conflict($"{readable.FullName} is {known.FullName} by {Name(_enumEvidence[readable].Evidence)}, and {obfuscated.FullName} by {Name(evidence)}.");

            return false;
        }

        if (_obfuscatedEnums.TryGetValue(obfuscated, out var other))
        {
            Conflict($"{obfuscated.FullName} is {other.FullName} by {Name(_enumEvidence[other].Evidence)}, and {readable.FullName} by {Name(evidence)}.");

            return false;
        }

        _enums[readable] = obfuscated;
        _obfuscatedEnums[obfuscated] = readable;
        _enumEvidence[readable] = (evidence, score);

        return true;
    }

    /// <summary>States which field of a message's counterpart one of its fields is, as a pin or an anchor does.</summary>
    public void StateField(MessageDescriptor readable, int readableNumber, int obfuscatedNumber, MatchEvidence evidence)
    {
        if (!_fields.TryGetValue(readable, out var fields))
            _fields[readable] = fields = [];

        if (!fields.TryGetValue(readableNumber, out var known))
        {
            fields[readableNumber] = obfuscatedNumber;

            return;
        }

        if (known != obfuscatedNumber)
            Conflict($"Field {readableNumber} of {readable.FullName} is field {known} by one statement, and field {obfuscatedNumber} by {Name(evidence)}.");
    }

    /// <summary>Pairs the fields of every message pair not yet visited, and through them their types, until none is left.</summary>
    public void Propagate()
    {
        while (true)
        {
            var pending = _messages.Where(pair => !_propagated.Contains(pair.Key)).ToList();

            if (pending.Count is 0)
                return;

            foreach (var (readable, obfuscated) in pending)
            {
                _propagated.Add(readable);
                PropagateFields(readable, obfuscated);
            }
        }
    }

    public void Conflict(string conflict)
    {
        if (_conflictSet.Add(conflict))
            _conflicts.Add(conflict);
    }

    private void PropagateFields(MessageDescriptor readable, MessageDescriptor obfuscated)
    {
        if (!_fields.TryGetValue(readable, out var fields))
            _fields[readable] = fields = [];

        // A statement of a field the readable message lacks, as a mistyped pin makes, would leave the field it meant to
        // pair by its number, perhaps with a field of the same shape that is not its counterpart.
        foreach (var (number, stated) in fields.Where(field => readable.FindFieldByNumber(field.Key) is null))
            Conflict($"Field {number} of {readable.FullName} is stated as field {stated} of {obfuscated.FullName}, but {readable.FullName} has no field {number}.");

        foreach (var group in fields.GroupBy(field => field.Value).Where(group => group.Count() > 1))
            Conflict($"Fields {string.Join(" and ", group.Select(field => field.Key))} of {readable.FullName} are stated as one field, {group.Key}, of {obfuscated.FullName}.");

        var used = new HashSet<int>(fields.Values);

        foreach (var field in readable.Fields.InFieldNumberOrder())
        {
            FieldDescriptor? counterpart;

            if (fields.TryGetValue(field.FieldNumber, out var stated))
            {
                counterpart = obfuscated.FindFieldByNumber(stated);

                if (counterpart is null)
                {
                    Conflict($"Field {field.FieldNumber} of {readable.FullName} is stated as field {stated} of {obfuscated.FullName}, which has none.");

                    continue;
                }
            }
            else
            {
                // The obfuscation keeps field numbers (D43): a field is the counterpart's field of its number, when that one
                // has its shape and no other field of the message took it.
                counterpart = obfuscated.FindFieldByNumber(field.FieldNumber);

                if (counterpart is null || used.Contains(counterpart.FieldNumber) || !SameShape(field, counterpart))
                    continue;

                fields[field.FieldNumber] = counterpart.FieldNumber;
                used.Add(counterpart.FieldNumber);
            }

            if (!SameShape(field, counterpart))
                continue;

            if (field.FieldType is FieldType.Enum)
            {
                Pair(field.EnumType, counterpart.EnumType, MatchEvidence.Field);
            }
            else if (field.FieldType is FieldType.Message or FieldType.Group)
            {
                if (!Descriptors.IsWellKnown(field.MessageType) && !Descriptors.IsWellKnown(counterpart.MessageType))
                    Pair(field.MessageType, counterpart.MessageType, MatchEvidence.Field);
                else if (field.MessageType.FullName != counterpart.MessageType.FullName)
                    Conflict($"Field {field.FieldNumber} of {readable.FullName} is {field.MessageType.FullName}, and its counterpart, field {counterpart.FieldNumber} of {obfuscated.FullName}, {counterpart.MessageType.FullName}.");
            }
        }
    }
}
