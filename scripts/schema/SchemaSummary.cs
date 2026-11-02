using Google.Protobuf.Reflection;
using FieldType = Google.Protobuf.Reflection.FieldDescriptorProto.Types.Type;

namespace D3B.Scripts.Schema;

/// <summary>Counts what a file descriptor holds, to describe a schema by its structure.</summary>
internal sealed class SchemaSummary
{
    public int Messages { get; private set; }

    public int NestedMessages { get; private set; }

    public int Enums { get; private set; }

    public int Fields { get; private set; }

    public int Repeated { get; private set; }

    public int Maps { get; private set; }

    public int Oneofs { get; private set; }

    public int OneofFields { get; private set; }

    public int Optional { get; private set; }

    /// <summary>The fields of each protobuf type, a list's or a map's values included, and a map's keys.</summary>
    public SortedDictionary<string, int> Types { get; } = new(StringComparer.Ordinal);

    public static SchemaSummary Of(FileDescriptorProto file)
    {
        var summary = new SchemaSummary { Enums = file.EnumType.Count };

        foreach (var message in file.MessageType)
            summary.Add(message, false);

        return summary;
    }

    public override string ToString()
    {
        return $"{Messages} messages ({NestedMessages} nested), {Enums} enums, {Fields} fields: {Repeated} lists, {Maps} maps, "
            + $"{OneofFields} in {Oneofs} oneofs, {Optional} optional";
    }

    private void Add(DescriptorProto message, bool nested)
    {
        var entries = message.NestedType.Where(type => type.Options?.MapEntry is true).ToDictionary(type => type.Name, StringComparer.Ordinal);

        Messages++;
        NestedMessages += nested ? 1 : 0;
        Enums += message.EnumType.Count;
        Oneofs += message.OneofDecl.Count - message.Field.Count(field => field.Proto3Optional);

        foreach (var field in message.Field)
        {
            Fields++;
            Optional += field.Proto3Optional ? 1 : 0;
            OneofFields += field.HasOneofIndex && !field.Proto3Optional ? 1 : 0;

            if (field.Type is FieldType.Message && entries.TryGetValue(field.TypeName[(field.TypeName.LastIndexOf('.') + 1)..], out var entry))
            {
                Maps++;
                Count(entry.Field[0]);
                Count(entry.Field[1]);

                continue;
            }

            Repeated += field.Label is FieldDescriptorProto.Types.Label.Repeated ? 1 : 0;
            Count(field);
        }

        foreach (var type in message.NestedType.Where(type => !entries.ContainsKey(type.Name)))
            Add(type, true);
    }

    private void Count(FieldDescriptorProto field)
    {
        var name = field.Type switch
        {
            FieldType.Message => "message",
            FieldType.Enum => "enum",
            _ => ProtoText.TypeOf(field),
        };

        Types[name] = Types.GetValueOrDefault(name) + 1;
    }
}
