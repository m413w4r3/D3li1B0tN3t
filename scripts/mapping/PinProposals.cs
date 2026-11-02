using D3B.Protocol.Mapping;
using D3B.Protocol.Readable;
using Google.Protobuf.Reflection;

namespace D3B.Scripts.Mapping;

/// <summary>
/// Proposes, for each readable message left unpaired, the build's messages it fits that are not paired yet, those the
/// captures carry first, with the side carrying them: what the maintainer reads to pin a pair (D43). It never pins
/// anything itself: two messages of one shape are told apart by what the player did, which only the maintainer knows.
/// </summary>
/// <remarks>
/// A message fits a readable one when it has each readable field under its number with its shape, a type already paired
/// being the pair's, a type not paired yet fitting in turn, a few levels down. A partial readable message may leave
/// fields of its counterpart out; any other must have them all.
/// </remarks>
internal static class PinProposals
{
    private const int MaxDepth = 4;
    private const int Shown = 6;

    public static IEnumerable<string> For(MatchResult result, ProtocolSchema schema, IReadOnlyList<CapturedPayload> payloads)
    {
        var counterparts = result.Messages.ToDictionary(match => match.Readable, match => match.Obfuscated);
        var enums = result.Enums.ToDictionary(match => match.Readable, match => match.Obfuscated);
        var paired = counterparts.Values.ToHashSet();
        var carried = payloads.ToDictionary(payload => payload.Message);
        var readable = ReadableProtocol.Files.SelectMany(file => file.MessageTypes.SelectMany(Nested)).ToDictionary(message => message.FullName, StringComparer.Ordinal);

        foreach (var name in result.Unpaired.Where(readable.ContainsKey))
        {
            var message = readable[name];
            var file = schema.FindFile(ReadableProtocol.Scopes.GetValueOrDefault(message.File.Package, string.Empty));

            if (file is null)
                continue;

            var candidates = file.MessageTypes.SelectMany(Nested)
                .Where(candidate => !paired.Contains(candidate) && Fits(message, candidate, counterparts, enums, 0))
                .OrderByDescending(carried.ContainsKey)
                .ToList();

            if (candidates.Count is 0)
            {
                yield return $"{name}: no message of {file.Name} left unpaired fits it";

                continue;
            }

            var shown = candidates.Take(Shown).Select(candidate => carried.TryGetValue(candidate, out var payload)
                ? $".{candidate.FullName} (client {payload.FromClient}, server {payload.FromServer})"
                : $".{candidate.FullName}");
            var others = candidates.Count > Shown ? $", and {candidates.Count - Shown} more" : string.Empty;

            yield return $"{name}: {candidates.Count} fit it, {candidates.Count(carried.ContainsKey)} carried by the captures: {string.Join(", ", shown)}{others}";
        }
    }

    private static IEnumerable<MessageDescriptor> Nested(MessageDescriptor message)
    {
        return message.NestedTypes.SelectMany(Nested).Prepend(message);
    }

    private static bool Fits(MessageDescriptor readable, MessageDescriptor obfuscated, Dictionary<MessageDescriptor, MessageDescriptor> counterparts, Dictionary<EnumDescriptor, EnumDescriptor> enums, int depth)
    {
        var partial = readable.GetOptions()?.GetExtension(OptionsExtensions.Partial) is true;

        if (!partial && readable.Fields.InDeclarationOrder().Count != obfuscated.Fields.InDeclarationOrder().Count)
            return false;

        foreach (var field in readable.Fields.InDeclarationOrder())
        {
            var other = obfuscated.FindFieldByNumber(field.FieldNumber);

            if (other is null
                || field.IsRepeated != other.IsRepeated
                || field.IsMap != other.IsMap
                || (field.RealContainingOneof is null) != (other.RealContainingOneof is null)
                || (field.ContainingOneof is { IsSynthetic: true }) != (other.ContainingOneof is { IsSynthetic: true })
                || field.FieldType != other.FieldType)
            {
                return false;
            }

            if (field.FieldType is FieldType.Message)
            {
                if (field.MessageType.File.Package == "google.protobuf" || other.MessageType.File.Package == "google.protobuf")
                {
                    if (field.MessageType.FullName != other.MessageType.FullName)
                        return false;
                }
                else if (counterparts.TryGetValue(field.MessageType, out var counterpart))
                {
                    if (counterpart != other.MessageType)
                        return false;
                }
                else if (depth < MaxDepth && !Fits(field.MessageType, other.MessageType, counterparts, enums, depth + 1))
                {
                    return false;
                }
            }
            else if (field.FieldType is FieldType.Enum)
            {
                if (enums.TryGetValue(field.EnumType, out var type))
                {
                    if (type != other.EnumType)
                        return false;
                }
                else if (!field.EnumType.Values.All(value => other.EnumType.FindValueByNumber(value.Number) is not null))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
