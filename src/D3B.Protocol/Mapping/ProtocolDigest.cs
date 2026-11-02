using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>The digest of a protocol's contract: what a mapping of it depends on, and nothing else.</summary>
/// <remarks>
/// It covers every message and enum by full name, every field's number, type, label, presence and oneof, and every enum
/// value's number. Field, value and oneof names are left out, and so are comments: renaming them changes neither the
/// wire nor what a mapping pairs.
/// </remarks>
public static class ProtocolDigest
{
    /// <summary>Computes the SHA-256 digest of the contract of a set of files.</summary>
    public static byte[] Of(IEnumerable<FileDescriptor> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var list = files.ToList();
        var text = new StringBuilder();

        foreach (var message in list.SelectMany(file => Descriptors.Messages(file.MessageTypes)).OrderBy(message => message.FullName, StringComparer.Ordinal))
        {
            text.Append(CultureInfo.InvariantCulture, $"message {message.FullName}\n");

            foreach (var field in message.Fields.InFieldNumberOrder())
            {
                var type = field.FieldType switch
                {
                    FieldType.Message or FieldType.Group => field.MessageType.FullName,
                    FieldType.Enum => field.EnumType.FullName,
                    _ => field.FieldType.ToString(),
                };
                var label = field.IsRepeated ? "repeated" : Descriptors.IsOptional(field) ? "optional" : "single";
                var oneof = field.RealContainingOneof?.Index.ToString(CultureInfo.InvariantCulture) ?? "-";

                text.Append(CultureInfo.InvariantCulture, $"  field {field.FieldNumber} {field.FieldType} {type} {label} {oneof}\n");
            }
        }

        foreach (var type in list.SelectMany(Descriptors.Enums).OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            text.Append(CultureInfo.InvariantCulture, $"enum {type.FullName}\n");

            foreach (var number in type.Values.Select(value => value.Number).Distinct().Order())
                text.Append(CultureInfo.InvariantCulture, $"  value {number}\n");
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
    }
}
