using System.Text.Json;

namespace D3B.Diagnostic.Contract;

/// <summary>Strict parsing and deterministic serialization helpers for protocol messages.</summary>
public static class DiagnosticJson
{
    /// <summary>Parses and validates an engine hello.</summary>
    public static DiagnosticEngineHello DeserializeHello(ReadOnlySpan<byte> json)
    {
        ValidateHello(json, engine: true);
        ValidateProtocolVersion(json);
        return Deserialize<DiagnosticEngineHello>(json);
    }

    /// <summary>Parses an analysis and rejects unsupported status or problem codes.</summary>
    public static DiagnosticAnalysis DeserializeAnalysis(ReadOnlySpan<byte> json)
    {
        ValidateAnalysis(json);
        ValidateCodes(json);
        return Deserialize<DiagnosticAnalysis>(json);
    }

    /// <summary>Parses a summary and validates its protocol version and enum keys.</summary>
    public static DiagnosticSummary DeserializeSummary(ReadOnlySpan<byte> json)
    {
        ValidateSummary(json);
        ValidateProtocolVersion(json);
        ValidateCodes(json);
        return Deserialize<DiagnosticSummary>(json);
    }

    /// <summary>Parses a redacted engine error and rejects unknown error codes.</summary>
    public static DiagnosticError DeserializeError(ReadOnlySpan<byte> json)
    {
        ValidateError(json);
        ValidateErrorCodes(json);
        return Deserialize<DiagnosticError>(json);
    }

    /// <summary>Parses and validates an in-band diagnostic schema.</summary>
    public static DiagnosticSchema DeserializeSchema(ReadOnlySpan<byte> json)
    {
        using JsonDocument document = ParseDocument(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("kind", out JsonElement kind) ||
            kind.ValueKind != JsonValueKind.String || kind.GetString() != "schema")
        {
            throw new DiagnosticProtocolException("The diagnostic schema line is invalid.");
        }

        ValidateSchemaShape(document.RootElement);

        DiagnosticSchema schema = Deserialize<DiagnosticSchema>(json);
        ValidateSchema(schema);
        return schema;
    }

    /// <summary>Parses a client hello and validates its protocol version.</summary>
    public static DiagnosticHello DeserializeClientHello(ReadOnlySpan<byte> json)
    {
        ValidateHello(json, engine: false);
        ValidateProtocolVersion(json);
        try
        {
            return JsonSerializer.Deserialize(json, DiagnosticJsonContext.Protocol.DiagnosticHello)
                ?? throw new DiagnosticProtocolException("The diagnostic message was empty.");
        }
        catch (DiagnosticProtocolException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new DiagnosticProtocolException("The diagnostic message was malformed.");
        }
    }

