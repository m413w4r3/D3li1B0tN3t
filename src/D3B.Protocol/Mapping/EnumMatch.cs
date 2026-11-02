using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>A readable enum paired with its obfuscated counterpart, and how.</summary>
public sealed class EnumMatch
{
    /// <summary>Gets the readable enum.</summary>
    public EnumDescriptor Readable { get; }

    /// <summary>Gets its obfuscated counterpart.</summary>
    public EnumDescriptor Obfuscated { get; }

    /// <summary>Gets what paired them.</summary>
    public MatchEvidence Evidence { get; }

    /// <summary>Gets the structural score that paired them, when it was a score.</summary>
    public double? Score { get; }

    internal EnumMatch(EnumDescriptor readable, EnumDescriptor obfuscated, MatchEvidence evidence, double? score)
    {
        Readable = readable;
        Obfuscated = obfuscated;
        Evidence = evidence;
        Score = score;
    }
}
