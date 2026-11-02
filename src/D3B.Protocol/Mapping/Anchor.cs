using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>A readable message and its obfuscated counterpart, measured in captures, with the fields the frames paired.</summary>
public sealed class Anchor
{
    /// <summary>Gets the readable message.</summary>
    public MessageDescriptor Readable { get; }

    /// <summary>Gets the obfuscated message.</summary>
    public MessageDescriptor Obfuscated { get; }

    /// <summary>Gets the fields the frames paired, as readable and obfuscated numbers.</summary>
    public IReadOnlyList<(int Readable, int Obfuscated)> Fields { get; }

    internal Anchor(MessageDescriptor readable, MessageDescriptor obfuscated, IReadOnlyList<(int Readable, int Obfuscated)> fields)
    {
        ArgumentNullException.ThrowIfNull(readable);
        ArgumentNullException.ThrowIfNull(obfuscated);
        ArgumentNullException.ThrowIfNull(fields);

        Readable = readable;
        Obfuscated = obfuscated;
        Fields = fields;
    }
}
