using System.Text.Json;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Resolution;

/// <summary>Reads the resolver's documented compact JSON reference corpus shape.</summary>
public static class ReferenceCorpusJsonLoader
{
    /// <summary>Loads a normalized JSON reference corpus from a file.</summary>
    public static ReferenceCorpus LoadFile(string path)
    {
        return Load(File.ReadAllText(path));
    }

    /// <summary>
    /// Loads JSON shaped as <c>{"source":"...","build":"...","buildExact":false,"messages":[{"token":"...","name":"...","fields":[{"number":1,"kind":"INT32","repeated":false,"messageType":null}]}]}</c>.
    /// Message-level source/build/buildExact override corpus-level values.
    /// </summary>
    public static ReferenceCorpus Load(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string source = GetString(root, "source", "normalized-json");
        string build = GetString(root, "build", "other/unknown");
        bool buildExact = GetBoolean(root, "buildExact", false);
        List<ReferenceMessage> messages = [];

        if (root.TryGetProperty("messages", out JsonElement messagesElement) && messagesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in messagesElement.EnumerateArray())
            {
                List<ReferenceField> fields = [];
                if (item.TryGetProperty("fields", out JsonElement fieldsElement) && fieldsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement field in fieldsElement.EnumerateArray())
                    {
                        fields.Add(new ReferenceField
                        {
                            Number = field.GetProperty("number").GetInt32(),
                            Kind = ParseKind(field.GetProperty("kind").GetString()),
                            Repeated = GetBoolean(field, "repeated", false),
                            MessageType = GetNullableString(field, "messageType")
                        });
                    }
                }

                messages.Add(new ReferenceMessage
                {
                    Token = GetString(item, "token", string.Empty),
                    Name = GetString(item, "name", string.Empty),
                    Source = GetString(item, "source", source),
                    Build = GetString(item, "build", build),
                    BuildExact = GetBoolean(item, "buildExact", buildExact),
                    Fields = fields.OrderBy(field => field.Number).ToArray()
                });
            }
        }

        return new ReferenceCorpus
        {
            Source = source,
            Build = build,
            BuildExact = buildExact,
            Messages = messages
        };
    }

    private static DiagnosticFieldKind ParseKind(string? kind)
    {
        if (Enum.TryParse(kind, true, out DiagnosticFieldKind value))
        {
            return value;
        }

        foreach (DiagnosticFieldKind candidate in Enum.GetValues<DiagnosticFieldKind>())
        {
            if (string.Equals(candidate.ToString(), kind, StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetKindName(candidate), kind, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new InvalidDataException("Unknown reference field kind.");
    }

    private static string GetKindName(DiagnosticFieldKind kind)
    {
        return kind switch
        {
            DiagnosticFieldKind.DoublePrecision => "DOUBLE",
            DiagnosticFieldKind.SinglePrecision => "FLOAT",
            DiagnosticFieldKind.SignedInteger64 => "INT64",
            DiagnosticFieldKind.UnsignedInteger64 => "UINT64",
            DiagnosticFieldKind.SignedInteger32 => "INT32",
            DiagnosticFieldKind.Fixed64 => "FIXED64",
            DiagnosticFieldKind.Fixed32 => "FIXED32",
            DiagnosticFieldKind.Boolean => "BOOL",
            DiagnosticFieldKind.Utf8String => "STRING",
            DiagnosticFieldKind.Group => "GROUP",
            DiagnosticFieldKind.Message => "MESSAGE",
            DiagnosticFieldKind.Bytes => "BYTES",
            DiagnosticFieldKind.UnsignedInteger32 => "UINT32",
            DiagnosticFieldKind.Enumeration => "ENUM",
            DiagnosticFieldKind.SignedFixed32 => "SFIXED32",
            DiagnosticFieldKind.SignedFixed64 => "SFIXED64",
            DiagnosticFieldKind.SignedZigZag32 => "SINT32",
            DiagnosticFieldKind.SignedZigZag64 => "SINT64",
            _ => throw new InvalidDataException("Unknown reference field kind.")
        };
    }

    private static string GetString(JsonElement element, string property, string fallback)
    {
        return GetNullableString(element, property) ?? fallback;
    }

    private static string? GetNullableString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static bool GetBoolean(JsonElement element, string property, bool fallback)
    {
        if (element.TryGetProperty(property, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        return fallback;
    }
}
