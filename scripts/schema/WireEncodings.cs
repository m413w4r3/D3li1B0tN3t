using Cpp2IL.Core.Model.Contexts;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Schema;

/// <summary>How a message's code writes its fields, read from its machine code and checked against its metadata.</summary>
/// <remarks>
/// <para>
/// A field of one value is written by <c>InternalWriteTo</c> as <c>WriteRawTag(tag)</c>, then <c>Write&lt;Name&gt;</c>:
/// the tag holds the field's number and wire type, the method its protobuf type. A list or a map is written through a
/// codec its static constructor builds: <c>FieldCodec.For&lt;Name&gt;(tag)</c> for a list of scalars, and for a map, one
/// codec for its keys, tag 8 and up, and one for its values, tag 16 and up. Codecs of messages and enums are generic:
/// their calls stay unresolved, and the metadata gives their type.
/// </para>
/// <para>
/// Every field of one value must be written exactly once, with a method fitting its C# type, and every codec read must
/// belong to a field. A map's key or value is matched to its map by its C# type, since its tag does not name the map: an
/// encoding other than the C# type's default would be ambiguous, and fails the reading.
/// </para>
/// </remarks>
internal sealed class WireEncodings
{
    private const string WriteContextName = "Google.Protobuf.WriteContext";
    private const string FieldCodecName = "Google.Protobuf.FieldCodec";

    private static readonly string[] _genericCodecs = ["ForMessage", "ForEnum", "ForGroup", "ForClassWrapper", "ForStructWrapper"];

    private readonly Dictionary<int, FieldEncoding> _values = [];
    private readonly HashSet<int> _unpacked = [];
    private readonly Dictionary<int, (FieldEncoding Key, FieldEncoding? Value)> _maps = [];

    private WireEncodings()
    {
    }

    /// <summary>Reads how a message writes its fields, and checks it against its layout.</summary>
    /// <exception cref="InvalidDataException">The code and the metadata disagree, or the code cannot be read.</exception>
    public static WireEncodings Read(TypeAnalysisContext message, MessageLayout layout, ClientPlatform platform)
    {
        var writer = message.Methods.SingleOrDefault(method => method.Parameters.Count is 1 && method.Parameters[0].ParameterType.FullName == WriteContextName + "&")
            ?? throw new InvalidDataException($"{message.FullName} has no InternalWriteTo.");
        var initializer = message.Methods.SingleOrDefault(method => method.Name == ".cctor")
            ?? throw new InvalidDataException($"{message.FullName} has no static constructor.");
        var encodings = new WireEncodings();

        encodings.ReadValues(message, layout, Writes(message, layout, IsilCalls.Read(writer, platform)));
        encodings.ReadCodecs(message, layout, Codecs(message, IsilCalls.Read(initializer, platform)));

        return encodings;
    }

    private static List<(int Number, int WireType, string Name)> Writes(TypeAnalysisContext message, MessageLayout layout, List<IsilCall> calls)
    {
        var tags = new List<(int Number, int WireType)>();
        var writers = new List<string>();

        foreach (var call in calls.Where(call => call.Calls(WriteContextName)))
        {
            var name = call.Target!.Name;

            if (name == "WriteRawTag")
            {
                // rcx holds the context; the tag's bytes, one to five as a varint, come after it.
                var count = call.Target.Parameters.Count;
                var bytes = call.Arguments.Skip(1).Take(count).ToList();

                if (count is < 1 or > 3 || bytes.Any(value => value is null))
                    throw new InvalidDataException($"{message.FullName}: a tag written as {count} bytes cannot be read.");

                long tag = 0;

                for (var index = 0; index < count; index++)
                    tag |= (bytes[index]!.Value & 0x7F) << (7 * index);

                tags.Add(((int)(tag >> 3), (int)(tag & 7)));
            }
            else
            {
                writers.Add(name["Write".Length..]);
            }
        }

        var singular = layout.Fields.Where(field => field.Kind is FieldKind.Singular).ToList();

        if (tags.Count != singular.Count || writers.Count != singular.Count)
            throw new InvalidDataException($"{message.FullName} has {tags.Count} field tags and {writers.Count} field writers, where its metadata declares {singular.Count} fields of one value.");

        var tagsByNumber = tags.GroupBy(tag => tag.Number).ToDictionary(group => group.Key, group => group.ToList());
        var available = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var name in writers)
        {
            if (FieldEncoding.Find(name) is null)
                throw new InvalidDataException($"{message.FullName} calls Write{name}, which this tool does not know.");

            available[name] = available.GetValueOrDefault(name) + 1;
        }

