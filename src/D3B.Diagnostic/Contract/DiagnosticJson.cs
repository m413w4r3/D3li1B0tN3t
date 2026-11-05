using System.Text.Json;

namespace D3B.Diagnostic.Contract;

/// <summary>Strict parsing and deterministic serialization helpers for protocol messages.</summary>
public static class DiagnosticJson
{
    /// <summary>Parses and validates an engine hello.</summary>
    public static DiagnosticEngineHello DeserializeHello(ReadOnlySpan<byte> json)
    {
        ValidateProtocolVersion(json);
        return Deserialize<DiagnosticEngineHello>(json);
    }

    /// <summary>Parses an analysis and rejects unsupported status or problem codes.</summary>
    public static DiagnosticAnalysis DeserializeAnalysis(ReadOnlySpan<byte> json)
    {
        ValidateCodes(json);
        return Deserialize<DiagnosticAnalysis>(json);
    }

    /// <summary>Parses a summary and validates its protocol version and enum keys.</summary>
    public static DiagnosticSummary DeserializeSummary(ReadOnlySpan<byte> json)
    {
        ValidateProtocolVersion(json);
        ValidateCodes(json);
        return Deserialize<DiagnosticSummary>(json);
    }

    /// <summary>Parses a redacted engine error and rejects unknown error codes.</summary>
    public static DiagnosticError DeserializeError(ReadOnlySpan<byte> json)
    {
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

        DiagnosticSchema schema = Deserialize<DiagnosticSchema>(json);
        ValidateSchema(schema);
        return schema;
    }

    /// <summary>Parses a client hello and validates its protocol version.</summary>
    public static DiagnosticHello DeserializeClientHello(ReadOnlySpan<byte> json)
    {
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
                if (field is null || field.Number is < 1 or > 536_870_911 || !numbers.Add(field.Number))
                {
                    throw new DiagnosticProtocolException("The diagnostic schema has an invalid or duplicate field number.");
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
