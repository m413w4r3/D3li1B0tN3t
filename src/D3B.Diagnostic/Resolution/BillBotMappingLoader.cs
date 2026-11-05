using System.Text.Json;

namespace D3B.Diagnostic.Resolution;

/// <summary>Loads the read-only BillBot semantic-name-to-token reference mapping.</summary>
public static class BillBotMappingLoader
{
    /// <summary>Loads <c>game_mappings_detailed.json</c>; its source build is deliberately unknown.</summary>
    public static ReferenceCorpus Load(string path)
    {
        return LoadJson(File.ReadAllText(path));
    }

    /// <summary>Parses the BillBot semantic-name-to-obfuscated-token mapping.</summary>
    public static ReferenceCorpus LoadJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        List<ReferenceMessage> messages = [];
        Collect(document.RootElement, messages);
        return new ReferenceCorpus
        {
            Source = "billbot:game_mappings_detailed.json",
            Build = "other/unknown",
            BuildExact = false,
            Messages = messages
                .DistinctBy(message => (message.Token, message.Name))
                .OrderBy(message => message.Token, StringComparer.Ordinal)
                .ThenBy(message => message.Name, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static void Collect(JsonElement element, List<ReferenceMessage> messages)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            string? name = GetProperty(element, "name", "semantic", "readable", "message", "fullName");
            string? token = GetProperty(element, "token", "obfuscated", "id", "class");
            if (name is not null && token is not null)
            {
                messages.Add(Create(token, name));
                return;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object
                    && TryCreateNamespaceEntry(property.Name, property.Value, out ReferenceMessage? reference)
                    && reference is not null)
                {
                    messages.Add(reference);
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.String && LooksLikeName(property.Name))
                {
                    messages.Add(Create(property.Value.GetString()!, property.Name));
                    continue;
                }

                Collect(property.Value, messages);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                Collect(child, messages);
            }
        }
    }

    private static bool TryCreateNamespaceEntry(string propertyName, JsonElement element, out ReferenceMessage? reference)
    {
        string? obfuscatedName = GetProperty(element, "full_obf_msg_namespace", "obf_msg_namespace");
        string? token = obfuscatedName is null ? null : LeafToken(obfuscatedName);
        if (token is null)
        {
            reference = null;
            return false;
        }

        string? semanticName = GetProperty(element, "full_non_obf_msg_namespace");
        reference = Create(token, string.IsNullOrWhiteSpace(semanticName) ? propertyName : semanticName);
        return true;
    }

    private static string? LeafToken(string namespaceName)
    {
        string leaf = namespaceName[(namespaceName.LastIndexOf('.') + 1)..].Replace("`", string.Empty, StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(leaf) ? null : leaf;
    }

    private static ReferenceMessage Create(string token, string name)
    {
        return new ReferenceMessage
        {
            Token = token,
            Name = name,
            Source = "billbot:game_mappings_detailed.json",
            Build = "other/unknown",
            BuildExact = false
        };
    }

    private static string? GetProperty(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static bool LooksLikeName(string value)
    {
        return value.Contains('.', StringComparison.Ordinal) || value.EndsWith("Request", StringComparison.Ordinal) || value.EndsWith("Event", StringComparison.Ordinal);
    }
}