        var candidates = new List<(int Index, int Number, int WireType, string[] Names)>();

        for (var index = 0; index < singular.Count; index++)
        {
            var field = singular[index];

            if (!tagsByNumber.TryGetValue(field.Number, out var fieldTags) || fieldTags.Count is not 1)
                throw new InvalidDataException($"{message.FullName}: field {field.Number} has {fieldTags?.Count ?? 0} write tags.");

            var tag = fieldTags[0];
            var names = available.Keys
                .Where(name =>
                {
                    var encoding = FieldEncoding.Find(name)!;

                    return encoding.WireType == tag.WireType && encoding.Fits(field.PropertyType);
                })
                .ToArray();

            if (names.Length is 0)
                throw new InvalidDataException($"{message.FullName}: field {field.Number}, of type {field.PropertyType.FullName}, has no matching writer for wire type {tag.WireType}.");

            candidates.Add((index, field.Number, tag.WireType, names));
        }

        var orderedCandidates = candidates.OrderBy(candidate => candidate.Names.Length).ToList();
        var selected = new string[singular.Count];
        string[]? uniqueSelection = null;
        var solutionCount = 0;

        void Match(int index)
        {
            if (solutionCount is > 1)
                return;

            if (index == orderedCandidates.Count)
            {
                var selection = (string[])selected.Clone();

                if (uniqueSelection is null)
                {
                    uniqueSelection = selection;
                    solutionCount = 1;
                }
                else if (!uniqueSelection.SequenceEqual(selection, StringComparer.Ordinal))
                {
                    solutionCount = 2;
                }

                return;
            }

            var candidate = orderedCandidates[index];

            foreach (var name in candidate.Names)
            {
                if (available[name] is 0)
                    continue;

                available[name]--;
                selected[candidate.Index] = name;
                Match(index + 1);
                available[name]++;

                if (solutionCount is > 1)
                    return;
            }
        }

        Match(0);

        if (solutionCount is 0)
            throw new InvalidDataException($"{message.FullName} field tags and writer methods cannot be matched to its fields.");

        if (solutionCount is > 1)
            throw new InvalidDataException($"{message.FullName} field tags and writer methods cannot be paired unambiguously by type and wire type.");

