using D3B.Protocol.Mapping;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Protocol.Tests.Mapping;

/// <summary>
/// A schema made from readable files as a build's is made from the client's: one file without a package per schema file
/// the readable packages are matched in, every message, enum, field, oneof and value renamed, and the order of the
/// top-level types reversed. Field and value numbers stay, as the client's obfuscation keeps them, and so do protobuf's
/// own types, such as <c>google.protobuf.Any</c>, whose file joins the schema when a file uses it.
/// </summary>
/// <remarks>
/// A map's entry is renamed as any nested message and keeps its option, so that the schema still holds a map, its key and
/// value paired and audited as a message's fields.
/// </remarks>
internal sealed class Obfuscation
{
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FileDescriptorProto> _files = new(StringComparer.Ordinal);
    private readonly List<FileDescriptor> _imports = [];
    private int _counter;

    /// <summary>Gets the obfuscated files, which a test may change before building the schema.</summary>
    public IReadOnlyCollection<FileDescriptorProto> Files => _files.Values;

    /// <param name="files">The readable files.</param>
    /// <param name="fileName">The schema file of a package <see cref="ReadableProtocol.Scopes"/> does not name.</param>
    public Obfuscation(IEnumerable<FileDescriptor> files, string fileName = ReadableProtocol.ConnectionSchemaFile)
    {
        var readable = files.ToList();
        var protos = readable.Select(file => file.ToProto()).ToList();

        foreach (var proto in protos)
        {
            foreach (var message in proto.MessageType)
                Name(message, Prefix(proto), ".");

            foreach (var type in proto.EnumType)
                _names[Prefix(proto) + type.Name] = "." + Next("t");
        }

        foreach (var proto in protos)
        {
            var name = ReadableProtocol.Scopes.GetValueOrDefault(proto.Package, fileName);

            if (!_files.TryGetValue(name, out var file))
                _files[name] = file = new FileDescriptorProto { Name = name, Syntax = "proto3" };

            file.MessageType.AddRange(proto.MessageType.Select(message => Rename(message, Prefix(proto))));
            file.EnumType.AddRange(proto.EnumType.Select(type => Rename(type, Prefix(proto) + type.Name)));
        }

        foreach (var file in _files.Values)
        {
            var messages = file.MessageType.Reverse().ToList();
            var enums = file.EnumType.Reverse().ToList();

            file.MessageType.Clear();
            file.MessageType.AddRange(messages);
            file.EnumType.Clear();
            file.EnumType.AddRange(enums);
        }

        // A file importing protobuf's own types imports them in the schema too, which then holds their files.
        foreach (var (proto, source) in protos.Zip(readable))
        {
            var file = _files[ReadableProtocol.Scopes.GetValueOrDefault(proto.Package, fileName)];

            foreach (var import in source.Dependencies.Where(dependency => dependency.Package == "google.protobuf"))
            {
                if (!file.Dependency.Contains(import.Name))
                    file.Dependency.Add(import.Name);

                if (!_imports.Contains(import))
                    _imports.Add(import);
            }
        }
    }

    private static IEnumerable<MessageDescriptor> Nested(MessageDescriptor message)
    {
        return message.NestedTypes.SelectMany(Nested).Prepend(message);
    }

    private static string Prefix(FileDescriptorProto proto)
    {
        return proto.Package.Length is 0 ? "." : "." + proto.Package + ".";
    }

    /// <summary>Gets the obfuscated full name of a readable message or enum, without the leading dot.</summary>
    public string Of(string readableFullName)
    {
        return _names["." + readableFullName][1..];
    }

    /// <summary>Gets an obfuscated message by the readable full name it was made from.</summary>
    public DescriptorProto Message(string readableFullName)
    {
        var path = Of(readableFullName).Split('.');
        var message = _files.Values.SelectMany(file => file.MessageType).Single(candidate => candidate.Name == path[0]);

        foreach (var name in path.Skip(1))
            message = message.NestedType.Single(candidate => candidate.Name == name);

        return message;
    }

