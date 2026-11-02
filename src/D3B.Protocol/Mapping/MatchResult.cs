using Google.Protobuf;

namespace D3B.Protocol.Mapping;

/// <summary>What matching a readable protocol with a build's schema found: the pairs, and what it could not settle.</summary>
public sealed class MatchResult
{
    /// <summary>Gets the readable messages paired, by full name.</summary>
    public IReadOnlyList<MessageMatch> Messages { get; }

    /// <summary>Gets the readable enums paired, by full name.</summary>
    public IReadOnlyList<EnumMatch> Enums { get; }

    /// <summary>Gets the full names of the readable messages and enums left without a counterpart.</summary>
    public IReadOnlyList<string> Unpaired { get; }

    /// <summary>Gets the evidence that contradicted other evidence; any fails the mapping.</summary>
    public IReadOnlyList<string> Conflicts { get; }

    internal MatchResult(IReadOnlyList<MessageMatch> messages, IReadOnlyList<EnumMatch> enums, IReadOnlyList<string> unpaired, IReadOnlyList<string> conflicts)
    {
        Messages = messages;
        Enums = enums;
        Unpaired = unpaired;
        Conflicts = conflicts;
    }

    /// <summary>Writes the pairs as a mapping of a client build.</summary>
    /// <param name="clientVersion">The build's version, as its version file names it.</param>
    /// <param name="schemaSha256">The SHA-256 of the build's schema.</param>
    /// <param name="readableDigest">The digest of the readable protocol matched.</param>
    /// <param name="toolVersion">The version of the tool that matched.</param>
    public ProtocolMapping ToMapping(string clientVersion, IReadOnlyList<byte> schemaSha256, IReadOnlyList<byte> readableDigest, string toolVersion)
    {
        ArgumentNullException.ThrowIfNull(schemaSha256);
        ArgumentNullException.ThrowIfNull(readableDigest);

        var mapping = new ProtocolMapping
        {
            FormatVersion = MappingFile.FormatVersion,
            ClientVersion = clientVersion,
            SchemaSha256 = ByteString.CopyFrom([.. schemaSha256]),
            ReadableDigest = ByteString.CopyFrom([.. readableDigest]),
            ToolVersion = toolVersion,
        };

        foreach (var match in Messages.OrderBy(match => match.Readable.FullName, StringComparer.Ordinal))
        {
            var pair = new MessagePair { Readable = match.Readable.FullName, Obfuscated = match.Obfuscated.FullName, Evidence = match.Evidence };

            pair.Fields.AddRange(match.Fields.OrderBy(field => field.Readable).Select(field => new NumberPair { Readable = field.Readable, Obfuscated = field.Obfuscated }));
            mapping.Messages.Add(pair);
        }

        foreach (var match in Enums.OrderBy(match => match.Readable.FullName, StringComparer.Ordinal))
        {
            var pair = new EnumPair { Readable = match.Readable.FullName, Obfuscated = match.Obfuscated.FullName, Evidence = match.Evidence };
            var numbers = match.Obfuscated.Values.Select(value => value.Number).ToHashSet();

            // Enum values travel as their numbers, which the obfuscation keeps, as it keeps field numbers (D43).
            pair.Values.AddRange(match.Readable.Values.Select(value => value.Number).Distinct().Order().Where(numbers.Contains)
                .Select(number => new NumberPair { Readable = number, Obfuscated = number }));
            mapping.Enums.Add(pair);
        }

        return mapping;
    }
}