        return [.. candidates.Select(candidate => (candidate.Number, candidate.WireType, uniqueSelection![candidate.Index]))];
    }

    private static List<(int Number, int WireType, string Name)> Codecs(TypeAnalysisContext message, List<IsilCall> calls)
    {
        var codecs = new List<(int Number, int WireType, string Name)>();

        foreach (var call in calls.Where(call => call.Calls(FieldCodecName)))
        {
            var name = call.Target!.Name;

            if (_genericCodecs.Contains(name))
                continue;

            if (FieldEncoding.Find(name["For".Length..]) is null)
                throw new InvalidDataException($"{message.FullName} builds a codec with FieldCodec.{name}, which this tool does not know.");

            var tag = call.Arguments[0] ?? throw new InvalidDataException($"{message.FullName}: the tag of FieldCodec.{name} cannot be read.");

            codecs.Add(((int)((tag & 0xFFFFFFFF) >> 3), (int)(tag & 7), name["For".Length..]));
        }

        return codecs;
    }

    private static int? Take(List<(int Number, int WireType, string Name)> codecs, bool[] used, int number, TypeAnalysisContext type, bool defaultOnly, int? wireType = null)
    {
        for (var index = 0; index < codecs.Count; index++)
        {
            var encoding = FieldEncoding.Find(codecs[index].Name)!;

            if (used[index] || codecs[index].Number != number || !encoding.Fits(type) || (wireType is not null && codecs[index].WireType != wireType))
                continue;

            if (defaultOnly && (encoding != FieldEncoding.DefaultOf(type) || codecs[index].WireType != encoding.WireType))
                continue;

            used[index] = true;

            return index;
        }

        return null;
    }

    /// <summary>The encoding of a field of one value, or of a list's scalars.</summary>
    /// <returns>The encoding, or null for a list of messages or enums, which the metadata types.</returns>
    public FieldEncoding? ValueOf(int number)
    {
        return _values.GetValueOrDefault(number);
    }

    /// <summary>Tells whether a list of scalars is written unpacked, against proto3's default.</summary>
    public bool IsUnpacked(int number)
    {
        return _unpacked.Contains(number);
    }

    /// <summary>The encodings of a map's key and, for a scalar value, of its value.</summary>
    public (FieldEncoding Key, FieldEncoding? Value) MapOf(int number)
    {
        return _maps[number];
    }

    private void ReadValues(TypeAnalysisContext message, MessageLayout layout, List<(int Number, int WireType, string Name)> writes)
    {
        var singular = layout.Fields.Where(field => field.Kind is FieldKind.Singular).ToList();

        foreach (var field in singular)
        {
            var written = writes.Where(write => write.Number == field.Number).ToList();

            if (written.Count is not 1)
                throw new InvalidDataException($"{message.FullName}: field {field.Number} is written {written.Count} times.");

            var (_, wireType, name) = written[0];
            var encoding = FieldEncoding.Find(name)
                ?? throw new InvalidDataException($"{message.FullName}: field {field.Number} is written with Write{name}, which this tool does not know.");

            if (encoding.WireType != wireType || !encoding.Fits(field.PropertyType))
                throw new InvalidDataException($"{message.FullName}: field {field.Number}, of type {field.PropertyType.FullName}, is written with Write{name} and wire type {wireType}.");

            _values[field.Number] = encoding;
        }

        if (writes.Count != singular.Count)
            throw new InvalidDataException($"{message.FullName} writes {writes.Count} fields of one value, where its metadata declares {singular.Count}.");
    }

    private void ReadCodecs(TypeAnalysisContext message, MessageLayout layout, List<(int Number, int WireType, string Name)> codecs)
    {
        var used = new bool[codecs.Count];

        foreach (var field in layout.Fields.Where(field => field.Kind is FieldKind.Repeated))
        {
            var element = field.ValueTypes[0];

            if (ProtocolTypes.IsMessage(element) || ProtocolTypes.IsAny(element) || element.IsEnumType)
                continue;

            // A packed list's tag has wire type 2, which no map's key or value has: it is looked for first, so that a map's
            // codec of the same number is not taken for an unpacked list.
            var index = Take(codecs, used, field.Number, element, defaultOnly: false, wireType: 2)
                ?? Take(codecs, used, field.Number, element, defaultOnly: false)
                ?? throw new InvalidDataException($"{message.FullName}: no codec writes list {field.Number}.");
            var encoding = FieldEncoding.Find(codecs[index].Name)!;
            var wireType = codecs[index].WireType;

            if (encoding.IsPackable && wireType == encoding.WireType)
                _unpacked.Add(field.Number);
            else if (wireType is not 2)
                throw new InvalidDataException($"{message.FullName}: list {field.Number} is written with wire type {wireType}.");

            _values[field.Number] = encoding;
        }

        foreach (var field in layout.Fields.Where(field => field.Kind is FieldKind.Map))
        {
            var key = Take(codecs, used, 1, field.ValueTypes[0], defaultOnly: true)
                ?? throw new InvalidDataException($"{message.FullName}: no codec writes the keys of map {field.Number} with the default encoding of {field.ValueTypes[0].FullName}.");
            var valueType = field.ValueTypes[1];
            var value = ProtocolTypes.IsMessage(valueType) || ProtocolTypes.IsAny(valueType) || valueType.IsEnumType
                ? null
                : FieldEncoding.Find(codecs[Take(codecs, used, 2, valueType, defaultOnly: true)
                    ?? throw new InvalidDataException($"{message.FullName}: no codec writes the values of map {field.Number} with the default encoding of {valueType.FullName}.")].Name);

            _maps[field.Number] = (FieldEncoding.Find(codecs[key].Name)!, value);
        }

        var unused = Array.IndexOf(used, false);

        if (unused >= 0)
            throw new InvalidDataException($"{message.FullName} builds codec For{codecs[unused].Name} for field {codecs[unused].Number}, which is none of its fields.");
    }
}