    /// <summary>Gets an obfuscated enum by the readable full name it was made from.</summary>
    public EnumDescriptorProto Enum(string readableFullName)
    {
        var path = Of(readableFullName).Split('.');

        if (path.Length is 1)
            return _files.Values.SelectMany(file => file.EnumType).Single(candidate => candidate.Name == path[0]);

        var parent = Message(string.Join('.', readableFullName.Split('.')[..^1]));

        return parent.EnumType.Single(candidate => candidate.Name == path[^1]);
    }

    /// <summary>Gets the readable game's messages no readable field uses: those a build's pins name, the structure leaving them open (D43).</summary>
    public static IReadOnlyList<MessageDescriptor> GamePayloads()
    {
        var files = ReadableProtocol.Files.Where(file => file.Package == ReadableProtocol.GamePackage).ToList();
        var messages = files.SelectMany(file => file.MessageTypes).ToList();
        var used = files.SelectMany(file => file.MessageTypes.SelectMany(Nested))
            .SelectMany(message => message.Fields.InDeclarationOrder())
            .Where(field => field.FieldType is FieldType.Message)
            .Select(field => field.MessageType)
            .ToHashSet();

        return [.. messages.Where(message => !used.Contains(message))];
    }

    /// <summary>Pins readable messages to their obfuscated counterparts, as a build's pins do for what the structure leaves open.</summary>
    public PinnedPairs Pins(IEnumerable<MessageDescriptor> messages, string clientVersion = "3.7.12.12")
    {
        var pins = new PinnedPairs { ClientVersion = clientVersion };

        pins.Messages.AddRange(messages.Select(message => new MessagePin { Readable = message.FullName, Obfuscated = Of(message.FullName) }));

        return pins;
    }

    /// <summary>Gets the schema's bytes as they stand: protobuf's own files first, then the obfuscated ones.</summary>
    public byte[] SchemaBytes()
    {
        var set = new FileDescriptorSet();

        set.File.AddRange(_imports.Select(import => import.ToProto()));
        set.File.AddRange(_files.Values);

        return set.ToByteArray();
    }

    /// <summary>Builds the schema of the obfuscated files as they stand.</summary>
    public ProtocolSchema Schema()
    {
        return ProtocolSchema.Load(SchemaBytes());
    }

    private void Name(DescriptorProto message, string readablePrefix, string obfuscatedPrefix)
    {
        var full = readablePrefix + message.Name;
        var name = obfuscatedPrefix + Next("t");

        _names[full] = name;

        foreach (var nested in message.NestedType)
            Name(nested, full + ".", name + ".");

        foreach (var type in message.EnumType)
            _names[full + "." + type.Name] = name + "." + Next("t");
    }

    private DescriptorProto Rename(DescriptorProto message, string prefix)
    {
        var full = prefix + message.Name;
        var renamed = message.Clone();

        renamed.Name = _names[full].Split('.')[^1];
        renamed.NestedType.Clear();
        renamed.NestedType.AddRange(message.NestedType.Select(nested => Rename(nested, full + ".")));
        renamed.EnumType.Clear();
        renamed.EnumType.AddRange(message.EnumType.Select(type => Rename(type, full + "." + type.Name)));

        foreach (var field in renamed.Field)
        {
            field.Name = Next("f");
            field.ClearJsonName();

            if (field.HasTypeName && _names.TryGetValue(field.TypeName, out var name))
                field.TypeName = name;
        }

        for (var index = 0; index < renamed.OneofDecl.Count; index++)
        {
            var synthetic = renamed.Field.FirstOrDefault(field => field.Proto3Optional && field.OneofIndex == index);

            renamed.OneofDecl[index].Name = synthetic is null ? Next("o") : "_" + synthetic.Name;
        }

        return renamed;
    }

    private EnumDescriptorProto Rename(EnumDescriptorProto type, string full)
    {
        var renamed = type.Clone();

        renamed.Name = _names[full].Split('.')[^1];

        foreach (var value in renamed.Value)
            value.Name = Next("v");

        renamed.Options = null;

        return renamed;
    }

    private string Next(string prefix)
    {
        return $"{prefix}{++_counter:D4}";
    }
}
