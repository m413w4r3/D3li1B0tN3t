using System.Security.Cryptography;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>A client build's obfuscated schema, the <c>FileDescriptorSet</c> <c>scripts/schema.cs</c> writes (D39), loaded.</summary>
public sealed class ProtocolSchema
{
    private readonly Dictionary<string, MessageDescriptor> _messages;
    private readonly Dictionary<string, EnumDescriptor> _enums;

    /// <summary>Gets the schema's files, linked.</summary>
    public IReadOnlyList<FileDescriptor> Files { get; }

    /// <summary>Gets the SHA-256 of the schema's bytes, which a mapping records.</summary>
    public IReadOnlyList<byte> Sha256 { get; }

    private ProtocolSchema(IReadOnlyList<FileDescriptor> files, byte[] sha256)
    {
        Files = files;
        Sha256 = sha256;
        _messages = files.SelectMany(file => Descriptors.Messages(file.MessageTypes)).ToDictionary(message => message.FullName, StringComparer.Ordinal);
        _enums = files.SelectMany(file => Descriptors.Enums(file)).ToDictionary(type => type.FullName, StringComparer.Ordinal);
    }

    /// <summary>Loads a schema from its bytes, linking every type name to its type.</summary>
    /// <exception cref="InvalidProtocolBufferException">The bytes are not a <c>FileDescriptorSet</c>.</exception>
    /// <exception cref="DescriptorValidationException">The descriptors do not link.</exception>
    public static ProtocolSchema Load(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var set = FileDescriptorSet.Parser.ParseFrom(bytes);
        var files = FileDescriptor.BuildFromByteStrings(set.File.Select(file => file.ToByteString()));

        return new ProtocolSchema(files, SHA256.HashData(bytes));
    }

    /// <summary>Loads the schema written to a file.</summary>
    /// <exception cref="InvalidProtocolBufferException">The file is not a <c>FileDescriptorSet</c>.</exception>
    /// <exception cref="DescriptorValidationException">The descriptors do not link.</exception>
    public static ProtocolSchema Load(string path)
    {
        return Load(File.ReadAllBytes(path));
    }

    /// <summary>Finds a message by its full name.</summary>
    public MessageDescriptor? FindMessage(string fullName)
    {
        return _messages.GetValueOrDefault(fullName);
    }

    /// <summary>Finds an enum by its full name.</summary>
    public EnumDescriptor? FindEnum(string fullName)
    {
        return _enums.GetValueOrDefault(fullName);
    }

    /// <summary>Gets a file of the schema by its name, such as <c>connection.proto</c>.</summary>
    public FileDescriptor? FindFile(string name)
    {
        return Files.FirstOrDefault(file => file.Name == name);
    }
}
