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
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String && LooksLikeName(property.Name))
                {
                    messages.Add(Create(property.Value.GetString()!, property.Name));
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
