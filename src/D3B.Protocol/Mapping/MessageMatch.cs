using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>A readable message paired with its obfuscated counterpart, how, and which fields pair.</summary>
public sealed class MessageMatch
{
    /// <summary>Gets the readable message.</summary>
    public MessageDescriptor Readable { get; }

    /// <summary>Gets its obfuscated counterpart.</summary>
    public MessageDescriptor Obfuscated { get; }

    /// <summary>Gets what paired them.</summary>
    public MatchEvidence Evidence { get; }

    /// <summary>Gets the structural score that paired them, when it was a score.</summary>
    public double? Score { get; }

    /// <summary>Gets the fields paired, as readable and obfuscated numbers, by readable number.</summary>
    public IReadOnlyList<(int Readable, int Obfuscated)> Fields { get; }

    internal MessageMatch(MessageDescriptor readable, MessageDescriptor obfuscated, MatchEvidence evidence, double? score, IReadOnlyList<(int Readable, int Obfuscated)> fields)
    {
        Readable = readable;
        Obfuscated = obfuscated;
        Evidence = evidence;
        Score = score;
        Fields = fields;
    }
}