    /// <summary>Serializes a client hello as compact JSON.</summary>
    public static byte[] SerializeHello(DiagnosticHello value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, DiagnosticJsonContext.Protocol.DiagnosticHello);
    }

    /// <summary>Serializes a schema as compact JSON.</summary>
    public static byte[] SerializeSchema(DiagnosticSchema value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, DiagnosticJsonContext.Protocol.DiagnosticSchema);
    }

    /// <summary>Serializes a frame analysis as compact JSON.</summary>
    public static byte[] SerializeAnalysis(DiagnosticAnalysis value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, DiagnosticJsonContext.Protocol.DiagnosticAnalysis);
    }

    /// <summary>Serializes a replay summary as compact JSON.</summary>
    public static byte[] SerializeSummary(DiagnosticSummary value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, DiagnosticJsonContext.Protocol.DiagnosticSummary);
    }

    /// <summary>Serializes a redacted error as compact JSON.</summary>
    public static byte[] SerializeError(DiagnosticError value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, DiagnosticJsonContext.Protocol.DiagnosticError);
    }

    /// <summary>Serializes an end marker as compact JSON.</summary>
    public static byte[] SerializeEnd(DiagnosticEnd value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, DiagnosticJsonContext.Protocol.DiagnosticEnd);
    }

    private static T Deserialize<T>(ReadOnlySpan<byte> json)
    {
        try
        {
            T? value = JsonSerializer.Deserialize(json, GetTypeInfo<T>());
            return value ?? throw new DiagnosticProtocolException("The diagnostic message was empty.");
        }
        catch (DiagnosticProtocolException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new DiagnosticProtocolException("The diagnostic message was malformed or contained an unsupported value.");
        }
    }

    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> GetTypeInfo<T>()
    {
        if (typeof(T) == typeof(DiagnosticEngineHello))
        {
            return (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)(object)DiagnosticJsonContext.Protocol.DiagnosticEngineHello;
        }

        if (typeof(T) == typeof(DiagnosticAnalysis))
        {
            return (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)(object)DiagnosticJsonContext.Protocol.DiagnosticAnalysis;
        }

        if (typeof(T) == typeof(DiagnosticSummary))
        {
            return (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)(object)DiagnosticJsonContext.Protocol.DiagnosticSummary;
        }

        if (typeof(T) == typeof(DiagnosticSchema))
        {
            return (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)(object)DiagnosticJsonContext.Protocol.DiagnosticSchema;
        }

        return (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)(object)DiagnosticJsonContext.Protocol.DiagnosticError;
    }

    private static void ValidateProtocolVersion(ReadOnlySpan<byte> json)
    {
        using JsonDocument document = ParseDocument(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("protocolVersion", out JsonElement version)
            || !version.TryGetInt32(out int parsed)
            || parsed != DiagnosticProtocol.ProtocolVersion)
        {
            throw new DiagnosticProtocolException("The diagnostic protocol version is unsupported.");
        }
    }

    private static void ValidateCodes(ReadOnlySpan<byte> json)
    {
        using JsonDocument document = ParseDocument(json);
        Visit(document.RootElement);

        static void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.NameEquals("status") && property.Value.ValueKind == JsonValueKind.String
                        && !IsKnownStatus(property.Value.GetString()))
                    {
                        throw new DiagnosticProtocolException("The diagnostic status is unsupported.");
                    }

                    if (property.NameEquals("code") && property.Value.ValueKind == JsonValueKind.String
                        && !IsKnownProblemCode(property.Value.GetString()))
                    {
                        throw new DiagnosticProtocolException("The diagnostic problem code is unsupported.");
                    }

                    if (property.NameEquals("problems"))
                    {
                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement problem in property.Value.EnumerateArray())
                            {
                                if (problem.ValueKind == JsonValueKind.String && !IsKnownProblemCode(problem.GetString()))
                                {
                                    throw new DiagnosticProtocolException("The diagnostic problem code is unsupported.");
                                }

                                if (problem.ValueKind is not (JsonValueKind.String or JsonValueKind.Object))
                                {
                                    throw new DiagnosticProtocolException("The diagnostic problem is invalid.");
                                }
                            }
                        }
                        else if (property.Value.ValueKind == JsonValueKind.Object)
                        {
                            foreach (JsonProperty code in property.Value.EnumerateObject())
                            {
                                if (!IsKnownProblemCode(code.Name))
                                {
                                    throw new DiagnosticProtocolException("The diagnostic problem code is unsupported.");
                                }
                            }
                        }
                    }

                    if (property.NameEquals("statuses") && property.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty status in property.Value.EnumerateObject())
                        {
                            if (!IsKnownStatus(status.Name))
                            {
                                throw new DiagnosticProtocolException("The diagnostic status is unsupported.");
                            }
                        }
                    }

                    Visit(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in element.EnumerateArray())
                {
                    Visit(child);
                }
            }
        }
    }

    private static void ValidateErrorCodes(ReadOnlySpan<byte> json)
    {
        using JsonDocument document = ParseDocument(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("code", out JsonElement code) ||
            code.ValueKind != JsonValueKind.String || !IsKnownErrorCode(code.GetString()))
        {
            throw new DiagnosticProtocolException("The diagnostic error code is unsupported.");
        }
    }

    private static void ValidateHello(ReadOnlySpan<byte> json, bool engine)
    {
        using JsonDocument document = ParseDocument(json);
        JsonElement root = RequireObject(document.RootElement);
        RequireString(root, "kind", "hello");
        RequireInt32(root, "protocolVersion", 0, int.MaxValue);
        if (engine)
        {
            RequireString(root, "engine");
            RequireString(root, "engineVersion");
            RequireString(root, "snsCommit");
        }
        else
        {
            RequireString(root, "clientVersion");
            RequireSha256(root, "schemaSha256");
        }
    }

    private static void ValidateAnalysis(ReadOnlySpan<byte> json)
    {
        using JsonDocument document = ParseDocument(json);
        JsonElement root = RequireObject(document.RootElement);
        RequireString(root, "kind", "analysis");
        RequireInt64(root, "index", 0, long.MaxValue);
        RequireBoolean(root, "typeUrlOk");
        string direction = RequireString(root, "direction");
        if (direction is not ("client" or "server" or "d3b"))
        {
            throw new DiagnosticProtocolException("The diagnostic direction is unsupported.");
        }

        string status = RequireString(root, "status");
        if (!IsKnownStatus(status))
        {
            throw new DiagnosticProtocolException("The diagnostic status is unsupported.");
        }

        JsonElement fields = RequireKind(root, "fields", JsonValueKind.Array);
        JsonElement problems = RequireKind(root, "problems", JsonValueKind.Array);
        foreach (JsonElement problem in problems.EnumerateArray())
        {
            RequireObject(problem);
            string code = RequireString(problem, "code");
            if (!IsKnownProblemCode(code))
            {
                throw new DiagnosticProtocolException("The diagnostic problem code is unsupported.");
            }
        }
        foreach (JsonElement field in fields.EnumerateArray())
        {
            ValidateAnalysisField(field);
        }
    }

    private static void ValidateAnalysisField(JsonElement field)
    {
        RequireObject(field);
        RequireInt32(field, "number", 1, 536_870_911);
        RequireBoolean(field, "repeated");
        RequireBoolean(field, "packed");
        RequireInt32(field, "occurrences", 0, int.MaxValue);
        JsonElement wireTypes = RequireKind(field, "wireTypes", JsonValueKind.Array);
        foreach (JsonElement wireType in wireTypes.EnumerateArray())
        {
            if (wireType.ValueKind != JsonValueKind.String || !IsKnownWireType(wireType.GetString()))
            {
                throw new DiagnosticProtocolException("The diagnostic wire type is unsupported.");
            }
        }

        RequireKind(field, "observed", JsonValueKind.Object);
        JsonElement fieldProblems = RequireKind(field, "problems", JsonValueKind.Array);
        foreach (JsonElement problem in fieldProblems.EnumerateArray())
        {
            if (problem.ValueKind != JsonValueKind.String || !IsKnownProblemCode(problem.GetString()))
            {
                throw new DiagnosticProtocolException("The diagnostic problem code is unsupported.");
            }
        }

        if (field.TryGetProperty("nested", out JsonElement nested))
        {
            if (nested.ValueKind != JsonValueKind.Array)
            {
                throw new DiagnosticProtocolException("The nested diagnostic fields are invalid.");
            }

            foreach (JsonElement nestedField in nested.EnumerateArray())
            {
                ValidateAnalysisField(nestedField);
            }
        }
    }

    private static void ValidateSummary(ReadOnlySpan<byte> json)
    {
        using JsonDocument document = ParseDocument(json);
        JsonElement root = RequireObject(document.RootElement);
        RequireString(root, "kind", "summary");
        RequireInt32(root, "protocolVersion", 0, int.MaxValue);
        RequireString(root, "clientVersion");
        RequireSha256(root, "schemaSha256");
        RequireInt64(root, "frames", 0, long.MaxValue);
        RequireInt64(root, "dropped", 0, long.MaxValue);
        string coverage = RequireString(root, "coverage");
        if (coverage is not ("complete" or "partial"))
        {
            throw new DiagnosticProtocolException("The diagnostic coverage value is unsupported.");
        }

        JsonElement statuses = RequireKind(root, "statuses", JsonValueKind.Object);
        foreach (JsonProperty status in statuses.EnumerateObject())
        {
            if (!IsKnownStatus(status.Name) || !status.Value.TryGetInt64(out long count) || count < 0)
            {
                throw new DiagnosticProtocolException("The diagnostic status count is invalid.");
            }
        }

        JsonElement problems = RequireKind(root, "problems", JsonValueKind.Object);
        foreach (JsonProperty problem in problems.EnumerateObject())
        {
            if (!IsKnownProblemCode(problem.Name) || !problem.Value.TryGetInt64(out long count) || count < 0)
            {
                throw new DiagnosticProtocolException("The diagnostic problem count is invalid.");
            }
        }
    }

    private static void ValidateError(ReadOnlySpan<byte> json)
    {
        using JsonDocument document = ParseDocument(json);
        JsonElement root = RequireObject(document.RootElement);
        RequireString(root, "kind", "error");
        RequireString(root, "code");
    }

    private static JsonElement RequireObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new DiagnosticProtocolException("The diagnostic message must be an object.");
        }

        return element;
    }

    private static JsonElement RequireKind(JsonElement parent, string name, JsonValueKind kind)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind != kind)
        {
            throw new DiagnosticProtocolException("The diagnostic message is missing a required property.");
        }

        return value;
    }

    private static string RequireString(JsonElement parent, string name, string? expected = null)
    {
        JsonElement value = RequireKind(parent, name, JsonValueKind.String);
        string result = value.GetString()!;
        if (expected is not null && result != expected)
        {
            throw new DiagnosticProtocolException("The diagnostic message kind is invalid.");
        }

        return result;
    }

    private static void RequireBoolean(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new DiagnosticProtocolException("The diagnostic message is missing a required boolean.");
        }
    }

    private static void RequireInt32(JsonElement parent, string name, int minimum, int maximum)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int parsed) || parsed < minimum || parsed > maximum)
        {
            throw new DiagnosticProtocolException("The diagnostic message has an invalid integer property.");
        }
    }

    private static void RequireInt64(JsonElement parent, string name, long minimum, long maximum)
    {
        if (!parent.TryGetProperty(name, out JsonElement value) || !value.TryGetInt64(out long parsed) || parsed < minimum || parsed > maximum)
        {
            throw new DiagnosticProtocolException("The diagnostic message has an invalid integer property.");
        }
    }

    private static void RequireSha256(JsonElement parent, string name)
    {
        string value = RequireString(parent, name);
        if (value.Length != 64 || value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new DiagnosticProtocolException("The diagnostic schema fingerprint is invalid.");
        }
    }

    private static bool IsKnownWireType(string? value)
    {
        return value is "VARINT" or "I64" or "LEN" or "I32" or "GROUP" or "UNKNOWN";
    }

    private static bool IsKnownStatus(string? value)
    {
        return value is "VALID" or "SCHEMA_MISMATCH" or "MALFORMED" or "TRUNCATED" or "TRAILING_BYTES" or "UNKNOWN_TYPE" or "LIMIT_EXCEEDED";
    }

    private static bool IsKnownProblemCode(string? value)
    {
        return value is "UNKNOWN_TYPE" or "UNKNOWN_FIELD_NUMBER" or "WIRE_TYPE_MISMATCH" or "PACKED_NOT_ALLOWED"
            or "INVALID_UTF8" or "GROUP_UNSUPPORTED" or "DEPTH_EXCEEDED" or "LENGTH_OVERFLOW" or "MALFORMED"
            or "VARINT_OVERFLOW" or "TRUNCATED" or "LIMIT_EXCEEDED" or "SCHEMA_MESSAGE_MISSING" or "SCHEMA_INVALID";
    }

    private static bool IsKnownErrorCode(string? value)
    {
        return value is "CONTRACT_INVALID" or "VERSION_UNSUPPORTED" or "SCHEMA_FINGERPRINT_INVALID" or "SCHEMA_INVALID"
            or "UNKNOWN_COMMAND" or "INTERNAL_ERROR" or "IO_ERROR" or "SOCKET_FAILED" or "UNSUPPORTED_PLATFORM";
    }

    private static void ValidateSchema(DiagnosticSchema schema)
    {
        if (schema.Messages is null)
        {
            throw new DiagnosticProtocolException("The diagnostic schema has no messages array.");
        }

        if (schema.Messages.Count > DiagnosticProtocol.MaxSchemaMessages)
        {
            throw new DiagnosticProtocolException("The diagnostic schema message limit was exceeded.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (DiagnosticMessage message in schema.Messages)
        {
            if (message is null || string.IsNullOrWhiteSpace(message.Name) || !names.Add(message.Name) || message.Fields is null)
            {
                throw new DiagnosticProtocolException("The diagnostic schema has an invalid or duplicate message name.");
            }

            if (message.Fields.Count > DiagnosticProtocol.MaxFieldsPerMessage)
            {
                throw new DiagnosticProtocolException("The diagnostic schema field limit was exceeded.");
            }

            var numbers = new HashSet<int>();
            foreach (DiagnosticField field in message.Fields)
            {
                if (field is null || field.Number is < 1 or > 536_870_911 || !numbers.Add(field.Number)
                    || string.IsNullOrWhiteSpace(field.Name)
                    || (field.Packed && (!field.Repeated || !IsPackable(field.Kind)))
                    || (field.Kind == DiagnosticFieldKind.Message) != (field.MessageType is not null)
                    || (field.Kind != DiagnosticFieldKind.Enumeration && field.EnumType is not null))
                {
                    throw new DiagnosticProtocolException("The diagnostic schema has an invalid field.");
                }
            }
        }

        foreach (DiagnosticField field in schema.Messages.SelectMany(message => message.Fields))
        {
            if (field.Kind == DiagnosticFieldKind.Message &&
                (field.MessageType is null || !names.Contains(field.MessageType)))
            {
                throw new DiagnosticProtocolException("The diagnostic schema has a dangling message type.");
            }
        }
    }

    private static void ValidateSchemaShape(JsonElement root)
    {
        JsonElement messages = RequireKind(root, "messages", JsonValueKind.Array);
        foreach (JsonElement message in messages.EnumerateArray())
        {
            RequireObject(message);
            string name = RequireString(message, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DiagnosticProtocolException("The diagnostic schema has an invalid message name.");
            }

            RequireBoolean(message, "mapEntry");
            JsonElement fields = RequireKind(message, "fields", JsonValueKind.Array);
            foreach (JsonElement field in fields.EnumerateArray())
            {
                RequireObject(field);
                RequireInt32(field, "number", 1, 536_870_911);
                RequireString(field, "name");
                RequireString(field, "kind");
                RequireBoolean(field, "repeated");
                RequireBoolean(field, "packed");
                RequireBoolean(field, "hasPresence");
                ValidateOptionalString(field, "messageType");
                ValidateOptionalString(field, "enumType");
                ValidateOptionalString(field, "oneof");
            }
        }
    }

    private static void ValidateOptionalString(JsonElement parent, string name)
    {
        if (parent.TryGetProperty(name, out JsonElement value) && value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
        {
            throw new DiagnosticProtocolException("The diagnostic schema has an invalid optional string.");
        }
    }

    private static bool IsPackable(DiagnosticFieldKind kind)
    {
        return kind is not (DiagnosticFieldKind.Utf8String or DiagnosticFieldKind.Bytes or DiagnosticFieldKind.Message or DiagnosticFieldKind.Group);
    }

    private static JsonDocument ParseDocument(ReadOnlySpan<byte> json)
    {
        try
        {
            return JsonDocument.Parse(json.ToArray());
        }
        catch (JsonException)
        {
            throw new DiagnosticProtocolException("The diagnostic message was malformed.");
        }
    }
}
